"""Archive Arcaea Wiki technical/reference pages without downloading media."""

from __future__ import annotations

import hashlib
import json
import re
import time
import urllib.parse
from datetime import datetime, timezone
from pathlib import Path

import requests
from bs4 import BeautifulSoup


BASE_URL = "https://wikiwiki.jp/arcaea/"
LIST_URL = "https://wikiwiki.jp/arcaea/?cmd=list"
OUTPUT_DIR = Path(__file__).resolve().parent.parent / "docs" / "references" / "arcaea-wiki-archive"
USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36"
)
SESSION = requests.Session()
SESSION.headers.update(
    {
        "User-Agent": USER_AGENT,
        "Accept-Language": "ja,en-US;q=0.8,en;q=0.6",
        "Accept": "text/html,application/xhtml+xml",
    }
)

# 曲別攻略、コメント、ストーリー本文を避け、ゲーム仕様・譜面・計算・
# 各種一覧と、その履歴・サブページを保存する。
TARGET_ROOTS = (
    "Arcaea Sound Collection",
    "Daily Top 100",
    "FAQ",
    "Nintendo Switch",
    "アップデート履歴",
    "イラストレーター順",
    "こぼれ話",
    "コースモード",
    "コンポーザー順",
    "タイトル順",
    "ノーツ数順",
    "パートナー",
    "パック順",
    "ポテンシャル研究所",
    "リズムゲーム用語解説",
    "ルール",
    "レベル順",
    "ワールドモード",
    "演奏時間順",
    "高レベル非公式難易度",
    "初心者の方へ",
    "遅延調整報告",
    "譜面制作者",
    "譜面定数表",
    "include/パートナー",
    "include/譜面定数表",
)

# JavaScriptでランキングを組み立てるため、保存対象の #content 自体には
# 本文が存在しないことを確認済みのページ。
KNOWN_EMPTY_PAGES = {"Daily Top 100"}


def request(url: str) -> tuple[str, dict[str, str]]:
    for attempt in range(8):
        response = SESSION.get(url, timeout=45)
        if response.status_code == 200:
            response.encoding = "utf-8"
            return response.text, {
                key.lower(): value for key, value in response.headers.items()
            }
        if response.status_code != 429 or attempt == 7:
            response.raise_for_status()
        else:
            retry_after = response.headers.get("Retry-After")
            wait_seconds = int(retry_after) if retry_after and retry_after.isdigit() else 30 * (attempt + 1)
            wait_seconds = min(wait_seconds, 180)
            print(f"RATE_LIMIT wait={wait_seconds}s url={url}", flush=True)
            time.sleep(wait_seconds)
    raise RuntimeError(f"Request retry exhausted: {url}")


def is_target(page: str) -> bool:
    if page in {"FrontPage", "MenuBar"}:
        return True
    return any(
        page == root or page.startswith(root + "/") or page.startswith(root + "(")
        for root in TARGET_ROOTS
    )


def list_pages() -> tuple[list[str], dict[str, str]]:
    source, headers = request(LIST_URL)
    soup = BeautifulSoup(source, "html.parser")
    pages = {"FrontPage", "MenuBar"}
    for anchor in soup.select('a[href^="/arcaea/"]'):
        href = anchor.get("href", "")
        path = href.removeprefix("/arcaea/").split("?", 1)[0].split("#", 1)[0]
        page = urllib.parse.unquote(path)
        if not page or page.startswith("::") or page.startswith("コメント/"):
            continue
        if page.endswith("/Header") or page.endswith("/Footer"):
            continue
        if is_target(page):
            pages.add(page)
    return sorted(pages, key=str.casefold), headers


def page_url(page: str) -> str:
    if page == "FrontPage":
        return BASE_URL
    return BASE_URL + urllib.parse.quote(page, safe="/()~")


def clean_content(source: str, source_url: str) -> tuple[str, str, str]:
    soup = BeautifulSoup(source, "html.parser")
    title = soup.title.get_text(" ", strip=True) if soup.title else source_url
    content = soup.select_one("#content")
    if content is None:
        raise RuntimeError(f"No #content element: {source_url}")

    for unwanted in content.select("script, style, form, button, video, audio, source, picture"):
        unwanted.decompose()
    for unwanted in content.select('[id*="ad-container"], .pcomment-form-placeholder'):
        unwanted.decompose()
    for image in list(content.select("img")):
        alt = image.get("alt", "").strip()
        replacement = soup.new_tag("span")
        replacement.string = f"[画像: {alt}]" if alt else "[画像]"
        image.replace_with(replacement)
    for iframe in list(content.select("iframe")):
        src = urllib.parse.urljoin(source_url, iframe.get("src", ""))
        replacement = soup.new_tag("span")
        replacement.string = f"[埋め込み: {src}]" if src else "[埋め込み]"
        iframe.replace_with(replacement)
    for anchor in content.select("a[href]"):
        anchor["href"] = urllib.parse.urljoin(source_url, anchor["href"])

    body_html = str(content)
    body_html = "\n".join(line.rstrip() for line in body_html.splitlines()).strip() + "\n"
    plain_text = content.get_text("\n", strip=True).replace("\xa0", " ")
    plain_text = re.sub(r"[ \t]+", " ", plain_text)
    plain_text = re.sub(r"\n{3,}", "\n\n", plain_text).strip()
    return title, body_html, plain_text


def html_document(title: str, source_url: str, body_html: str) -> str:
    escaped_title = title.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    escaped_url = source_url.replace("&", "&amp;").replace('"', "&quot;")
    return (
        "<!doctype html>\n"
        '<html lang="ja">\n<head>\n  <meta charset="utf-8">\n'
        f"  <title>{escaped_title}</title>\n"
        f'  <meta name="source" content="{escaped_url}">\n'
        "</head>\n<body>\n"
        f'<p>Source: <a href="{escaped_url}">{escaped_url}</a></p>\n'
        f"{body_html}</body>\n</html>\n"
    )


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    articles_dir = OUTPUT_DIR / "pages"
    articles_dir.mkdir(exist_ok=True)

    pages, list_headers = list_pages()
    manifest: list[dict] = []
    combined: list[str] = []
    for index, page in enumerate(pages, start=1):
        url = page_url(page)
        page_hash = hashlib.sha256(page.encode("utf-8")).hexdigest()[:12]
        filename = f"{index:03d}_{page_hash}.html"
        existing = articles_dir / filename
        if existing.exists():
            source = existing.read_text(encoding="utf-8")
            headers = {}
            downloaded = False
        else:
            source, headers = request(url)
            downloaded = True
        title, body_html, plain_text = clean_content(source, url)
        document = html_document(title, url, body_html)
        (articles_dir / filename).write_text(document, encoding="utf-8", newline="\n")
        manifest.append(
            {
                "index": index,
                "page": page,
                "title": title,
                "url": url,
                "html_file": f"pages/{filename}",
                "etag": headers.get("etag"),
                "last_modified": headers.get("last-modified"),
                "body_sha256": hashlib.sha256(body_html.encode("utf-8")).hexdigest(),
                "plain_text_characters": len(plain_text),
            }
        )
        combined.extend(
            [
                "=" * 80,
                f"[{index:03d}] {title}",
                f"ページ名: {page}",
                f"URL: {url}",
                "=" * 80,
                plain_text,
                "",
            ]
        )
        if index == 1 or index % 10 == 0 or index == len(pages):
            print(f"PROGRESS {index}/{len(pages)} {page}", flush=True)
        if downloaded:
            time.sleep(7.0)

    retrieved = datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")
    (OUTPUT_DIR / "all_pages.txt").write_text(
        f"Arcaea Wiki technical/reference archive\n取得日時: {retrieved}\n"
        f"保存ページ数: {len(manifest)}\n全ページ一覧上のページ数: 1424\n\n"
        + "\n".join(combined),
        encoding="utf-8",
        newline="\n",
    )
    metadata = {
        "site": "Arcaea Wiki",
        "base_url": BASE_URL,
        "list_url": LIST_URL,
        "retrieved_at": retrieved,
        "page_count": len(manifest),
        "site_page_count_at_selection": 1424,
        "selection_roots": list(TARGET_ROOTS),
        "excluded": ["曲別攻略ページ", "コメントページ", "ストーリー本文", "画像・動画本体"],
        "media_downloaded": False,
        "known_empty_pages": sorted(KNOWN_EMPTY_PAGES),
        "list_etag": list_headers.get("etag"),
        "pages": manifest,
    }
    (OUTPUT_DIR / "manifest.json").write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    index_lines = [
        "# Arcaea Wiki 技術・資料ページ保存索引",
        "",
        f"- 取得日時: {retrieved}",
        f"- 元サイト: {BASE_URL}",
        f"- 保存ページ数: {len(manifest)}",
        "- サイト全ページ一覧: 1,424ページ",
        "- 対象: ゲーム仕様、譜面、計算、定数、遅延、World／Course、各種一覧、変更履歴",
        "- 除外: 曲別攻略、コメント、ストーリー本文、画像・動画本体",
        "- 全文検索用: [all_pages.txt](all_pages.txt)",
        "- 既知の本文なしページ: `Daily Top 100`（JavaScript生成のため保存HTMLの本文は空）",
        "",
        "## 保存ページ",
        "",
    ]
    for item in manifest:
        index_lines.append(
            f"{item['index']}. [{item['page']}]({item['html_file']}) — [原文]({item['url']})"
        )
    (OUTPUT_DIR / "README.md").write_text(
        "\n".join(index_lines) + "\n", encoding="utf-8", newline="\n"
    )

    if not 100 <= len(manifest) <= 150:
        raise RuntimeError(f"Unexpected target count: {len(manifest)}")
    unexpected_empty_pages = [
        item["page"]
        for item in manifest
        if item["plain_text_characters"] == 0
        and item["page"] not in KNOWN_EMPTY_PAGES
    ]
    if unexpected_empty_pages:
        raise RuntimeError(
            f"Unexpected archived pages without text: {unexpected_empty_pages}"
        )
    print(f"ARCHIVE_COMPLETE pages={len(manifest)} output={OUTPUT_DIR}", flush=True)


if __name__ == "__main__":
    main()
