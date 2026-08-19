import { Injectable } from '@angular/core';

import { AccessTokens } from './auth.models';

const ACCESS_TOKEN_KEY = 'pgm.accessToken';
const REFRESH_TOKEN_KEY = 'pgm.refreshToken';

/**
 * Wraps localStorage so the rest of the app never touches it directly, and so
 * tests can swap it out. Access in a private-mode browser can throw, so every
 * read and write is guarded — losing the session is acceptable, crashing the
 * app on boot is not.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  read(): AccessTokens | null {
    const accessToken = this.get(ACCESS_TOKEN_KEY);
    const refreshToken = this.get(REFRESH_TOKEN_KEY);

    return accessToken && refreshToken ? { accessToken, refreshToken } : null;
  }

  write(tokens: AccessTokens): void {
    this.set(ACCESS_TOKEN_KEY, tokens.accessToken);
    this.set(REFRESH_TOKEN_KEY, tokens.refreshToken);
  }

  clear(): void {
    this.remove(ACCESS_TOKEN_KEY);
    this.remove(REFRESH_TOKEN_KEY);
  }

  private get(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  private set(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Storage unavailable: the session simply won't survive a reload.
    }
  }

  private remove(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      // Nothing to do — see set().
    }
  }
}

/**
 * Reads the `sub` claim without verifying the signature. That is fine here: the
 * client only uses it to know which profile to load, and every real decision is
 * made by the API against the signed token.
 */
export function readUserIdFromToken(accessToken: string): string | null {
  const payload = accessToken.split('.')[1];

  if (!payload) {
    return null;
  }

  try {
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const claims = JSON.parse(json) as { sub?: string };

    return claims.sub ?? null;
  } catch {
    return null;
  }
}
