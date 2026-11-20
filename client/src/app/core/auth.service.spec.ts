import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService.register', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  const respondToSignup = async (
    login: (req: ReturnType<HttpTestingController['expectOne']>) => void,
  ) => {
    http.expectOne('/api/auth/register').flush(null);
    await vi.waitFor(() => login(http.expectOne('/api/auth/login?useCookies=true')));
  };

  it('asks for the emailed link when the server refuses an unconfirmed login', async () => {
    const result = auth.register('ana@example.com', 'Passw0rd!');
    await respondToSignup((req) =>
      req.flush({ detail: 'NotAllowed' }, { status: 401, statusText: 'Unauthorized' }),
    );
    expect(await result).toBe('confirm-email');
    expect(auth.user()).toBeNull();
  });

  it('signs straight in when confirmation is off', async () => {
    const result = auth.register('ana@example.com', 'Passw0rd!');
    await respondToSignup((req) => req.flush(null));
    await vi.waitFor(() =>
      http.expectOne('/api/auth/me').flush({ id: '1', email: 'ana@example.com', onboarded: false }),
    );
    expect(await result).toBe('signed-in');
    expect(auth.user()?.email).toBe('ana@example.com');
  });
});
