import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/util';

// Pages the account emails link to. The emails carry the userId/code (or email/code) as query params,
// bound here with withComponentInputBinding.

const page = 'mx-auto block max-w-sm px-4 py-16';
const field = 'mt-1 w-full border border-white/20 bg-val-ink px-3 py-2 focus:border-val-teal';
const button =
  'clip-angle w-full bg-val-red py-3 font-semibold uppercase tracking-wider hover:brightness-110 disabled:opacity-50';

/** /confirm-email?userId&code[&changedEmail]: from the confirmation email. */
@Component({
  selector: 'app-confirm-email-page',
  imports: [RouterLink],
  host: { class: page },
  template: `
    <h1 class="font-display text-4xl uppercase tracking-wide">Confirm email</h1>
    @switch (state()) {
      @case ('working') {
        <p class="mt-4 text-val-bone/80" role="status">Confirming…</p>
      }
      @case ('done') {
        <p class="mt-4 text-val-bone/80" role="status">Your email is confirmed.</p>
        @if (auth.user()) {
          <a routerLink="/" class="mt-6 inline-block text-val-teal hover:underline">Continue</a>
        } @else {
          <a routerLink="/login" class="mt-6 inline-block text-val-teal hover:underline">Log in</a>
        }
      }
      @case ('failed') {
        <p role="alert" class="mt-4 border-l-4 border-val-red bg-val-red/10 px-3 py-2 text-sm">
          This link is invalid or has expired. Log in to get a new one.
        </p>
        <a routerLink="/login" class="mt-6 inline-block text-val-teal hover:underline">Log in</a>
      }
    }
  `,
})
export class ConfirmEmailPage implements OnInit {
  private http = inject(HttpClient);
  protected auth = inject(AuthService);

  userId = input<string>();
  code = input<string>();
  changedEmail = input<string>();

  protected state = signal<'working' | 'done' | 'failed'>('working');

  async ngOnInit() {
    const [userId, code] = [this.userId(), this.code()];
    if (!userId || !code) return this.state.set('failed');
    const params: Record<string, string> = { userId, code };
    if (this.changedEmail()) params['changedEmail'] = this.changedEmail()!;
    try {
      await firstValueFrom(
        this.http.get('/api/auth/confirmEmail', { params, responseType: 'text' }),
      );
      this.state.set('done');
      if (this.auth.user()) await this.auth.load(); // a changed email shows up in the header
    } catch {
      this.state.set('failed');
    }
  }
}

/** /forgot-password: asks for a reset link. The answer is the same whether or not the account exists. */
@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  host: { class: page },
  template: `
    <h1 class="font-display text-4xl uppercase tracking-wide">Reset password</h1>
    @if (sentTo(); as email) {
      <p role="status" class="mt-6 border border-val-teal/40 bg-val-teal/10 p-4 text-sm">
        If an account uses <span class="font-semibold">{{ email }}</span>, we've sent it a link to
        choose a new password. Check spam too.
      </p>
      <a routerLink="/login" class="mt-6 inline-block text-val-teal hover:underline"
        >Back to log in</a
      >
    } @else {
      <p class="mt-2 text-sm text-val-bone/70">We'll email you a link to choose a new password.</p>
      @if (failure(); as f) {
        <p role="alert" class="mt-4 border-l-4 border-val-red bg-val-red/10 px-3 py-2 text-sm">
          {{ f }}
        </p>
      }
      <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 space-y-4">
        <label class="block">
          <span class="text-sm font-semibold uppercase tracking-wider">Email</span>
          <input type="email" formControlName="email" autocomplete="email" class="${field}" />
        </label>
        <button type="submit" [disabled]="form.invalid || busy()" class="${button}">
          Send reset link
        </button>
      </form>
    }
  `,
})
export class ForgotPasswordPage {
  private http = inject(HttpClient);

  protected form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });
  protected busy = signal(false);
  protected failure = signal<string | null>(null);
  protected sentTo = signal<string | null>(null);

  protected async submit() {
    const { email } = this.form.getRawValue();
    this.busy.set(true);
    this.failure.set(null);
    try {
      await firstValueFrom(this.http.post('/api/auth/forgotPassword', { email }));
      this.sentTo.set(email);
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

/** /reset-password?email&code: from the reset email. */
@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  host: { class: page },
  template: `
    <h1 class="font-display text-4xl uppercase tracking-wide">New password</h1>
    @if (done()) {
      <p role="status" class="mt-4 text-val-bone/80">Your password is changed.</p>
      <a routerLink="/login" class="mt-6 inline-block text-val-teal hover:underline">Log in</a>
    } @else if (!email() || !code()) {
      <p role="alert" class="mt-4 border-l-4 border-val-red bg-val-red/10 px-3 py-2 text-sm">
        This link is incomplete. Open it again from the email, or ask for a new one.
      </p>
      <a routerLink="/forgot-password" class="mt-6 inline-block text-val-teal hover:underline"
        >Get a new link</a
      >
    } @else {
      <p class="mt-2 text-sm text-val-bone/70">For {{ email() }}</p>
      @if (failure(); as f) {
        <p role="alert" class="mt-4 border-l-4 border-val-red bg-val-red/10 px-3 py-2 text-sm">
          {{ f }}
          @if (expired()) {
            <a routerLink="/forgot-password" class="text-val-teal underline">Get a new link</a>
          }
        </p>
      }
      <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 space-y-4">
        <label class="block">
          <span class="text-sm font-semibold uppercase tracking-wider">New password</span>
          <input
            type="password"
            formControlName="password"
            autocomplete="new-password"
            class="${field}"
          />
          <span class="mt-1 block text-xs text-val-bone/60"
            >6+ characters with an uppercase letter, a lowercase letter, a digit and a symbol.</span
          >
        </label>
        <button type="submit" [disabled]="form.invalid || busy()" class="${button}">
          Save password
        </button>
      </form>
    }
  `,
})
export class ResetPasswordPage {
  private http = inject(HttpClient);

  email = input<string>();
  code = input<string>();

  protected form = inject(FormBuilder).nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(6)]],
  });
  protected busy = signal(false);
  protected failure = signal<string | null>(null);
  protected expired = signal(false);
  protected done = signal(false);

  protected async submit() {
    this.busy.set(true);
    this.failure.set(null);
    try {
      await firstValueFrom(
        this.http.post('/api/auth/resetPassword', {
          email: this.email(),
          resetCode: this.code(),
          newPassword: this.form.getRawValue().password,
        }),
      );
      this.done.set(true);
    } catch (e) {
      // Identity reports a used, expired or tampered code as "InvalidToken".
      const invalid = (e as { error?: { errors?: object } }).error?.errors;
      this.expired.set(!!invalid && 'InvalidToken' in invalid);
      this.failure.set(
        this.expired() ? 'This link has expired or was already used.' : errorMessage(e),
      );
    } finally {
      this.busy.set(false);
    }
  }
}
