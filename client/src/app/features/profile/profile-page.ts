import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { ChatService } from '../../core/chat.service';
import { MetaService } from '../../core/meta.service';
import { errorMessage } from '../../core/util';
import { RankBadge } from '../../shared/rank-badge';

export interface PublicProfile {
  id: string;
  displayName: string | null;
  riotName: string;
  riotTag: string;
  region: string;
  servers: string[];
  platform: string | null;
  rank: number | null;
  peakRank: number | null;
  roles: string[];
  agents: string[];
  languages: string[];
  mic: string | null;
  modes: string[];
  playstyle: string | null;
  playTimes: string[];
  bio: string | null;
  discordUsername: string | null;
  online: boolean;
  isDemo: boolean;
}

/** Public profile at /u/:id. */
@Component({
  selector: 'app-profile-page',
  imports: [RankBadge, RouterLink],
  templateUrl: './profile-page.html',
})
export class ProfilePage {
  private http = inject(HttpClient);
  private router = inject(Router);
  private chat = inject(ChatService);
  protected meta = inject(MetaService);
  protected auth = inject(AuthService);
  id = input.required<string>(); // route param

  protected profile = httpResource<PublicProfile>(() => `/api/profiles/${this.id()}`);
  protected isMe = computed(() => this.auth.user()?.id === this.id());
  protected copied = signal(false);
  /** Ids I blocked; only loaded when signed in. */
  private blocks = httpResource<string[]>(() => (this.auth.user() ? '/api/blocks' : undefined));
  protected blocked = computed(() => this.blocks.value()?.includes(this.id()) ?? false);
  protected busy = signal(false);
  protected failure = signal<string | null>(null);

  protected trackerUrl(p: PublicProfile) {
    return `https://tracker.gg/valorant/profile/riot/${encodeURIComponent(`${p.riotName}#${p.riotTag}`)}/overview`;
  }

  protected async message() {
    await this.act(async () => this.router.navigate(['/messages', await this.chat.startDm(this.id())]));
  }

  protected async toggleBlock(p: PublicProfile) {
    if (this.blocked()) {
      await this.act(() => firstValueFrom(this.http.delete(`/api/blocks/${p.id}`)));
    } else if (confirm(`Block ${p.riotName}? Neither of you will be able to message the other.`)) {
      await this.act(() => firstValueFrom(this.http.put(`/api/blocks/${p.id}`, null)));
    }
    this.blocks.reload();
  }

  private async act(call: () => Promise<unknown>) {
    this.busy.set(true);
    this.failure.set(null);
    try {
      await call();
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async copyDiscord(name: string) {
    await navigator.clipboard.writeText(name);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 2000);
  }
}
