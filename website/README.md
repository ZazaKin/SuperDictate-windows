# SuperDictate website

The product site: one page in English (`/`), German (`/de/`), Russian (`/ru/`)
and Spanish (`/es/`). Every demo on it is the app's UI recreated in HTML and CSS
and animated by `src/assets/site.js`; there are no videos, web fonts, trackers
or cookies, and nothing is loaded from another site.

| Path | What it is |
|---|---|
| `src/index.html` | The page template; `{{key}}` comes from the language files |
| `i18n/<lang>.json` | All text, one file per language (every file needs every key) |
| `src/assets/` | Stylesheet, script, logo, share card, touch icon |
| `src/_headers` | Security and cache headers served by Cloudflare Pages |
| `build.py` | Builds everything into `public/` (standard library only) |
| `make-images.py` | Redraws the share card and icons (needs Pillow; results are committed) |
| `brand/brand-kit.html` | The brand board the site follows |

## Build and preview

```bash
python website/build.py
python -m http.server 8787 --directory website/public
```

Then open http://localhost:8787. Set `SITE_URL` when the site lives elsewhere
than `https://superdictate.pages.dev` (it goes into canonical links, the sitemap
and `security.txt`).

## Security

- `src/_headers` sends a strict Content-Security-Policy (own files only, no
  inline code, Trusted Types enforced, no framing), HSTS, `nosniff`, a
  restrictive Permissions-Policy and cross-origin isolation.
- The pages also carry the policy as a meta tag, so a local preview enforces it.
- No form, no backend, no cookies, no third-party requests.
- `/.well-known/security.txt` points to the private vulnerability reporting.

Keep it that way: no inline `<script>`, `style=""` or `on…=""` in the template,
and no `innerHTML` in the script. The browser will block them.

## Deploy on Cloudflare Pages

One time, in the Cloudflare dashboard:

1. **Workers & Pages › Create › Pages › Connect to Git**, authorize GitHub and
   pick `ZazaKin/SuperDictate-windows`.
2. Settings:
   - Project name: `superdictate` (gives `superdictate.pages.dev`)
   - Production branch: `main`
   - Framework preset: None
   - Build command: `python3 website/build.py`
   - Build output directory: `website/public`
   - Environment variable `SITE_URL`: `https://<your project>.pages.dev`, or
     your own domain later
3. **Save and Deploy.** Every push to `main` redeploys. Under Settings › Builds,
   **Build watch paths** set to `website/*` skips rebuilds for app-only changes.

Check the headers afterwards at https://securityheaders.com.
