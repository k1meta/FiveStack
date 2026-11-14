import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { LandingPage } from './landing-page';

describe('LandingPage', () => {
  async function ctaHrefs(signedIn: boolean) {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    if (signedIn) {
      TestBed.inject(AuthService).user.set({ id: '1', email: 'a@b.c', displayName: 'A', discordUsername: null, onboarded: true, logins: [] });
    }
    const fixture = TestBed.createComponent(LandingPage);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/meta').flush({ roles: [], agents: [] });
    await fixture.whenStable();
    const hero = (fixture.nativeElement as HTMLElement).querySelector('section')!;
    return [...hero.querySelectorAll('a')].map((a) => a.getAttribute('href'));
  }

  it('invites signed-out visitors to sign up', async () => {
    expect(await ctaHrefs(false)).toEqual(['/register', '/squads']);
  });

  it('sends signed-in players to the finder', async () => {
    expect(await ctaHrefs(true)).toEqual(['/players', '/squads']);
  });
});
