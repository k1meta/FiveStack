import { HttpClient, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgTemplateOutlet } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { ChatService } from '../../core/chat.service';
import { MetaService } from '../../core/meta.service';
import { errorMessage } from '../../core/util';
import { RankBadge } from '../../shared/rank-badge';
import { PublicProfile } from '../profile/profile-page';
import { Squad, rankRange, timeLeft } from './squads-page';

export interface SquadDetail {
  post: Squad;
  roster: PublicProfile[];
  /** Pending requests; only filled in for the host. */
  requests: { player: PublicProfile; createdAt: string }[];
  /** The viewer's own request: 'pending' | 'accepted' | 'declined', or null. */
  myStatus: string | null;
  /** The squad chat; only for the host and accepted players. */
  conversationId: string | null;
}

/** One squad at /squads/:id: details, roster, join/cancel, and the host's request queue. */
@Component({
  selector: 'app-squad-page',
  imports: [RankBadge, RouterLink, NgTemplateOutlet],
  templateUrl: './squad-page.html',
})
export class SquadPage {
  private http = inject(HttpClient);
  private router = inject(Router);
  protected meta = inject(MetaService);
  protected auth = inject(AuthService);
  protected timeLeft = timeLeft;
  id = input.required<string>(); // route param

  protected squad = httpResource<SquadDetail>(() => `/api/squads/${this.id()}`);
  protected isHost = computed(() => this.squad.value()?.post.host.id === this.auth.user()?.id);
  protected busy = signal(false);
  protected failure = signal<string | null>(null);
  protected copied = signal<string | null>(null);

  constructor() {
    // The hub pushes SquadChanged to the host on a new/cancelled request and to a player when the host answers.
    inject(ChatService)
      .squadChanged.pipe(takeUntilDestroyed())
      .subscribe((postId) => {
        if (postId === this.id()) this.squad.reload();
      });
  }

  protected join() {
    return this.act(this.http.post(`/api/squads/${this.id()}/requests`, null));
  }

  protected cancel() {
    return this.act(this.http.delete(`/api/squads/${this.id()}/requests/me`));
  }

  protected decide(userId: string, status: 'accepted' | 'declined') {
    return this.act(this.http.put(`/api/squads/${this.id()}/requests/${userId}`, { status }));
  }

  protected async close() {
    if (!confirm('Close this squad? It leaves the board for good.')) return;
    await this.act(this.http.delete(`/api/squads/${this.id()}`));
  }

  protected rankRange(s: Squad) {
    return rankRange(this.meta, s);
  }

  protected async copyDiscord(name: string) {
    await navigator.clipboard.writeText(name);
    this.copied.set(name);
    setTimeout(() => this.copied.set(null), 2000);
  }

  protected loginUrl() {
    return this.router.createUrlTree(['/login'], {
      queryParams: { returnUrl: `/squads/${this.id()}` },
    });
  }

  private async act(call: Observable<unknown>) {
    this.busy.set(true);
    this.failure.set(null);
    try {
      await firstValueFrom(call);
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.busy.set(false);
      this.squad.reload();
    }
  }
}
