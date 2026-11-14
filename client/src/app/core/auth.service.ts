import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface Me {
  id: string;
  email: string | null;
  displayName: string | null;
  discordUsername: string | null;
}

/** Session state. The auth cookie is HttpOnly, so `/api/auth/me` is the source of truth. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  readonly user = signal<Me | null>(null);
  /** External providers the server has configured, e.g. ['Google', 'Discord']. */
  readonly providers = signal<string[]>([]);

  async load() {
    const [me, providers] = await Promise.all([
      firstValueFrom(this.http.get<Me>('/api/auth/me')).catch(() => null), // 401 = signed out
      firstValueFrom(this.http.get<string[]>('/api/auth/providers')).catch(() => []),
    ]);
    this.user.set(me);
    this.providers.set(providers);
  }

  async login(email: string, password: string) {
    await firstValueFrom(this.http.post('/api/auth/login?useCookies=true', { email, password }));
    this.user.set(await firstValueFrom(this.http.get<Me>('/api/auth/me')));
  }

  async register(email: string, password: string) {
    await firstValueFrom(this.http.post('/api/auth/register', { email, password }));
    await this.login(email, password);
  }

  async logout() {
    await firstValueFrom(this.http.post('/api/auth/logout', null));
    this.user.set(null);
  }
}
