import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { AuthService } from './auth/auth.service';
import { adminUser, createFakeAuth, employeeUser } from './auth/testing/fake-auth.service';

describe('App', () => {
  async function render(user: Parameters<typeof createFakeAuth>[0]) {
    const auth = createFakeAuth(user);
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return { fixture, auth, el: fixture.nativeElement as HTMLElement };
  }

  it('shows the Documents link to hr-admin', async () => {
    const { el } = await render(adminUser);
    expect(el.querySelector('[data-testid="nav-documents"]')).not.toBeNull();
    expect(el.textContent).toContain('admin@askhr.demo');
  });

  it('hides the Documents link from employee', async () => {
    const { el } = await render(employeeUser);
    expect(el.querySelector('[data-testid="nav-documents"]')).toBeNull();
  });

  it('updates the nav when the auth signal changes (zoneless)', async () => {
    const { fixture, auth, el } = await render(employeeUser);
    auth.setUser(adminUser);
    await fixture.whenStable(); // no zone.js: the signal change itself schedules the re-render
    expect(el.querySelector('[data-testid="nav-documents"]')).not.toBeNull();
  });
});
