import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { authGuard, roleGuard } from './auth.guards';
import { AuthService } from './auth.service';
import { adminUser, createFakeAuth, employeeUser } from './testing/fake-auth.service';

describe('auth guards', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/documents' } as RouterStateSnapshot;

  function setup(user: Parameters<typeof createFakeAuth>[0]) {
    const auth = createFakeAuth(user);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    // Functional guards call inject(), so run them inside an injection context.
    const run = (guard: typeof authGuard) =>
      runInInjectionContext(TestBed.inject(EnvironmentInjector), () => guard(route, state));
    return { auth, run };
  }

  it('authGuard starts login and blocks when signed out', () => {
    const { auth, run } = setup(null);
    expect(run(authGuard)).toBe(false);
    expect(auth.login).toHaveBeenCalledWith('/documents');
  });

  it('authGuard allows a signed-in user', () => {
    const { run } = setup(employeeUser);
    expect(run(authGuard)).toBe(true);
  });

  it('roleGuard allows hr-admin', () => {
    const { run } = setup(adminUser);
    expect(run(roleGuard('hr-admin'))).toBe(true);
  });

  it('roleGuard redirects employee to /forbidden', () => {
    const { run } = setup(employeeUser);
    const result = run(roleGuard('hr-admin'));
    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/forbidden');
  });

  it('roleGuard starts login when signed out', () => {
    const { auth, run } = setup(null);
    expect(run(roleGuard('hr-admin'))).toBe(false);
    expect(auth.login).toHaveBeenCalled();
  });
});
