import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Functional guard: a plain function. inject() works inside it because the router
 * calls it in an injection context — no class, no CanActivate interface.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) return true;

  void auth.login(state.url); // leaves the app; Keycloak sends the user back to state.url
  return false;
};

/** Guard factory: roleGuard('hr-admin') returns a guard for that role. */
export const roleGuard =
  (role: string): CanActivateFn =>
  (_route, state) => {
    const auth = inject(AuthService);
    if (!auth.isAuthenticated()) {
      void auth.login(state.url);
      return false;
    }
    // Returning a UrlTree (instead of false) tells the router to redirect there.
    return auth.hasRole(role) ? true : inject(Router).createUrlTree(['/forbidden']);
  };
