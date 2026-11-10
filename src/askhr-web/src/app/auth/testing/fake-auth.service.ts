import { computed, signal } from '@angular/core';
import { vi } from 'vitest';
import { AuthService, AuthUser } from '../auth.service';

/** Test double with the same signal surface as AuthService. */
export function createFakeAuth(user: AuthUser | null = null) {
  const current = signal<AuthUser | null>(user);
  const roles = computed(() => current()?.roles ?? []);
  const fake = {
    user: current.asReadonly(),
    isAuthenticated: computed(() => current() !== null),
    roles,
    hasRole: (role: string) => roles().includes(role),
    login: vi.fn().mockResolvedValue(undefined),
    logout: vi.fn().mockResolvedValue(undefined),
    getValidToken: vi.fn().mockResolvedValue(user ? 'test-token' : undefined),
    init: vi.fn().mockResolvedValue(undefined),
    setUser: (u: AuthUser | null) => current.set(u),
  };
  return fake as typeof fake & AuthService;
}

export const adminUser: AuthUser = { username: 'admin@askhr.demo', name: 'Hana Admin', roles: ['hr-admin'] };
export const employeeUser: AuthUser = { username: 'employee@askhr.demo', name: 'Eli Employee', roles: ['employee'] };
