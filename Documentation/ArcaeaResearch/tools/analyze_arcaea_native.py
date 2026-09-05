"""Reproducible, read-only inspection helpers for Arcaea native libraries.

This tool never modifies the input binary.  It intentionally keeps LIEF and
Capstone out of the application's runtime requirements; install them only in
the research environment that runs this script.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path
from typing import Any, Iterable

try:
    import lief
except ImportError as exc:  # pragma: no cover - depends on research machine
    raise SystemExit("LIEF is required: pip install lief") from exc

try:
    from capstone import (
        CS_ARCH_ARM,
        CS_ARCH_ARM64,
        CS_MODE_ARM,
        CS_MODE_LITTLE_ENDIAN,
        CS_MODE_THUMB,
        Cs,
    )
    from capstone.arm_const import ARM_OP_MEM
    from capstone.arm64_const import ARM64_OP_MEM
except ImportError as exc:  # pragma: no cover - depends on research machine
    raise SystemExit("Capstone is required: pip install capstone") from exc


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def parse_binary(path: Path):
    binary = lief.parse(str(path))
    if binary is None:
        raise SystemExit(f"LIEF could not parse: {path}")
    return binary


def symbol_rows(binary, filters: Iterable[str]) -> list[dict[str, Any]]:
    needles = tuple(filters)
    rows = []
    for symbol in binary.dynamic_symbols:
        if needles and not any(needle in symbol.name for needle in needles):
            continue
        rows.append(
            {
                "address": symbol.value,
                "size": symbol.size,
                "name": symbol.name,
            }
        )
    return sorted(rows, key=lambda row: (row["address"], row["name"]))


def inventory(path: Path, binary) -> dict[str, Any]:
    section_names = {section.name for section in binary.sections}
    return {
        "path": str(path.resolve()),
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "format": str(binary.format).split(".")[-1],
        "architecture": str(binary.header.machine_type).split(".")[-1],
        "dynamic_symbol_count": len(binary.dynamic_symbols),
        "has_static_symbol_table": ".symtab" in section_names,
        "has_debug_sections": any(name.startswith(".debug_") for name in section_names),
        "sections": [
            {
                "name": section.name,
                "address": section.virtual_address,
                "size": section.size,
            }
            for section in binary.sections
        ],
    }


def capstone_for(binary, thumb: bool):
    architecture = str(binary.header.machine_type).split(".")[-1]
    if architecture == "ARM":
        mode = CS_MODE_THUMB if thumb else CS_MODE_ARM
        return Cs(CS_ARCH_ARM, mode | CS_MODE_LITTLE_ENDIAN)
    if architecture == "AARCH64":
        return Cs(CS_ARCH_ARM64, CS_MODE_ARM | CS_MODE_LITTLE_ENDIAN)
    raise SystemExit(f"Unsupported architecture: {architecture}")


def read_bytes(binary, address: int, size: int) -> bytes:
    content = bytes(binary.get_content_from_virtual_address(address, size))
    if len(content) != size:
        raise SystemExit(
            f"Could not read {size} bytes at 0x{address:x}; got {len(content)}"
        )
    return content


def decode_scalar(data: bytes, kind: str):
    formats = {
        "f32": "<f",
        "f64": "<d",
        "i32": "<i",
        "u32": "<I",
        "i64": "<q",
        "u64": "<Q",
    }
    expected = struct.calcsize(formats[kind])
    if len(data) != expected:
        raise SystemExit(f"{kind} requires exactly {expected} bytes")
    return struct.unpack(formats[kind], data)[0]


def plt_map(binary) -> dict[int, str]:
    """Map ARM32 PLT stub addresses to relocation symbols.

    The inspected ARM32 binaries use the standard 20-byte PLT header followed
    by fixed 12-byte stubs.  Refuse to guess for other layouts/architectures.
    """

    architecture = str(binary.header.machine_type).split(".")[-1]
    if architecture != "ARM":
        return {}
    plt = binary.get_section(".plt")
    if plt is None:
        return {}
    first_stub = plt.virtual_address + 20
    return {
        first_stub + index * 12: relocation.symbol.name
        for index, relocation in enumerate(binary.pltgot_relocations)
    }


def command_inventory(args) -> None:
    reports = []
    for path in args.binary:
        reports.append(inventory(path, parse_binary(path)))
    print(json.dumps(reports, ensure_ascii=False, indent=2))


def command_symbols(args) -> None:
    binary = parse_binary(args.binary)
    print(json.dumps(symbol_rows(binary, args.contains), ensure_ascii=False, indent=2))


def command_disasm(args) -> None:
    binary = parse_binary(args.binary)
    code = read_bytes(binary, args.address, args.size)
    disassembler = capstone_for(binary, args.thumb)
    stubs = plt_map(binary) if args.resolve_plt else {}
    for instruction in disassembler.disasm(code, args.address):
        suffix = ""
        if instruction.mnemonic in {"bl", "blx"} and instruction.op_str.startswith("#0x"):
            target = int(instruction.op_str[1:], 16)
            name = stubs.get(target)
            if name:
                suffix = f" ; {name}"
        print(
            f"{instruction.address:08x}  {instruction.bytes.hex():10}  "
            f"{instruction.mnemonic:10} {instruction.op_str}{suffix}"
        )


def command_read(args) -> None:
    binary = parse_binary(args.binary)
    data = read_bytes(binary, args.address, args.size)
    result: dict[str, Any] = {
        "address": args.address,
        "size": args.size,
        "hex": data.hex(),
    }
    if args.kind:
        result[args.kind] = decode_scalar(data, args.kind)
    print(json.dumps(result, ensure_ascii=False, indent=2))


def command_relocations(args) -> None:
    binary = parse_binary(args.binary)
    rows = []
    for relocation in binary.dynamic_relocations:
        if args.address_start is not None and relocation.address < args.address_start:
            continue
        if args.address_end is not None and relocation.address >= args.address_end:
            continue
        if args.addend is not None and relocation.addend != args.addend:
            continue
        rows.append(
            {
                "address": relocation.address,
                "addend": relocation.addend,
                "type": str(relocation.type),
                "symbol": relocation.symbol.name,
            }
        )
    print(json.dumps(rows, ensure_ascii=False, indent=2))


def command_memrefs(args) -> None:
    """Find instructions whose memory operand uses an exact displacement."""

    binary = parse_binary(args.binary)
    section = binary.get_section(args.section)
    if section is None:
        raise SystemExit(f"Section not found: {args.section}")
    disassembler = capstone_for(binary, args.thumb)
    disassembler.detail = True
    disassembler.skipdata = not args.dynamic_symbols
    architecture = str(binary.header.machine_type).split(".")[-1]
    memory_operand_type = ARM_OP_MEM if architecture == "ARM" else ARM64_OP_MEM
    rows = []
    ranges = [(section.virtual_address, bytes(section.content))]
    if args.dynamic_symbols:
        section_end = section.virtual_address + section.size
        ranges = []
        for symbol in binary.dynamic_symbols:
            address = symbol.value & ~1 if architecture == "ARM" and args.thumb else symbol.value
            if symbol.size <= 0 or not section.virtual_address <= address < section_end:
                continue
            ranges.append((address, read_bytes(binary, address, symbol.size)))
    seen = set()
    for range_address, code in ranges:
        for instruction in disassembler.disasm(code, range_address):
            if instruction.id == 0:  # Capstone skip-data pseudo instruction
                continue
            if args.mnemonic and instruction.mnemonic != args.mnemonic:
                continue
            if any(
                operand.type == memory_operand_type
                and operand.mem.disp == args.displacement
                for operand in instruction.operands
            ):
                if instruction.address in seen:
                    continue
                seen.add(instruction.address)
                rows.append(
                    {
                        "address": instruction.address,
                        "bytes": instruction.bytes.hex(),
                        "mnemonic": instruction.mnemonic,
                        "operands": instruction.op_str,
                    }
                )
    print(json.dumps(rows, ensure_ascii=False, indent=2))


def command_callrefs(args) -> None:
    """Find dynamically named functions that call a selected PLT symbol."""

    binary = parse_binary(args.binary)
    architecture = str(binary.header.machine_type).split(".")[-1]
    disassembler = capstone_for(binary, args.thumb)
    stubs = plt_map(binary)
    text_section = binary.get_section(".text")
    if text_section is None:
        raise SystemExit("Section not found: .text")
    text_end = text_section.virtual_address + text_section.size
    rows = []
    for symbol in binary.dynamic_symbols:
        if symbol.size <= 0:
            continue
        address = symbol.value & ~1 if architecture == "ARM" and args.thumb else symbol.value
        if not text_section.virtual_address <= address < text_end:
            continue
        if address + symbol.size > text_end:
            continue
        code = read_bytes(binary, address, symbol.size)
        for instruction in disassembler.disasm(code, address):
            if instruction.mnemonic not in {"bl", "blx"} or not instruction.op_str.startswith("#0x"):
                continue
            target = int(instruction.op_str[1:], 16)
            callee = stubs.get(target, "")
            if args.contains not in callee:
                continue
            rows.append(
                {
                    "caller_address": address,
                    "caller": symbol.name,
                    "instruction_address": instruction.address,
                    "callee": callee,
                }
            )
    print(json.dumps(rows, ensure_ascii=False, indent=2))


def command_findinsn(args) -> None:
    """Find instructions in an executable section by mnemonic and operand text."""

    binary = parse_binary(args.binary)
    section = binary.get_section(args.section)
    if section is None:
        raise SystemExit(f"Section not found: {args.section}")
    disassembler = capstone_for(binary, args.thumb)
    disassembler.skipdata = True
    rows = []
    for instruction in disassembler.disasm(bytes(section.content), section.virtual_address):
        if instruction.id == 0:
            continue
        if args.mnemonic and instruction.mnemonic != args.mnemonic:
            continue
        if args.contains and args.contains not in instruction.op_str:
            continue
        rows.append(
            {
                "address": instruction.address,
                "bytes": instruction.bytes.hex(),
                "mnemonic": instruction.mnemonic,
                "operands": instruction.op_str,
            }
        )
    print(json.dumps(rows, ensure_ascii=False, indent=2))


def auto_int(value: str) -> int:
    return int(value, 0)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    inv = subparsers.add_parser("inventory", help="print ELF identity as JSON")
    inv.add_argument("binary", nargs="+", type=Path)
    inv.set_defaults(func=command_inventory)

    symbols = subparsers.add_parser("symbols", help="filter dynamic symbols")
    symbols.add_argument("binary", type=Path)
    symbols.add_argument("--contains", action="append", default=[])
    symbols.set_defaults(func=command_symbols)

    disasm = subparsers.add_parser("disasm", help="disassemble a virtual-address range")
    disasm.add_argument("binary", type=Path)
    disasm.add_argument("address", type=auto_int)
    disasm.add_argument("size", type=auto_int)
    disasm.add_argument("--thumb", action="store_true")
    disasm.add_argument("--resolve-plt", action="store_true")
    disasm.set_defaults(func=command_disasm)

    read = subparsers.add_parser("read", help="read bytes at a virtual address")
    read.add_argument("binary", type=Path)
    read.add_argument("address", type=auto_int)
    read.add_argument("size", type=auto_int)
    read.add_argument(
        "--kind",
        choices=("f32", "f64", "i32", "u32", "i64", "u64"),
    )
    read.set_defaults(func=command_read)

    relocations = subparsers.add_parser(
        "relocations", help="filter dynamic relocations used for RTTI/vtable recovery"
    )
    relocations.add_argument("binary", type=Path)
    relocations.add_argument("--address-start", type=auto_int)
    relocations.add_argument("--address-end", type=auto_int)
    relocations.add_argument("--addend", type=auto_int)
    relocations.set_defaults(func=command_relocations)

    memrefs = subparsers.add_parser(
        "memrefs", help="find memory operands with an exact displacement"
    )
    memrefs.add_argument("binary", type=Path)
    memrefs.add_argument("displacement", type=auto_int)
    memrefs.add_argument("--section", default=".text")
    memrefs.add_argument("--mnemonic")
    memrefs.add_argument("--thumb", action="store_true")
    memrefs.add_argument("--dynamic-symbols", action="store_true")
    memrefs.set_defaults(func=command_memrefs)

    callrefs = subparsers.add_parser(
        "callrefs", help="find calls to a PLT symbol by name substring"
    )
    callrefs.add_argument("binary", type=Path)
    callrefs.add_argument("contains")
    callrefs.add_argument("--thumb", action="store_true")
    callrefs.set_defaults(func=command_callrefs)

    findinsn = subparsers.add_parser(
        "findinsn", help="find instructions by mnemonic and operand substring"
    )
    findinsn.add_argument("binary", type=Path)
    findinsn.add_argument("--section", default=".text")
    findinsn.add_argument("--mnemonic")
    findinsn.add_argument("--contains")
    findinsn.add_argument("--thumb", action="store_true")
    findinsn.set_defaults(func=command_findinsn)
    return parser


def main() -> None:
    args = build_parser().parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
