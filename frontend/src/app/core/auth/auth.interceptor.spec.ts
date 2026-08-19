import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { environment } from '../../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        // The interceptor sends a dead session to /login, so that route has to
        // resolve or the navigation rejects.
        provideRouter([{ path: 'login', children: [] }]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  it('should not attach a token to the login request', () => {
    auth.setTokens({ accessToken: 'access', refreshToken: 'refresh' });

    client.post(`${environment.apiUrl}/users/login`, {}).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/users/login`);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('should attach the bearer token to an authenticated request', () => {
    auth.setTokens({ accessToken: 'access', refreshToken: 'refresh' });

    client.get(`${environment.apiUrl}/users/me`).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/users/me`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer access');
    request.flush({});
  });

  it('should refresh once and retry when the API answers 401', () => {
    auth.setTokens({ accessToken: 'stale', refreshToken: 'refresh' });

    let body: unknown = null;
    client.get(`${environment.apiUrl}/users/me`).subscribe((value) => (body = value));

    http
      .expectOne(
        (r) => r.url.endsWith('/users/me') && r.headers.get('Authorization') === 'Bearer stale',
      )
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    http
      .expectOne(`${environment.apiUrl}/users/refresh-token`)
      .flush({ accessToken: 'fresh', refreshToken: 'refresh2' });

    http
      .expectOne(
        (r) => r.url.endsWith('/users/me') && r.headers.get('Authorization') === 'Bearer fresh',
      )
      .flush({ ok: true });

    expect(body).toEqual({ ok: true });
    expect(auth.accessToken).toBe('fresh');
  });

  it('should sign the player out when the refresh itself fails', () => {
    auth.setTokens({ accessToken: 'stale', refreshToken: 'dead' });

    client.get(`${environment.apiUrl}/users/me`).subscribe({ error: () => undefined });

    http.expectOne(`${environment.apiUrl}/users/me`).flush(null, {
      status: 401,
      statusText: 'Unauthorized',
    });

    http.expectOne(`${environment.apiUrl}/users/refresh-token`).flush(null, {
      status: 400,
      statusText: 'Bad Request',
    });

    expect(auth.isAuthenticated()).toBe(false);
  });

  it('should not try to refresh when there is no refresh token', () => {
    client.get(`${environment.apiUrl}/users/me`).subscribe({ error: () => undefined });

    http.expectOne(`${environment.apiUrl}/users/me`).flush(null, {
      status: 401,
      statusText: 'Unauthorized',
    });

    http.expectNone(`${environment.apiUrl}/users/refresh-token`);
  });
});
