import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage, problemDetail, safeReturnUrl } from '../../core/util';

const SERVER_ERRORS: Record<string, string> = {
  'email-in-use':
    'An account with that email already exists. Log in with your password, then link the provider in Settings.',
  'external-failed': 'Sign-in with that provider failed. Please try again.',
  'not-allowed': 'This account is locked.',
};

/** Login and register share one page; the route's `data.mode` picks which. */
@Component({
  selector: 'app-auth-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './auth-page.html',
})
export class AuthPage {
  protected auth = inject(AuthService);
  private router = inject(Router);

  // Bound from route data and query params (withComponentInputBinding).
  mode = input<'login' | 'register'>('login');
  returnUrl = input<string>();
  error = input<string>();
  /** `2fa` when a Google/Discord sign-in still needs the authenticator code. */
  step = input<string>();

  protected busy = signal(false);
  protected failure = signal<string | null>(null);
  /** The address waiting for its confirmation link: after sign-up, or a login before confirming. */
  protected confirmFor = signal<string | null>(null);
  protected resent = signal(false);
  /** Which second step is pending: after a password, or after Google/Discord. */
  protected twoFactor = linkedSignal<'password' | 'external' | null>(() =>
    this.step() === '2fa' ? 'external' : null,
  );
  protected useRecovery = signal(false);
  protected code = new FormControl('', { nonNullable: true, validators: Validators.required });
  protected message = computed(() => this.failure() ?? SERVER_ERRORS[this.error() ?? ''] ?? null);

  protected safeReturnUrl = computed(() => safeReturnUrl(this.returnUrl()));

  protected form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
  });

  protected externalUrl(provider: string) {
    return `/api/auth/external/${provider}?returnUrl=${encodeURIComponent(this.safeReturnUrl())}`;
  }

  protected async submit() {
    const { email, password } = this.form.getRawValue();
    this.busy.set(true);
    this.failure.set(null);
    try {
      if (this.mode() === 'login') await this.auth.login(email, password);
      else if ((await this.auth.register(email, password)) === 'confirm-email') {
        this.confirmFor.set(email);
        return;
      }
      await this.signedIn();
    } catch (e) {
      const detail = problemDetail(e);
      if (detail === 'NotAllowed') this.confirmFor.set(email);
      else if (detail === 'RequiresTwoFactor') this.twoFactor.set('password');
      else
        this.failure.set(
          detail === 'LockedOut'
            ? 'Too many failed attempts. Try again in 5 minutes.'
            : e instanceof HttpErrorResponse && e.status === 401
              ? 'Wrong email or password.'
              : errorMessage(e),
        );
    } finally {
      this.busy.set(false);
    }
  }

  protected async submitCode() {
    const value = this.code.value.replace(/\s/g, '');
    this.busy.set(true);
    this.failure.set(null);
    try {
      if (this.twoFactor() === 'external')
        await this.auth.finishExternalSignIn(
          this.useRecovery() ? { recoveryCode: value } : { code: value },
        );
      else {
        const { email, password } = this.form.getRawValue();
        await this.auth.login(
          email,
          password,
          this.useRecovery() ? { twoFactorRecoveryCode: value } : { twoFactorCode: value },
        );
      }
      await this.signedIn();
    } catch (e) {
      this.failure.set(
        problemDetail(e) === 'LockedOut'
          ? 'Too many failed attempts. Try again in 5 minutes.'
          : e instanceof HttpErrorResponse && e.status === 401
            ? "That code didn't work. Authenticator codes change every 30 seconds."
            : errorMessage(e),
      );
    } finally {
      this.busy.set(false);
    }
  }

  private async signedIn() {
    if (this.auth.user()?.onboarded) await this.router.navigateByUrl(this.safeReturnUrl());
    else
      await this.router.navigate(['/onboarding'], {
        queryParams: { returnUrl: this.safeReturnUrl() },
      });
  }

  protected async resend() {
    this.busy.set(true);
    this.failure.set(null);
    try {
      await this.auth.resendConfirmation(this.confirmFor()!);
      this.resent.set(true);
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
