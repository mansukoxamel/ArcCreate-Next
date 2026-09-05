"""Compare long-note judgement counts in real AFF charts.

This is a research aid, not an implementation of the Arcaea client.  It mirrors
the count formulas and arc-chain tests found in the pinned ArcCreate and Arcade
Plus source revisions documented in ``AFF_NOTE_JUDGEMENT_RESEARCH.md``.
"""

from __future__ import annotations

import argparse
import bisect
import json
import math
import re
import struct
import unicodedata
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable

from bs4 import BeautifulSoup


TIMING_RE = re.compile(r"^timing\((-?\d+),(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)\);$")
TAP_RE = re.compile(r"^\((-?\d+),(-?\d+)\);$")
HOLD_RE = re.compile(r"^hold\((-?\d+),(-?\d+),(-?\d+)\);$")
ARC_RE = re.compile(r"^arc\(([^)]*)\)(?:\[(.*)\])?;$")
ARCTAP_RE = re.compile(r"arctap\((-?\d+)(?:,[^)]*)?\)")
GROUP_RE = re.compile(r"^timinggroup\((.*)\)\{$")
DIFFICULTY = {0: "Past", 1: "Present", 2: "Future", 3: "Beyond", 4: "Eternal"}


@dataclass
class Timing:
    time: int
    bpm: float


@dataclass
class Hold:
    start: int
    end: int
    group: int


@dataclass
class Arc:
    start: int
    end: int
    x1: float
    x2: float
    y1: float
    y2: float
    color: int
    line_flag: str
    arctaps: int
    group: int

    @property
    def is_trace(self) -> bool:
        return self.line_flag != "false"

    @property
    def is_designant(self) -> bool:
        return self.line_flag == "designant"


@dataclass
class Group:
    attributes: str = ""
    timings: list[Timing] = field(default_factory=list)

    @property
    def noinput(self) -> bool:
        return "noinput" in {part.strip() for part in self.attributes.split(",")}


@dataclass
class Chart:
    density: float = 1.0
    taps: list[tuple[int, int]] = field(default_factory=list)
    holds: list[Hold] = field(default_factory=list)
    arcs: list[Arc] = field(default_factory=list)
    groups: dict[int, Group] = field(default_factory=lambda: {0: Group()})


@dataclass(frozen=True)
class NativeJudgementPoint:
    time: int
    weight: int = 1


def parse_chart(path: Path) -> Chart:
    chart = Chart()
    current_group = 0
    next_group = 1
    in_body = False
    for raw_line in path.read_text(encoding="utf-8-sig").splitlines():
        line = raw_line.strip()
        if not in_body:
            if line.startswith("TimingPointDensityFactor:"):
                chart.density = float(line.split(":", 1)[1])
            if line == "-":
                in_body = True
            continue
        if not line:
            continue
        group_match = GROUP_RE.match(line)
        if group_match:
            current_group = next_group
            next_group += 1
            chart.groups[current_group] = Group(group_match.group(1))
            continue
        if line == "};":
            current_group = 0
            continue
        timing_match = TIMING_RE.match(line)
        if timing_match:
            chart.groups[current_group].timings.append(
                Timing(int(timing_match.group(1)), float(timing_match.group(2)))
            )
            continue
        tap_match = TAP_RE.match(line)
        if tap_match:
            chart.taps.append((int(tap_match.group(1)), current_group))
            continue
        hold_match = HOLD_RE.match(line)
        if hold_match:
            chart.holds.append(
                Hold(int(hold_match.group(1)), int(hold_match.group(2)), current_group)
            )
            continue
        arc_match = ARC_RE.match(line)
        if arc_match:
            args = [part.strip() for part in arc_match.group(1).split(",")]
            if len(args) < 10:
                raise ValueError(f"{path}: invalid arc: {line}")
            chart.arcs.append(
                Arc(
                    start=int(args[0]),
                    end=int(args[1]),
                    x1=float(args[2]),
                    x2=float(args[3]),
                    y1=float(args[5]),
                    y2=float(args[6]),
                    color=int(args[7]),
                    line_flag=args[9],
                    arctaps=len(ARCTAP_RE.findall(arc_match.group(2) or "")),
                    group=current_group,
                )
            )
    for group in chart.groups.values():
        group.timings.sort(key=lambda item: item.time)
    return chart


def bpm_at(chart: Chart, group_id: int, timing: int) -> float | None:
    timings = chart.groups[group_id].timings
    if not timings:
        return None
    index = bisect.bisect_right([item.time for item in timings], timing) - 1
    return timings[max(index, 0)].bpm


def is_connected(first: Arc, second: Arc, model: str) -> bool:
    if first is second or first.is_trace != second.is_trace:
        return False
    if model == "arccreate":
        return (
            abs(first.end - second.start) <= 1
            and first.x2 == second.x1
            and first.y2 == second.y1
        )
    if model == "native_3_6_1":
        return (
            abs(first.end - second.start) <= 9
            and abs(first.x2 - second.x1) < 0.1
            and first.y2 == second.y1
        )
    return (
        abs(first.end - second.start) < 10
        and abs(first.x2 - second.x1) < 0.1
        and abs(first.y2 - second.y1) < 0.01
    )


def connection_flags(arcs: list[Arc], model: str) -> tuple[set[int], set[int]]:
    """Return (successors, predecessors) for connected arcs."""
    by_end: dict[int, list[Arc]] = defaultdict(list)
    for arc in arcs:
        by_end[arc.end].append(arc)
    radius = 1 if model == "arccreate" else 9
    successors: set[int] = set()
    predecessors: set[int] = set()
    for target in arcs:
        for end_time in range(target.start - radius, target.start + radius + 1):
            candidates = [
                candidate
                for candidate in by_end[end_time]
                if is_connected(candidate, target, model)
            ]
            if candidates:
                successors.add(id(target))
                predecessors.update(id(candidate) for candidate in candidates)
    return successors, predecessors


def predecessor_flags(arcs: list[Arc], model: str) -> set[int]:
    """Backward-compatible helper returning arcs that have a predecessor."""
    return connection_flags(arcs, model)[0]


def f32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def native_interval_ms(bpm: float, density: float) -> float:
    """Reproduce the sequential ARM32 float operations in 3.6.1."""
    absolute = f32(abs(f32(bpm)))
    factor = 1.0 if absolute >= f32(255.0) else 2.0
    return f32(f32(f32(60_000.0 / absolute) / factor) / f32(density))


def native_3_6_1_judgement_points(
    start: int,
    end: int,
    bpm: float,
    density: float,
    continued: bool,
    has_successor: bool,
) -> list[NativeJudgementPoint]:
    """Reproduce LogicLongNoteBase/LogicArcNote judgement-point setup.

    The outgoing-arc adjustment removes the final point in one boundary case.
    ``weight`` is normally one.  At one kind of outgoing arc boundary, the
    client removes the last timestamp and changes the previous point's weight
    from one to two, preserving the total score/combo count.
    """
    duration = end - start
    if bpm == 0 or density == 0:
        return [] if duration == 0 else [
            NativeJudgementPoint(math.trunc(f32(f32(start) + f32(duration * 0.5))))
        ]
    interval = native_interval_ms(bpm, density)
    total = math.trunc(f32(f32(duration) / interval))
    first_index = 0 if continued else 1
    points = []
    for index in range(first_index, total):
        timing = math.trunc(f32(f32(start) + f32(f32(index) * interval)))
        if timing < end:
            points.append(NativeJudgementPoint(timing))
    if not points and duration != 0:
        points.append(
            NativeJudgementPoint(
                math.trunc(f32(f32(start) + f32(f32(duration) * f32(0.5))))
            )
        )
    if (
        has_successor
        and len(points) >= 2
        and math.trunc(f32(f32(duration - 2) / interval)) == total - 1
    ):
        points.pop()
        points[-1] = NativeJudgementPoint(points[-1].time, 2)
    return points


def interval_ms(bpm: float, density: float) -> float:
    absolute = abs(bpm)
    return 60_000.0 / absolute / (1 if absolute >= 255 else 2) / density


def long_count(duration: int, bpm: float | None, density: float, continued: bool,
               model: str, kind: str) -> int:
    if duration == 0:
        return 0 if kind == "arc" or model == "arccreate" else 1
    if bpm is None:
        return 0
    if bpm == 0:
        return 0 if model == "arccreate" else 1
    total = math.floor(duration / interval_ms(bpm, density))
    modifier = 0 if continued else 1
    return 1 if total <= modifier else total - modifier


def count_chart(chart: Chart, model: str) -> dict[str, int]:
    active_taps = sum(not chart.groups[group].noinput for _, group in chart.taps)
    hold_count = 0
    arc_count = 0
    arctap_count = 0
    for hold in chart.holds:
        if chart.groups[hold.group].noinput:
            continue
        bpm = bpm_at(chart, hold.group, hold.start)
        if model == "native_3_6_1" and bpm is not None:
            hold_count += sum(
                point.weight for point in native_3_6_1_judgement_points(
                    hold.start, hold.end, bpm, chart.density, False, False
                )
            )
        else:
            hold_count += long_count(
                hold.end - hold.start,
                bpm,
                chart.density,
                False,
                model,
                "hold",
            )
    continued_arcs, outgoing_arcs = connection_flags(chart.arcs, model)
    for arc in chart.arcs:
        if chart.groups[arc.group].noinput or arc.is_designant:
            continue
        arctap_count += arc.arctaps
        if arc.is_trace:
            continue
        bpm = bpm_at(chart, arc.group, arc.start)
        if model == "native_3_6_1" and bpm is not None:
            arc_count += sum(
                point.weight for point in native_3_6_1_judgement_points(
                    arc.start,
                    arc.end,
                    bpm,
                    chart.density,
                    id(arc) in continued_arcs,
                    id(arc) in outgoing_arcs,
                )
            )
        else:
            arc_count += long_count(
                arc.end - arc.start,
                bpm,
                chart.density,
                id(arc) in continued_arcs,
                model,
                "arc",
            )
    return {
        "tap": active_taps,
        "hold_judgements": hold_count,
        "arc_judgements": arc_count,
        "arctap": arctap_count,
        "total": active_taps + hold_count + arc_count + arctap_count,
    }


def normalize_title(value: str) -> str:
    value = unicodedata.normalize("NFKC", value).casefold()
    return "".join(character for character in value if character.isalnum())


def parse_optional_count(value: str) -> int | None:
    value = value.strip().replace(",", "")
    if value == "-":
        return 0
    return int(value) if value.isdigit() else None


def load_wiki_counts(wiki_dir: Path) -> dict[tuple[str, str], dict[str, int | None]]:
    counts: dict[tuple[str, str], dict[str, int | None]] = {}
    for prefix in ("053_", "054_"):
        for path in wiki_dir.glob(f"{prefix}*.html"):
            soup = BeautifulSoup(path.read_text(encoding="utf-8"), "html.parser")
            for table in soup.find_all("table"):
                headers = [cell.get_text(" ", strip=True) for cell in table.find_all("th")]
                if headers[:5] != ["Notes", "Song", "Composer", "Diff.", "Lv."]:
                    continue
                for row in table.find_all("tr")[1:]:
                    cells = row.find_all("td")
                    if len(cells) < 5:
                        continue
                    note_text = cells[0].get_text(" ", strip=True).replace(",", "")
                    link = cells[1].find("a")
                    title = (link.get("title") if link else cells[1].get_text(" ", strip=True)) or ""
                    difficulty = cells[3].get_text(" ", strip=True)
                    if note_text.isdigit() and difficulty in DIFFICULTY.values():
                        counts[(normalize_title(title), difficulty)] = {
                            "total": int(note_text),
                            "tap": parse_optional_count(cells[5].get_text(" ", strip=True)) if len(cells) > 5 else None,
                            "hold_judgements": parse_optional_count(cells[6].get_text(" ", strip=True)) if len(cells) > 6 else None,
                            "arc_judgements": parse_optional_count(cells[7].get_text(" ", strip=True)) if len(cells) > 7 else None,
                            "arctap": parse_optional_count(cells[8].get_text(" ", strip=True)) if len(cells) > 8 else None,
                        }
    return counts


def load_song_titles(songlist_path: Path) -> dict[tuple[str, int], str]:
    data = json.loads(songlist_path.read_text(encoding="utf-8"))
    result: dict[tuple[str, int], str] = {}
    for song in data["songs"]:
        titles = song.get("title_localized", {})
        candidates = [titles.get("en", ""), titles.get("ja", "")]
        for difficulty in song.get("difficulties", []):
            rating_class = difficulty.get("ratingClass")
            for title in candidates:
                if title:
                    result[(song["id"], rating_class)] = title
                    break
    return result


def iter_aff_files(songs_dir: Path) -> Iterable[Path]:
    return sorted(songs_dir.glob("*/*.aff"))


def analyze(songs_dir: Path, wiki_dir: Path) -> dict[str, object]:
    wiki_counts = load_wiki_counts(wiki_dir)
    song_titles = load_song_titles(songs_dir / "songlist")
    rows = []
    parse_errors = []
    feature_counts: Counter[str] = Counter()
    differences: Counter[int] = Counter()
    native_differences: Counter[int] = Counter()
    wiki_matches = Counter()
    wiki_component_matches = Counter()
    aggregate_model_counts = Counter()
    native_component_differences: dict[str, Counter[int]] = {
        component: Counter()
        for component in ("tap", "hold_judgements", "arc_judgements", "arctap", "total")
    }
    crossing_examples = []
    difference_examples = []
    different_model_wiki_examples = []
    wiki_mismatch_examples = []
    native_wiki_mismatch_examples = []

    for path in iter_aff_files(songs_dir):
        try:
            chart = parse_chart(path)
        except Exception as exc:  # Preserve the failure as evidence in the report.
            parse_errors.append({"path": str(path), "error": str(exc)})
            continue
        create = count_chart(chart, "arccreate")
        plus = count_chart(chart, "arcade_plus")
        native = count_chart(chart, "native_3_6_1")
        for model_name, counts in (
            ("arccreate", create),
            ("arcade_plus", plus),
            ("native_3_6_1", native),
        ):
            for component, count in counts.items():
                aggregate_model_counts[f"{model_name}_{component}"] += count
        for component in native_component_differences:
            native_component_differences[component][native[component] - plus[component]] += 1
        difference = plus["total"] - create["total"]
        differences[difference] += 1
        native_differences[native["total"] - plus["total"]] += 1
        feature_counts["charts"] += 1
        feature_counts["taps"] += len(chart.taps)
        feature_counts["holds"] += len(chart.holds)
        feature_counts["arcs"] += len(chart.arcs)
        feature_counts["arctaps"] += sum(arc.arctaps for arc in chart.arcs)
        feature_counts["designant_arcs"] += sum(arc.is_designant for arc in chart.arcs)
        feature_counts["zero_bpm_long_notes"] += sum(
            bpm_at(chart, note.group, note.start) == 0 for note in [*chart.holds, *chart.arcs]
        )
        feature_counts["strict_continuations"] += len(predecessor_flags(chart.arcs, "arccreate"))
        feature_counts["loose_continuations"] += len(predecessor_flags(chart.arcs, "arcade_plus"))
        feature_counts["native_3_6_1_continuations"] += len(
            predecessor_flags(chart.arcs, "native_3_6_1")
        )
        for note in [*chart.holds, *chart.arcs]:
            if chart.groups[note.group].noinput:
                continue
            internal_timings = [
                timing.time for timing in chart.groups[note.group].timings
                if note.start < timing.time < note.end
            ]
            if internal_timings:
                feature_counts["long_notes_crossing_timing"] += 1
                if len(crossing_examples) < 20:
                    crossing_examples.append(
                        {"path": str(path), "kind": type(note).__name__.lower(),
                         "start": note.start, "end": note.end, "internal_timings": internal_timings[:8]}
                    )

        try:
            rating_class = int(path.stem)
        except ValueError:
            rating_class = -1
        title = song_titles.get((path.parent.name, rating_class))
        difficulty = DIFFICULTY.get(rating_class)
        wiki_record = wiki_counts.get((normalize_title(title), difficulty)) if title and difficulty else None
        wiki_total = wiki_record["total"] if wiki_record else None
        if wiki_record is not None:
            wiki_matches["compared"] += 1
            create_match = create["total"] == wiki_total
            plus_match = plus["total"] == wiki_total
            native_match = native["total"] == wiki_total
            wiki_matches["arccreate"] += create_match
            wiki_matches["arcade_plus"] += plus_match
            wiki_matches["native_3_6_1"] += native_match
            wiki_matches["both"] += create_match and plus_match
            wiki_matches["neither"] += not create_match and not plus_match
            if difference:
                wiki_matches["different_models"] += 1
                wiki_matches["different_arccreate"] += create_match
                wiki_matches["different_arcade_plus"] += plus_match
                different_model_wiki_examples.append(
                    {"path": str(path), "title": title, "difficulty": difficulty,
                     "wiki_total": wiki_total, "arccreate_total": create["total"],
                     "arcade_plus_total": plus["total"], "native_3_6_1_total": native["total"]}
                )
            for component in ("tap", "hold_judgements", "arc_judgements", "arctap"):
                expected = wiki_record[component]
                if expected is None:
                    continue
                wiki_component_matches[f"{component}_compared"] += 1
                wiki_component_matches[f"{component}_arccreate"] += create[component] == expected
                wiki_component_matches[f"{component}_arcade_plus"] += plus[component] == expected
                wiki_component_matches[f"{component}_native_3_6_1"] += native[component] == expected
            if (not create_match or not plus_match) and len(wiki_mismatch_examples) < 40:
                wiki_mismatch_examples.append(
                    {"path": str(path), "title": title, "difficulty": difficulty,
                     "wiki": wiki_record, "arccreate": create, "arcade_plus": plus,
                     "native_3_6_1": native,
                     "difference": difference}
                )
            if not native_match:
                native_wiki_mismatch_examples.append(
                    {
                        "path": str(path),
                        "title": title,
                        "difficulty": difficulty,
                        "wiki": wiki_record,
                        "native_3_6_1": native,
                    }
                )
        if difference and len(difference_examples) < 30:
            difference_examples.append(
                {"path": str(path), "title": title, "difficulty": difficulty,
                 "wiki_total": wiki_total, "arccreate": create, "arcade_plus": plus,
                 "native_3_6_1": native,
                 "difference": difference}
            )
        rows.append((path, create, plus, native, wiki_total))

    return {
        "scope": {
            "songs_dir": str(songs_dir),
            "wiki_dir": str(wiki_dir),
            "parsed_aff_files": feature_counts["charts"],
            "parse_errors": parse_errors,
        },
        "features": dict(feature_counts),
        "model_total_difference_histogram": {
            str(key): value for key, value in sorted(differences.items())
        },
        "native_3_6_1_minus_arcade_plus_histogram": {
            str(key): value for key, value in sorted(native_differences.items())
        },
        "aggregate_model_counts": dict(aggregate_model_counts),
        "native_3_6_1_minus_arcade_plus_component_histograms": {
            component: {str(key): value for key, value in sorted(histogram.items())}
            for component, histogram in native_component_differences.items()
        },
        "wiki_total_comparison": dict(wiki_matches),
        "wiki_component_comparison": dict(wiki_component_matches),
        "difference_examples": difference_examples,
        "different_model_wiki_examples": different_model_wiki_examples,
        "wiki_mismatch_examples": wiki_mismatch_examples,
        "native_3_6_1_wiki_mismatches": native_wiki_mismatch_examples,
        "long_notes_crossing_timing_examples": crossing_examples,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--songs", type=Path, required=True)
    parser.add_argument("--wiki-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    report = analyze(args.songs, args.wiki_dir)
    rendered = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
    else:
        print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
