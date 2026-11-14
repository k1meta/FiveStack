import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

const SERVER_ERRORS: Record<string, string> = {
  'email-in-use': 'An account with that email already exists. Log in with your password, then link the provider in Settings.',
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

  protected busy = signal(false);
  protected failure = signal<string | null>(null);
  protected message = computed(() => this.failure() ?? SERVER_ERRORS[this.error() ?? ''] ?? null);

  /** Only same-site paths, so a crafted link can't bounce users off-site. */
  protected safeReturnUrl = computed(() => {
    const url = this.returnUrl();
    return url?.startsWith('/') && !url.startsWith('//') ? url : '/';
  });

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
      await (this.mode() === 'login' ? this.auth.login(email, password) : this.auth.register(email, password));
      await this.router.navigateByUrl(this.safeReturnUrl());
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

function errorMessage(e: unknown): string {
  if (!(e instanceof HttpErrorResponse)) return 'Something went wrong.';
  if (e.status === 401) return 'Wrong email or password.';
  if (e.status === 429) return 'Too many attempts. Wait a minute and try again.';
  // Identity returns ValidationProblemDetails: { errors: { DuplicateUserName: ["..."], ... } }
  const errors = e.error?.errors as Record<string, string[]> | undefined;
  return errors ? Object.values(errors).flat().join(' ') : 'Something went wrong.';
}
