import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/util';

/** What Identity's POST /api/auth/manage/2fa returns. `recoveryCodes` only right after they're generated. */
interface TwoFactorStatus {
  sharedKey: string;
  recoveryCodesLeft: number;
  recoveryCodes: string[] | null;
  isTwoFactorEnabled: boolean;
}

const button =
  'border border-white/20 px-4 py-2 text-sm font-semibold uppercase tracking-wider hover:border-val-teal disabled:opacity-50';

/** Settings section: turn TOTP two-factor authentication on (QR + code) or off, and manage recovery codes. */
@Component({
  selector: 'app-two-factor-settings',
  imports: [ReactiveFormsModule],
  template: `
    <h2 class="font-display text-2xl uppercase tracking-wide">Two-factor authentication</h2>
    @if (failure(); as f) {
      <p role="alert" class="mt-4 border-l-4 border-val-red bg-val-red/10 px-3 py-2 text-sm">
        {{ f }}
      </p>
    }
    @if (status(); as s) {
      @if (codes(); as list) {
        <div role="status" class="mt-4 border border-val-teal/40 bg-val-teal/10 p-4">
          <p class="font-semibold">Save your recovery codes</p>
          <p class="mt-1 text-sm text-val-bone/80">
            Each one signs you in once if you lose your phone. They won't be shown again.
          </p>
          <ul class="mt-3 grid grid-cols-2 gap-1 font-mono text-sm">
            @for (c of list; track c) {
              <li>{{ c }}</li>
            }
          </ul>
          <button type="button" (click)="codes.set(null)" class="mt-3 ${button}">
            I saved them
          </button>
        </div>
      }

      @if (s.isTwoFactorEnabled) {
        <p class="mt-4 text-sm text-val-bone/80">
          <span class="font-semibold text-val-teal">On.</span> Signing in asks for a code from your
          authenticator app. {{ s.recoveryCodesLeft }} recovery codes left.
        </p>
        <div class="mt-4 flex flex-wrap gap-3">
          <button type="button" (click)="newCodes()" [disabled]="busy()" class="${button}">
            New recovery codes
          </button>
          <button type="button" (click)="turnOff()" [disabled]="busy()" class="${button}">
            Turn off
          </button>
        </div>
      } @else if (qr(); as image) {
        <ol class="mt-4 list-decimal space-y-4 pl-5 text-sm text-val-bone/80">
          <li>
            Scan this with an authenticator app (Google Authenticator, 1Password, Authy…).
            <img
              [src]="image"
              alt="QR code for your authenticator app"
              class="mt-3 size-48 bg-white"
            />
            <span class="mt-2 block">
              Can't scan? Enter this key:
              <code class="font-mono text-val-bone">{{ spacedKey() }}</code>
            </span>
          </li>
          <li>
            <form
              (submit)="$event.preventDefault(); turnOn()"
              class="flex flex-wrap items-end gap-3"
            >
              <label class="block">
                <span class="field-label">Code from the app</span>
                <input
                  [formControl]="code"
                  autocomplete="one-time-code"
                  inputmode="numeric"
                  class="field w-40 tracking-widest"
                />
              </label>
              <button type="submit" [disabled]="code.invalid || busy()" class="${button}">
                Turn on
              </button>
            </form>
          </li>
        </ol>
      } @else {
        <p class="mt-4 text-sm text-val-bone/80">
          Off. Add a code from your phone to every sign-in, so a leaked password isn't enough.
        </p>
        <button type="button" (click)="setUp()" [disabled]="busy()" class="mt-4 ${button}">
          Set up
        </button>
      }
    }
  `,
})
export class TwoFactorSettings implements OnInit {
  private http = inject(HttpClient);
  private auth = inject(AuthService);

  protected status = signal<TwoFactorStatus | null>(null);
  /** The QR image (data: URL) while setting up. */
  protected qr = signal<string | null>(null);
  /** Fresh recovery codes, shown once. */
  protected codes = signal<string[] | null>(null);
  protected busy = signal(false);
  protected failure = signal<string | null>(null);
  protected code = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^\s*\d{3}\s*\d{3}\s*$/)],
  });
  protected spacedKey = computed(() =>
    this.status()
      ?.sharedKey.match(/.{1,4}/g)
      ?.join(' '),
  );

  async ngOnInit() {
    await this.run(async () => this.status.set(await this.post({})));
  }

  protected setUp() {
    return this.run(async () => {
      const account = this.auth.user()?.email ?? this.auth.user()?.id ?? 'player';
      const uri =
        `otpauth://totp/${encodeURIComponent(`FiveStack:${account}`)}` +
        `?secret=${this.status()!.sharedKey}&issuer=FiveStack&digits=6&period=30`;
      // Lazy: only players who set up 2FA download it. qrcode is CommonJS, so the bundle puts its exports on `default`.
      const qrcode = await import('qrcode');
      const { toDataURL } = ('default' in qrcode ? qrcode.default : qrcode) as typeof qrcode;
      this.qr.set(await toDataURL(uri, { margin: 1, width: 192 }));
    });
  }

  protected turnOn() {
    return this.run(async () => {
      const twoFactorCode = this.code.value.replace(/\s/g, '');
      const s = await this.post({ enable: true, twoFactorCode }).catch(() => {
        throw new Error(
          "That code didn't match. Use the newest code and check your phone's clock.",
        );
      });
      this.status.set(s);
      this.codes.set(s.recoveryCodes);
      this.qr.set(null);
      this.code.reset();
    });
  }

  protected turnOff() {
    if (!confirm('Turn off two-factor authentication? Your password alone will sign you in.'))
      return;
    // A new shared key too, so the old authenticator entry stops working.
    return this.run(async () => {
      this.status.set(await this.post({ enable: false, resetSharedKey: true }));
      this.codes.set(null);
    });
  }

  protected newCodes() {
    return this.run(async () => {
      const s = await this.post({ resetRecoveryCodes: true });
      this.status.set(s);
      this.codes.set(s.recoveryCodes);
    });
  }

  private post(body: object) {
    return firstValueFrom(this.http.post<TwoFactorStatus>('/api/auth/manage/2fa', body));
  }

  private async run(action: () => Promise<unknown>) {
    this.busy.set(true);
    this.failure.set(null);
    try {
      await action();
    } catch (e) {
      this.failure.set(e instanceof Error ? e.message : errorMessage(e)); // HttpErrorResponse isn't an Error
    } finally {
      this.busy.set(false);
    }
  }
}
