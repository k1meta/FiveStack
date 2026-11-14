import { Component, computed, inject, input } from '@angular/core';
import { MetaService } from '../core/meta.service';

/** Rank icon + name for a valorant-api competitive tier number. */
@Component({
  selector: 'app-rank-badge',
  template: `
    @if (rank(); as r) {
      <span class="inline-flex items-center gap-2">
        <img [src]="r.icon" alt="" class="size-8" />
        <span class="font-semibold">{{ r.name }}</span>
      </span>
    }
  `,
})
export class RankBadge {
  private meta = inject(MetaService);
  tier = input<number | null>();
  protected rank = computed(() => this.meta.rank(this.tier()));
}
