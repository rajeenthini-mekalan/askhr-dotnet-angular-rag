import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { adminUser, createFakeAuth } from './testing/fake-auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: createFakeAuth(adminUser) },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('adds the bearer token to /api requests', async () => {
    const response = firstValueFrom(http.get('/api/documents'));
    // The token is fetched asynchronously (it may be refreshed first), so let that promise settle.
    await Promise.resolve();
    const req = backend.expectOne('/api/documents');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-token');
    req.flush([]);
    await response;
  });

  it('does not send the token to other URLs', async () => {
    const response = firstValueFrom(http.get('https://example.com/data'));
    const req = backend.expectOne('https://example.com/data');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
    await response;
  });
});
