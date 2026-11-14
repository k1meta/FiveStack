import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { MetaService } from '../../core/meta.service';
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
}

/** Public profile at /u/:id. */
@Component({
  selector: 'app-profile-page',
  imports: [RankBadge, RouterLink],
  templateUrl: './profile-page.html',
})
export class ProfilePage {
  protected meta = inject(MetaService);
  protected auth = inject(AuthService);
  id = input.required<string>(); // route param

  protected profile = httpResource<PublicProfile>(() => `/api/profiles/${this.id()}`);
  protected isMe = computed(() => this.auth.user()?.id === this.id());
  protected copied = signal(false);

  protected trackerUrl(p: PublicProfile) {
    return `https://tracker.gg/valorant/profile/riot/${encodeURIComponent(`${p.riotName}#${p.riotTag}`)}/overview`;
  }

  protected async copyDiscord(name: string) {
    await navigator.clipboard.writeText(name);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 2000);
  }
}
