import { InjectionToken } from '@angular/core';
import Keycloak from 'keycloak-js';

/** Public settings only: a SPA is a public client, so there is nothing secret to hide here. */
export const authConfig = {
  url: 'http://localhost:8080',
  realm: 'askhr',
  clientId: 'askhr-web',
} as const;

/** Realm roles (must match deploy/keycloak/askhr-realm.json). */
export const Roles = {
  HrAdmin: 'hr-admin',
  Employee: 'employee',
} as const;

/**
 * The keycloak-js instance behind an InjectionToken, so tests can swap in a fake
 * instead of talking to a real Keycloak.
 */
export const KEYCLOAK = new InjectionToken<Keycloak>('KEYCLOAK', {
  providedIn: 'root',
  factory: () => new Keycloak(authConfig),
});
