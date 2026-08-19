import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { readUserIdFromToken } from './token-storage';

/** Unsigned token carrying only the `sub` claim the client reads. */
function tokenFor(userId: string): string {
  const payload = btoa(JSON.stringify({ sub: userId }));
  return `header.${payload}.signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should start signed out', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.user()).toBeNull();
  });

  it('should keep the tokens after a successful login', () => {
    service.login('a@b.com', 'Password123').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/users/login`);
    expect(request.request.body).toEqual({ email: 'a@b.com', password: 'Password123' });
    request.flush({ accessToken: 'access', refreshToken: 'refresh' });

    expect(service.isAuthenticated()).toBe(true);
    expect(service.accessToken).toBe('access');
  });

  it('should restore the session from storage on construction', () => {
    service.setTokens({ accessToken: 'access', refreshToken: 'refresh' });

    // A fresh injector reads what the previous instance persisted.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    const restored = TestBed.inject(AuthService);
    expect(restored.isAuthenticated()).toBe(true);

    http = TestBed.inject(HttpTestingController);
  });

  it('should clear tokens and user on logout', () => {
    service.setTokens({ accessToken: 'access', refreshToken: 'refresh' });

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('pgm.accessToken')).toBeNull();
  });

  it('should load the profile of the user in the token', () => {
    const userId = '4b1b2b60-0f0a-4a6b-9f13-0e6b2d7a1a11';
    service.setTokens({ accessToken: tokenFor(userId), refreshToken: 'refresh' });

    service.loadProfile().subscribe();

    const request = http.expectOne(`${environment.apiUrl}/users/${userId}`);
    request.flush({
      id: userId,
      email: 'a@b.com',
      firstName: 'Dan',
      lastName: 'M',
      displayName: 'Dan',
      paymentType: 'Pix',
      paymentHandle: 'dan@example.com',
    });

    expect(service.user()?.displayName).toBe('Dan');
  });

  it('should merge the update into the cached profile', () => {
    const userId = '4b1b2b60-0f0a-4a6b-9f13-0e6b2d7a1a11';
    service.setTokens({ accessToken: tokenFor(userId), refreshToken: 'refresh' });

    service.loadProfile().subscribe();
    http.expectOne(`${environment.apiUrl}/users/${userId}`).flush({
      id: userId,
      email: 'a@b.com',
      firstName: 'Dan',
      lastName: 'M',
      displayName: 'Dan',
      paymentType: null,
      paymentHandle: null,
    });

    service
      .updateProfile({ displayName: 'Danilo', paymentType: 'Pix', paymentHandle: 'chave' })
      .subscribe();

    http.expectOne(`${environment.apiUrl}/users/me/profile`).flush(null);

    expect(service.user()?.displayName).toBe('Danilo');
    expect(service.user()?.paymentHandle).toBe('chave');
  });
});

describe('readUserIdFromToken', () => {
  it('should read the sub claim', () => {
    expect(readUserIdFromToken(tokenFor('abc'))).toBe('abc');
  });

  it('should return null for a malformed token', () => {
    expect(readUserIdFromToken('not-a-token')).toBeNull();
    expect(readUserIdFromToken('a.not-base64!.c')).toBeNull();
  });
});
