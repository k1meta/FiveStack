import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgTemplateOutlet } from '@angular/common';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { Meta, MetaService } from '../../core/meta.service';
import { RankBadge } from '../../shared/rank-badge';
import { PublicProfile } from '../profile/profile-page';

export interface PlayerPage {
  items: PublicProfile[];
  total: number;
  page: number;
  pageSize: number;
}

/** Player finder at /players. The URL query string is the only filter state, so searches are shareable. */
@Component({
  selector: 'app-players-page',
  imports: [RankBadge, RouterLink, NgTemplateOutlet],
  templateUrl: './players-page.html',
})
export class PlayersPage {
  private router = inject(Router);
  protected meta = inject(MetaService);

  private query = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  // Query params pass straight through: GET /api/players uses the same names.
  protected results = httpResource<PlayerPage>(() => {
    const q = this.query();
    return { url: '/api/players', params: Object.fromEntries(q.keys.map((k) => [k, q.getAll(k)])) };
  });

  protected filtersOpen = signal(false); // mobile only; the sidebar is always shown from md up
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
    for (const [k, v] of Object.entries(changes)) queryParams[k] = v === '' || v?.length === 0 ? null : v;
    this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }

  protected pick(key: string, value: string) {
    this.set({ [key]: value });
  }

  protected goTo(page: number) {
    this.router.navigate([], { queryParams: { page: page > 1 ? page : null }, queryParamsHandling: 'merge' });
  }

  protected regionServers(m: Meta) {
    return m.regions.find((r) => r.id === this.get('region'))?.servers ?? [];
  }

  protected agentsFor(m: Meta, role: string) {
    return m.agents.filter((a) => a.role === role);
  }
}
