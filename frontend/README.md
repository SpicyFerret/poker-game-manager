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
npm run build       # production build -> dist/frontend/browser
```

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
- `routes` — the custom domain to attach to the Worker (commented out until decided).

CI (`.github/workflows/frontend-ci.yml`) runs build + test on every push/PR, and deploys
via Wrangler once `CLOUDFLARE_API_TOKEN` exists as a repo secret.
