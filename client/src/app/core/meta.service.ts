import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';

export interface Option {
  id: string;
  name: string;
}
export interface Region extends Option {
  servers: Option[];
}
export interface Rank {
  tier: number;
  name: string;
  icon: string;
}
export interface Agent {
  id: string;
  name: string;
  role: string;
  icon: string;
}
export interface Meta {
  regions: Region[];
  platforms: Option[];
  ranks: Rank[];
  roles: Option[];
  agents: Agent[];
  languages: Option[];
  mic: Option[];
  modes: Option[];
  playstyles: Option[];
  playTimes: Option[];
}

/** Game data from the server (the only source of truth). Fetched on first use, then kept for the session. */
@Injectable({ providedIn: 'root' })
export class MetaService {
  readonly meta = httpResource<Meta>(() => '/api/meta');
  /** Every server across all regions. */
  readonly servers = computed(() => this.meta.value()?.regions.flatMap((r) => r.servers) ?? []);

  rank(tier: number | null | undefined) {
    return this.meta.value()?.ranks.find((r) => r.tier === tier);
  }

  agent(id: string) {
    return this.meta.value()?.agents.find((a) => a.id === id);
  }

  /** Display name for an id, e.g. name(meta.languages, 'de') -> 'German'. Falls back to the id. */
  name(options: Option[] | undefined, id: string) {
    return options?.find((o) => o.id === id)?.name ?? id;
  }

  /** Comma-separated display names for ids. */
  names(options: Option[] | undefined, ids: string[]) {
    return ids.map((id) => this.name(options, id)).join(', ');
  }
}
