"""Builds the SuperDictate website into website/public (Python 3.9+, standard library only).

    python website/build.py                    # SITE_URL defaults to https://superdictate.pages.dev
    SITE_URL=https://example.com python website/build.py

One template (src/index.html) and one strings file per language (i18n/<lang>.json)
become static pages: / (English), /de/, /ru/, /es/. {{key}} is replaced with the
HTML-escaped string; keys ending in _html are inserted as written. The build fails
when a language lacks a key the template uses, so no page ships half-translated.
Cloudflare Pages runs this with build output directory website/public.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import html
import json
import os
import re
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SRC = HERE / "src"
OUT = HERE / "public"

SITE_URL = os.environ.get("SITE_URL", "https://superdictate.pages.dev").rstrip("/")
GITHUB = "https://github.com/ZazaKin/SuperDictate-windows"
LANGS = ["en", "de", "ru", "es"]  # The first one is served at the root.
PLACEHOLDER = re.compile(r"\{\{\s*(\w+)\s*\}\}")


def page_path(lang: str) -> str:
    return "/" if lang == LANGS[0] else f"/{lang}/"


def asset_url(name: str) -> str:
    """Versioned by content, so the long cache in _headers never serves a stale file."""
    digest = hashlib.sha256((SRC / "assets" / name).read_bytes()).hexdigest()[:10]
    return f"/assets/{name}?v={digest}"


def lang_switch(current: str, strings: dict[str, dict[str, str]]) -> str:
    items = []
    for lang in LANGS:
        current_attr = ' aria-current="page"' if lang == current else ""
        name = html.escape(strings[lang]["lang_name"])
        items.append(
            f'<li><a href="{page_path(lang)}" hreflang="{lang}" lang="{lang}" title="{name}"{current_attr}>{lang.upper()}</a></li>'
        )
    label = html.escape(strings[current]["lang_label"])
    return f'<ul class="langs" aria-label="{label}">' + "".join(items) + "</ul>"


def alternates() -> str:
    links = [f'<link rel="alternate" hreflang="{lang}" href="{SITE_URL}{page_path(lang)}">' for lang in LANGS]
    links.append(f'<link rel="alternate" hreflang="x-default" href="{SITE_URL}/">')
    return "\n".join(links)


def render(template: str, values: dict[str, str], where: str) -> str:
    missing = sorted({key for key in PLACEHOLDER.findall(template) if key not in values})
    if missing:
        sys.exit(f"{where}: missing strings: {', '.join(missing)}")

    def replace(match: re.Match[str]) -> str:
        key = match.group(1)
        value = values[key]
        return value if key.endswith("_html") else html.escape(value, quote=True)

    return PLACEHOLDER.sub(replace, template)


def main() -> None:
    strings = {lang: json.loads((HERE / "i18n" / f"{lang}.json").read_text(encoding="utf-8")) for lang in LANGS}
    reference = set(strings[LANGS[0]])
    for lang in LANGS[1:]:
        missing = sorted(reference - set(strings[lang]))
        extra = sorted(set(strings[lang]) - reference)
        if missing or extra:
            sys.exit(f"i18n/{lang}.json differs from {LANGS[0]}.json: missing {missing}, extra {extra}")

    # Empty the folder rather than delete it: a preview server may be serving from it.
    OUT.mkdir(exist_ok=True)
    for child in OUT.iterdir():
        shutil.rmtree(child) if child.is_dir() else child.unlink()
    shutil.copytree(SRC / "assets", OUT / "assets")
    for name in ("_headers", "_redirects", "favicon.ico"):
        shutil.copy2(SRC / name, OUT / name)

    template = (SRC / "index.html").read_text(encoding="utf-8")
    shared = {
        "site_url": SITE_URL,
        "github_url": GITHUB,
        "download_url": f"{GITHUB}/releases/latest",
        "donate_url": f"{GITHUB}#donate",
        "css": asset_url("site.css"),
        "js": asset_url("site.js"),
        "alternates_html": alternates(),
    }
    for lang in LANGS:
        values = {
            **strings[lang],
            **shared,
            "lang": lang,
            "home": page_path(lang),
            "canonical": SITE_URL + page_path(lang),
            "langswitch_html": lang_switch(lang, strings),
        }
        target = OUT / page_path(lang).strip("/") / "index.html"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(render(template, values, f"{lang} page"), encoding="utf-8")

    notfound = (SRC / "404.html").read_text(encoding="utf-8")
    (OUT / "404.html").write_text(render(notfound, {**strings[LANGS[0]], **shared, "lang": LANGS[0]}, "404 page"), encoding="utf-8")

    urls = []
    for lang in LANGS:
        links = "".join(f'<xhtml:link rel="alternate" hreflang="{other}" href="{SITE_URL}{page_path(other)}"/>' for other in LANGS)
        urls.append(f"<url><loc>{SITE_URL}{page_path(lang)}</loc>{links}</url>")
    (OUT / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">'
        + "".join(urls) + "</urlset>\n", encoding="utf-8")
    (OUT / "robots.txt").write_text(f"User-agent: *\nAllow: /\nSitemap: {SITE_URL}/sitemap.xml\n", encoding="utf-8")

    # RFC 9116: where to report a vulnerability; must not claim to be valid for over a year.
    expires = (dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=360)).strftime("%Y-%m-%dT00:00:00Z")
    well_known = OUT / ".well-known"
    well_known.mkdir()
    (well_known / "security.txt").write_text(
        f"Contact: {GITHUB}/security/advisories/new\n"
        f"Expires: {expires}\n"
        "Preferred-Languages: en, de, ru, es\n"
        f"Canonical: {SITE_URL}/.well-known/security.txt\n"
        f"Policy: {GITHUB}/blob/main/SECURITY.md\n", encoding="utf-8")

    print(f"Built {len(LANGS)} languages into {OUT} for {SITE_URL}")


if __name__ == "__main__":
    main()
