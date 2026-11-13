import { TestBed } from '@angular/core/testing';
import type Keycloak from 'keycloak-js';
import { vi } from 'vitest';
import { KEYCLOAK } from './auth.config';
import { AuthService } from './auth.service';

function fakeKeycloak(authenticated: boolean, roles: string[] = []) {
  return {
    authenticated,
    token: authenticated ? 'access-token' : undefined,
    tokenParsed: authenticated ? { preferred_username: 'admin@askhr.demo', name: 'Hana Admin', roles } : undefined,
    init: vi.fn().mockResolvedValue(authenticated),
    updateToken: vi.fn().mockResolvedValue(false),
    login: vi.fn().mockResolvedValue(undefined),
    logout: vi.fn().mockResolvedValue(undefined),
  };
}

describe('AuthService', () => {
  function setup(kc: ReturnType<typeof fakeKeycloak>) {
    TestBed.configureTestingModule({ providers: [{ provide: KEYCLOAK, useValue: kc as unknown as Keycloak }] });
    return TestBed.inject(AuthService);
  }

  it('is signed out when Keycloak has no session', async () => {
    const auth = setup(fakeKeycloak(false));
    await auth.init();
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.roles()).toEqual([]);
    expect(await auth.getValidToken()).toBeUndefined();
  });

  it('exposes the user and roles from the access token as signals', async () => {
    const kc = fakeKeycloak(true, ['hr-admin', 'default-roles-askhr']);
    const auth = setup(kc);
    await auth.init();

    expect(kc.init).toHaveBeenCalledWith(expect.objectContaining({ onLoad: 'check-sso', pkceMethod: 'S256' }));
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.user()?.username).toBe('admin@askhr.demo');
    expect(auth.hasRole('hr-admin')).toBe(true);
    expect(auth.hasRole('employee')).toBe(false);
  });

  it('refreshes before handing out a token', async () => {
    const kc = fakeKeycloak(true, ['employee']);
    const auth = setup(kc);
    await auth.init();

    expect(await auth.getValidToken()).toBe('access-token');
    expect(kc.updateToken).toHaveBeenCalledWith(30);
  });

  it('signs the user out locally when the refresh fails', async () => {
    const kc = fakeKeycloak(true, ['employee']);
    const auth = setup(kc);
    await auth.init();
    kc.updateToken.mockRejectedValueOnce(new Error('session ended'));

    expect(await auth.getValidToken()).toBeUndefined();
    expect(auth.isAuthenticated()).toBe(false);
  });
});
