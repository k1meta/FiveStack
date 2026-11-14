import { HttpClient } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/util';
import { ProfileEditor } from '../profile/profile-editor';

@Component({
  selector: 'app-settings-page',
  imports: [ProfileEditor],
  templateUrl: './settings-page.html',
})
export class SettingsPage {
  private http = inject(HttpClient);
  private router = inject(Router);
  protected auth = inject(AuthService);

  error = input<string>(); // ?error=link-failed from the OAuth callback

  protected confirmText = signal('');
  protected deleting = signal(false);
  protected deleteFailure = signal<string | null>(null);

  protected linkUrl(provider: string) {
    return `/api/auth/external/${provider}?returnUrl=${encodeURIComponent('/settings')}`;
  }

  protected async deleteAccount() {
    this.deleting.set(true);
    this.deleteFailure.set(null);
    try {
      await firstValueFrom(this.http.delete('/api/me'));
      this.auth.user.set(null);
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.deleteFailure.set(errorMessage(e));
    } finally {
      this.deleting.set(false);
    }
  }
}
