interface Env {
  ASSETS: Fetcher;
  API_ORIGIN: string;
}

const API_PREFIX = '/api/v1/';

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (url.pathname.startsWith(API_PREFIX)) {
      const target = new URL(url.pathname + url.search, env.API_ORIGIN);
      return fetch(new Request(target, request));
    }

    return env.ASSETS.fetch(request);
  },
} satisfies ExportedHandler<Env>;
