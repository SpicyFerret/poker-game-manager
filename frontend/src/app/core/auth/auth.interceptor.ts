import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';

/** Endpoints that must never carry a bearer token or trigger a refresh loop. */
const ANONYMOUS_PATHS = ['/users/login', '/users/register', '/users/refresh-token'];

export function authInterceptor(
  request: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (ANONYMOUS_PATHS.some((path) => request.url.includes(path))) {
    return next(request);
  }

  return next(withToken(request, auth.accessToken)).pipe(
    catchError((error: unknown) => {
      const isExpired = error instanceof HttpErrorResponse && error.status === 401;

      if (!isExpired || !auth.refreshToken) {
        return throwError(() => error);
      }

      // One attempt only: the retried request is not itself retried, so a
      // refresh token that is also dead ends the session instead of looping.
      return auth.refresh().pipe(
        switchMap((tokens) => next(withToken(request, tokens.accessToken))),
        catchError((refreshError: unknown) => {
          auth.logout();
          void router.navigate(['/login']);

          return throwError(() => refreshError);
        }),
      );
    }),
  );
}

function withToken(
  request: HttpRequest<unknown>,
  accessToken: string | null,
): HttpRequest<unknown> {
  return accessToken
    ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : request;
}
