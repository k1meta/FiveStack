import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  const run = () =>
    TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url: '/onboarding' } as RouterStateSnapshot),
    );

  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }),
  );

  it('redirects signed-out users to /login with a returnUrl', () => {
    const tree = run() as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(tree)).toBe('/login?returnUrl=%2Fonboarding');
  });

  it('lets signed-in users through', () => {
    TestBed.inject(AuthService).user.set({
      id: '1',
      email: 'a@b.c',
      displayName: null,
      discordUsername: null,
      onboarded: false,
      logins: [],
    });
    expect(run()).toBe(true);
  });
});
