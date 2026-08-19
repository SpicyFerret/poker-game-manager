# Frontend

Angular standalone app, built as static assets and served by a Cloudflare Worker
(`worker/index.ts`) that also proxies `/api/v1/*` to the API — front and API share
one domain in production, so there's no CORS to deal with there.

## Develop

```bash
npm install
npm start          # ng serve on http://localhost:4200, calls the API directly (see environment.ts)
```

The backend (`dotnet run --project ../backend/src/Web.Api`) must be running locally for API
calls to succeed; it allows `http://localhost:4200` via CORS in the Development environment.

## Build

```bash
npm run build       # production build -> dist/frontend/browser/{pt,en}
```

## Translations

Angular's i18n is compile-time. `pt` is the source locale — the strings in the templates *are* the
Portuguese — and `en` is a translation file. After adding or changing any `i18n` attribute or
`$localize` string:

```bash
npx ng extract-i18n   # refreshes src/locale/messages.xlf
```

then add the matching `<trans-unit>` (same `id`) to `src/locale/messages.en.xlf`. Translations are
matched by id, so the `<source>` text there is only a human reference.

The build emits one complete copy of the app per locale, each with its own `<base href>`. That is why
`wrangler.jsonc` sets `not_found_handling: "none"`: Wrangler's SPA fallback only knows about a single
root `index.html`, so `worker/index.ts` does the locale routing instead — it redirects `/` to the
locale negotiated from `Accept-Language`, and serves `/{locale}/index.html` for unknown paths inside
a locale so deep links survive a reload.

## Test

```bash
npm test             # Vitest, headless
```

## Deploy

The Worker (static assets + `/api/v1/*` proxy) is deployed with Wrangler, driven by
`wrangler.jsonc`:

```bash
npm run build
npm run deploy       # wrangler deploy — needs CLOUDFLARE_API_TOKEN
```

`wrangler.jsonc` has two placeholders to fill in once they're known:
- `vars.API_ORIGIN` — the public hostname the Cloudflare Tunnel (already running on the
  Raspberry Pi cluster) exposes for the API. See `infra/kubernetes/README.md` for the
  internal address the tunnel should point at.
- `routes` — the custom domain to attach to the Worker.

CI (`.github/workflows/frontend-ci.yml`) runs build + test on every push/PR, and deploys
via Wrangler once `CLOUDFLARE_API_TOKEN` exists as a repo secret.
