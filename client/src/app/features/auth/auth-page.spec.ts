import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthPage } from './auth-page';

describe('AuthPage', () => {
  it('asks for the authenticator code after the password, then sends both', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(AuthPage);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const type = async (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    const submit = () => el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();

    await type('input[type=email]', 'ana@example.com');
    await type('input[type=password]', 'Passw0rd!');
    submit();
    http
      .expectOne('/api/auth/login?useCookies=true')
      .flush({ detail: 'RequiresTwoFactor' }, { status: 401, statusText: 'Unauthorized' });
    await vi.waitFor(async () => {
      await fixture.whenStable();
      expect(el.textContent).toContain('6-digit code from your authenticator app');
    });

    await type('input[autocomplete=one-time-code]', '123 456');
    submit();
    const login = http.expectOne('/api/auth/login?useCookies=true');
    expect(login.request.body).toEqual({
      email: 'ana@example.com',
      password: 'Passw0rd!',
      twoFactorCode: '123456',
    });
  });
});
