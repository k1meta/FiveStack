import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { Meta, MetaService } from '../../core/meta.service';
import { RankBadge } from '../../shared/rank-badge';
import { PublicProfile } from '../profile/profile-page';

export interface Squad {
  id: string;
  title: string;
  mode: string;
  region: string;
  server: string | null;
  rankMin: number | null;
  rankMax: number | null;
  neededRoles: string[];
  neededAgents: string[];
  slotsOpen: number;
  micRequired: boolean;
  createdAt: string;
  expiresAt: string;
  open: boolean;
  host: PublicProfile;
}

export interface SquadPage {
  items: Squad[];
  total: number;
  page: number;
  pageSize: number;
}

/** "2h 14m left" until a post expires. */
export function timeLeft(expiresAt: string) {
  const min = Math.max(0, Math.round((Date.parse(expiresAt) - Date.now()) / 60_000));
  return min >= 60 ? `${Math.floor(min / 60)}h ${min % 60}m left` : `${min}m left`;
}

/** "Gold 1 – Diamond 3", "Gold 1+", "up to Diamond 3" or "Any rank". */
export function rankRange(meta: MetaService, s: Squad) {
  const min = meta.rank(s.rankMin)?.name;
  const max = meta.rank(s.rankMax)?.name;
  if (min && max) return min === max ? min : `${min} – ${max}`;
  return min ? `${min}+` : max ? `up to ${max}` : 'Any rank';
}

/** Squad board at /squads. Like /players, the URL query string is the only filter state. */
@Component({
  selector: 'app-squads-page',
  imports: [RankBadge, RouterLink],
  templateUrl: './squads-page.html',
})
export class SquadsPage {
  private router = inject(Router);
  protected meta = inject(MetaService);
  protected auth = inject(AuthService);
  protected timeLeft = timeLeft;

  private query = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  // Query params pass straight through: GET /api/squads uses the same names.
  protected results = httpResource<SquadPage>(() => {
    const q = this.query();
    return { url: '/api/squads', params: Object.fromEntries(q.keys.map((k) => [k, q.getAll(k)])) };
  });
  /** The signed-in player's open post (204 = none). */
  protected mine = httpResource<Squad | null>(() =>
    this.auth.user() ? '/api/squads/mine' : undefined,
  );

  protected filtered = computed(() => this.query().keys.some((k) => k !== 'page'));
  protected page = computed(() => this.results.value()?.page ?? 1);
  protected pages = computed(() => {
    const r = this.results.value();
    return r ? Math.max(1, Math.ceil(r.total / r.pageSize)) : 1;
  });

  protected get(key: string) {
    return this.query().get(key);
  }

  protected has(key: string, id: string) {
    return this.query().getAll(key).includes(id);
  }

  protected toggle(key: string, id: string) {
    const ids = this.query().getAll(key);
    this.set({ [key]: this.has(key, id) ? ids.filter((v) => v !== id) : [...ids, id] });
  }

  /** Changes filters and goes back to page 1. Empty values drop the param. */
  protected set(changes: Params) {
    const queryParams: Params = { page: null };
    for (const [k, v] of Object.entries(changes))
      queryParams[k] = v === '' || v?.length === 0 ? null : v;
    this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }

  protected goTo(page: number) {
    this.router.navigate([], {
      queryParams: { page: page > 1 ? page : null },
      queryParamsHandling: 'merge',
    });
  }

  protected regionServers(m: Meta) {
    return m.regions.find((r) => r.id === this.get('region'))?.servers ?? [];
  }

  protected rankRange(s: Squad) {
    return rankRange(this.meta, s);
  }
}
