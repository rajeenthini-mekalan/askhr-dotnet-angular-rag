import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';
import { AuthService } from './auth.service';

/** Only our own API gets the token. Never send it to third-party URLs. */
const isApiRequest = (url: string) => url.startsWith('/api/');

/**
 * Functional interceptor (registered with withInterceptors([...]) in app.config.ts).
 * Refreshes the token if it is about to expire, then adds "Authorization: Bearer ...".
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApiRequest(req.url)) return next(req);

  const auth = inject(AuthService); // must be called synchronously, before any await/subscribe

  return from(auth.getValidToken()).pipe(
    switchMap((token) =>
      next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req),
    ),
  );
};
