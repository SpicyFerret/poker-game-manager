interface Env {
  ASSETS: Fetcher;
  API_ORIGIN: string;
}

const API_PREFIX = '/api/v1/';

/**
 * Angular's i18n is a compile-time build: `ng build` emits one full copy of the
 * app per locale under its own directory, each with its own <base href>. So the
 * Worker, not Wrangler, owns locale routing — the built-in
 * `not_found_handling: "single-page-application"` only knows about a single
 * index.html at the root.
 *
 * Order matters: SUPPORTED_LOCALES[0] is the fallback when the browser asks for
 * something we don't publish.
 */
const SUPPORTED_LOCALES = ['pt', 'en'] as const;
type Locale = (typeof SUPPORTED_LOCALES)[number];

const DEFAULT_LOCALE: Locale = SUPPORTED_LOCALES[0];

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (url.pathname.startsWith(API_PREFIX)) {
      const target = new URL(url.pathname + url.search, env.API_ORIGIN);
      return fetch(new Request(target, request));
    }

    const locale = localeFromPath(url.pathname);

    if (!locale) {
      const preferred = negotiateLocale(request.headers.get('Accept-Language'));
      const target = new URL(`/${preferred}${url.pathname}${url.search}`, url.origin);

      return Response.redirect(target.toString(), 302);
    }

    const asset = await env.ASSETS.fetch(request);

    if (asset.status !== 404) {
      return asset;
    }

    // Unknown path inside a locale: hand it to the Angular router rather than
    // 404ing, which is what makes deep links like /pt/profile work on reload.
    const indexUrl = new URL(`/${locale}/index.html`, url.origin);
    const index = await env.ASSETS.fetch(new Request(indexUrl, { headers: request.headers }));

    return new Response(index.body, {
      status: index.ok ? 200 : index.status,
      headers: index.headers,
    });
  },
} satisfies ExportedHandler<Env>;

function localeFromPath(pathname: string): Locale | null {
  const [, first] = pathname.split('/');

  return SUPPORTED_LOCALES.includes(first as Locale) ? (first as Locale) : null;
}

/**
 * Minimal Accept-Language negotiation: highest-q match on the primary subtag,
 * so `pt-BR` and `pt-PT` both land on `pt`.
 */
function negotiateLocale(header: string | null): Locale {
  if (!header) {
    return DEFAULT_LOCALE;
  }

  const ranked = header
    .split(',')
    .map((part) => {
      const [tag, ...params] = part.trim().split(';');
      const q = params
        .map((p) => p.trim())
        .find((p) => p.startsWith('q='))
        ?.slice(2);

      return { tag: tag.toLowerCase().split('-')[0], quality: q ? Number(q) : 1 };
    })
    .filter((entry) => Number.isFinite(entry.quality))
    .sort((a, b) => b.quality - a.quality);

  return (
    ranked
      .map((entry) => entry.tag)
      .find((tag): tag is Locale => SUPPORTED_LOCALES.includes(tag as Locale)) ?? DEFAULT_LOCALE
  );
}
