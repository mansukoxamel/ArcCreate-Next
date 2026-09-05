"""Archive all public note articles by nesiddo without downloading media."""

from __future__ import annotations

import hashlib
import html
import json
import re
import time
import urllib.request
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path


CREATOR = "nesiddo"
BASE_URL = "https://note.com"
OUTPUT_DIR = Path(__file__).resolve().parent.parent / "docs" / "references" / "nesiddo-note-archive"
USER_AGENT = "MAGATU-TEMPO-FORGE research archive/1.0"


def request_json(url: str) -> dict:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


class ArticleTextExtractor(HTMLParser):
    BLOCK_TAGS = {
        "address", "article", "aside", "blockquote", "div", "figcaption",
        "figure", "footer", "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "hr", "li", "main", "ol", "p", "pre", "section",
        "table", "tbody", "td", "th", "thead", "tr", "ul",
    }

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.parts: list[str] = []
        self.skip_depth = 0
        self.links: list[str | None] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in {"script", "style"}:
            self.skip_depth += 1
            return
        if self.skip_depth:
            return
        if tag in self.BLOCK_TAGS or tag == "br":
            self.parts.append("\n")
        if tag == "li":
            self.parts.append("- ")
        if tag == "a":
            self.links.append(dict(attrs).get("href"))

    def handle_endtag(self, tag: str) -> None:
        if tag in {"script", "style"} and self.skip_depth:
            self.skip_depth -= 1
            return
        if self.skip_depth:
            return
        if tag == "a" and self.links:
            href = self.links.pop()
            if href:
                self.parts.append(f" ({href})")
        if tag in self.BLOCK_TAGS:
            self.parts.append("\n")

    def handle_data(self, data: str) -> None:
        if not self.skip_depth:
            self.parts.append(data)

    def text(self) -> str:
        value = "".join(self.parts).replace("\xa0", " ")
        value = re.sub(r"[ \t]+", " ", value)
        value = re.sub(r" *\n *", "\n", value)
        value = re.sub(r"\n{3,}", "\n\n", value)
        return value.strip()


def html_to_text(body: str) -> str:
    parser = ArticleTextExtractor()
    parser.feed(body)
    parser.close()
    return parser.text()


def article_document(title: str, source_url: str, published: str, body: str) -> str:
    document = "\n".join(
        [
            "<!doctype html>",
            '<html lang="ja">',
            "<head>",
            '  <meta charset="utf-8">',
            f"  <title>{html.escape(title)}</title>",
            f'  <meta name="source" content="{html.escape(source_url, quote=True)}">',
            f'  <meta name="published" content="{html.escape(published, quote=True)}">',
            "</head>",
            "<body>",
            f"<h1>{html.escape(title)}</h1>",
            f'<p>Source: <a href="{html.escape(source_url, quote=True)}">{html.escape(source_url)}</a></p>',
            f"<p>Published: {html.escape(published)}</p>",
            "<hr>",
            body,
            "</body>",
            "</html>",
            "",
        ]
    )
    return "\n".join(line.rstrip() for line in document.splitlines()) + "\n"


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    articles_dir = OUTPUT_DIR / "articles"
    articles_dir.mkdir(exist_ok=True)

    summaries: list[dict] = []
    page = 1
    while True:
        listing = request_json(
            f"{BASE_URL}/api/v2/creators/{CREATOR}/contents?kind=note&page={page}"
        )["data"]
        summaries.extend(listing.get("contents", []))
        if listing.get("isLastPage", False):
            break
        page += 1
        time.sleep(0.2)

    manifest: list[dict] = []
    combined: list[str] = []
    for index, summary in enumerate(summaries, start=1):
        key = summary["key"]
        payload = request_json(f"{BASE_URL}/api/v3/notes/{key}")["data"]
        title = payload["name"]
        published_value = payload.get("publish_at") or summary.get("publishAt")
        published = str(published_value)
        date = published[:10]
        source_url = f"{BASE_URL}/{CREATOR}/n/{key}"
        body = payload.get("body") or ""
        filename = f"{date}_{key}.html"
        document = article_document(title, source_url, published, body)
        (articles_dir / filename).write_text(document, encoding="utf-8", newline="\n")
        plain_text = html_to_text(body)
        combined.extend(
            [
                "=" * 80,
                f"[{index:02d}] {title}",
                f"公開日: {published}",
                f"URL: {source_url}",
                f"記事キー: {key}",
                "=" * 80,
                plain_text,
                "",
            ]
        )
        manifest.append(
            {
                "index": index,
                "key": key,
                "title": title,
                "published": published,
                "url": source_url,
                "html_file": f"articles/{filename}",
                "body_sha256": hashlib.sha256(body.encode("utf-8")).hexdigest(),
                "body_html_characters": len(body),
                "plain_text_characters": len(plain_text),
                "can_read": summary.get("canRead"),
                "price": summary.get("price"),
            }
        )
        time.sleep(0.2)

    retrieved = datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")
    (OUTPUT_DIR / "all_articles.txt").write_text(
        f"nesiddo note archive\n取得日時: {retrieved}\n記事数: {len(manifest)}\n\n"
        + "\n".join(combined),
        encoding="utf-8",
        newline="\n",
    )
    metadata = {
        "creator": CREATOR,
        "profile_url": f"{BASE_URL}/{CREATOR}",
        "retrieved_at": retrieved,
        "article_count": len(manifest),
        "media_downloaded": False,
        "articles": manifest,
    }
    (OUTPUT_DIR / "manifest.json").write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )

    index_lines = [
        "# nesiddo note記事 保存索引",
        "",
        f"- 取得日時: {retrieved}",
        f"- 元プロフィール: {BASE_URL}/{CREATOR}",
        f"- 保存記事数: {len(manifest)}",
        "- 画像・動画本体: 未取得",
        "- 全文検索用: [all_articles.txt](all_articles.txt)",
        "",
        "## 記事一覧",
        "",
    ]
    for item in manifest:
        index_lines.append(
            f"{item['index']}. [{item['title']}]({item['html_file']}) "
            f"({item['published'][:10]}) — [原文]({item['url']})"
        )
    (OUTPUT_DIR / "README.md").write_text(
        "\n".join(index_lines) + "\n", encoding="utf-8", newline="\n"
    )

    if len(manifest) != 20:
        raise RuntimeError(f"Expected 20 articles, archived {len(manifest)}")
    if any(not item["can_read"] or item["price"] != 0 for item in manifest):
        raise RuntimeError("At least one article was not confirmed as free and readable")
    if any(item["body_html_characters"] == 0 for item in manifest):
        raise RuntimeError("At least one article body was empty")
    print(f"ARCHIVE_COMPLETE articles={len(manifest)} output={OUTPUT_DIR}")


if __name__ == "__main__":
    main()
