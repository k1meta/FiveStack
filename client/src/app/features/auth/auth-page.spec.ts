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

  it('on sign-up, needs the password twice and can reveal both fields', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(AuthPage);
    fixture.componentRef.setInput('mode', 'register');
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const type = async (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    const submit = el.querySelector<HTMLButtonElement>('button[type=submit]')!;
    const passwords = () => [
      ...el.querySelectorAll<HTMLInputElement>('input[autocomplete=new-password]'),
    ];

    await type('input[type=email]', 'ana@example.com');
    await type('#password', 'Passw0rd!');
    await type('[formcontrolname=confirm]', 'Passw0rd?');
    expect(submit.disabled).toBe(true);
    expect(el.textContent).toContain("Passwords don't match.");

    await type('[formcontrolname=confirm]', 'Passw0rd!');
    expect(submit.disabled).toBe(false);

    expect(passwords().map((i) => i.type)).toEqual(['password', 'password']);
    el.querySelector<HTMLButtonElement>('button[aria-label="Show password"]')!.click();
    await fixture.whenStable();
    expect(passwords().map((i) => i.type)).toEqual(['text', 'text']);
  });
});
