import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AccessTokens, RegisterRequest, UpdateProfileRequest, UserProfile } from './auth.models';
import { TokenStorage, readUserIdFromToken } from './token-storage';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorage);

  private readonly tokens = signal<AccessTokens | null>(this.storage.read());
  private readonly currentUser = signal<UserProfile | null>(null);

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.tokens() !== null);

  get accessToken(): string | null {
    return this.tokens()?.accessToken ?? null;
  }

  get refreshToken(): string | null {
    return this.tokens()?.refreshToken ?? null;
  }

  register(request: RegisterRequest): Observable<string> {
    return this.http.post<string>(`${environment.apiUrl}/users/register`, request);
  }

  login(email: string, password: string): Observable<AccessTokens> {
    return this.http
      .post<AccessTokens>(`${environment.apiUrl}/users/login`, { email, password })
      .pipe(tap((tokens) => this.setTokens(tokens)));
  }

  /**
   * Used by the interceptor when a request comes back 401. Deliberately does not
   * go through the interceptor's retry path itself.
   */
  refresh(): Observable<AccessTokens> {
    return this.http
      .post<AccessTokens>(`${environment.apiUrl}/users/refresh-token`, {
        refreshToken: this.refreshToken,
      })
      .pipe(tap((tokens) => this.setTokens(tokens)));
  }

  loadProfile(): Observable<UserProfile> {
    const token = this.accessToken;
    const userId = token ? readUserIdFromToken(token) : null;

    if (!userId) {
      throw new Error('Cannot load a profile without an authenticated user.');
    }

    return this.http
      .get<UserProfile>(`${environment.apiUrl}/users/${userId}`)
      .pipe(tap((profile) => this.currentUser.set(profile)));
  }

  updateProfile(request: UpdateProfileRequest): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/users/me/profile`, request).pipe(
      tap(() => {
        const current = this.currentUser();

        if (current) {
          this.currentUser.set({ ...current, ...request });
        }
      }),
    );
  }

  logout(): void {
    this.tokens.set(null);
    this.currentUser.set(null);
    this.storage.clear();
  }

  setTokens(tokens: AccessTokens): void {
    this.tokens.set(tokens);
    this.storage.write(tokens);
  }
}
