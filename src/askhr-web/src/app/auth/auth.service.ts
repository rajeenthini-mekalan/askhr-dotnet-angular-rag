import { computed, inject, Injectable, signal } from '@angular/core';
import { KEYCLOAK } from './auth.config';

export interface AuthUser {
  username: string;
  name: string;
  roles: readonly string[];
}

/**
 * Signal-based auth state. Keycloak (keycloak-js) does the protocol work: code flow + PKCE,
 * token storage in memory, refresh. This service turns that into signals the UI can read.
 *
 * Writable signal stays private; the rest of the app only sees read-only signals and computed values,
 * so the only way auth state changes is through this class.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly keycloak = inject(KEYCLOAK);
  private readonly _user = signal<AuthUser | null>(null);

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);
  readonly roles = computed(() => this._user()?.roles ?? []);

  /** Runs once before the first route is resolved (see provideAppInitializer in app.config.ts). */
  async init(): Promise<void> {
    // When a refresh fails (e.g. the SSO session ended), drop the user so guards send them to login.
    this.keycloak.onTokenExpired = () => {
      this.keycloak.updateToken(30).catch(() => this._user.set(null));
    };
    this.keycloak.onAuthRefreshSuccess = () => this.syncUser();
    this.keycloak.onAuthLogout = () => this._user.set(null);

    try {
      await this.keycloak.init({
        // check-sso: find out quietly whether a Keycloak session exists; do NOT force a login here.
        // Forcing is the guards' job, so public routes (like /forbidden) never bounce through Keycloak.
        onLoad: 'check-sso',
        // The quiet check runs in a hidden iframe that loads this tiny page (public/silent-check-sso.html).
        silentCheckSsoRedirectUri: `${window.location.origin}/silent-check-sso.html`,
        pkceMethod: 'S256',
        checkLoginIframe: false,
      });
    } catch (error) {
      console.error('Keycloak init failed. Is Keycloak running on', this.keycloak.authServerUrl, '?', error);
    }
    this.syncUser();
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  /** Full-page redirect to the Keycloak login page; comes back to returnUrl afterwards. */
  login(returnUrl = '/'): Promise<void> {
    return this.keycloak.login({ redirectUri: window.location.origin + returnUrl });
  }

  logout(): Promise<void> {
    return this.keycloak.logout({ redirectUri: window.location.origin });
  }

  /** Access token for API calls, refreshed first if it expires within 30 seconds. */
  async getValidToken(): Promise<string | undefined> {
    if (!this.keycloak.authenticated) return undefined;
    try {
      await this.keycloak.updateToken(30);
      return this.keycloak.token;
    } catch {
      this._user.set(null);
      return undefined;
    }
  }

  private syncUser(): void {
    const t = this.keycloak.authenticated ? this.keycloak.tokenParsed : undefined;
    if (!t) {
      this._user.set(null);
      return;
    }
    const claims = t as { preferred_username?: string; name?: string; roles?: string[] };
    this._user.set({
      username: claims.preferred_username ?? '',
      name: claims.name ?? claims.preferred_username ?? '',
      // "roles" is the flat claim added by the realm-role protocol mapper on the askhr-web client.
      roles: claims.roles ?? [],
    });
  }
}
