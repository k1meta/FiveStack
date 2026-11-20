import { Component, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { safeReturnUrl } from '../../core/util';
import { ProfileEditor } from './profile-editor';

@Component({
  selector: 'app-onboarding-page',
  imports: [ProfileEditor],
  template: `
    <section class="mx-auto max-w-3xl px-4 py-12">
      <h1 class="font-display text-4xl uppercase tracking-wide">Build your profile</h1>
      <p class="mt-2 text-val-bone/70">
        So the right teammates can find you. You can change all of it later in Settings.
      </p>
      <app-profile-editor wizard class="mt-8 block" (saved)="done()" />
    </section>
  `,
})
export class OnboardingPage {
  private auth = inject(AuthService);
  private router = inject(Router);
  returnUrl = input<string>(); // query param

  protected done() {
    const url = safeReturnUrl(this.returnUrl());
    this.router.navigateByUrl(url === '/' ? `/u/${this.auth.user()?.id}` : url);
  }
}
