import { NgTemplateOutlet } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, booleanAttribute, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { Meta, MetaService } from '../../core/meta.service';
import { errorMessage } from '../../core/util';

export interface Profile {
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
  showDiscord: boolean;
}

type Multi = 'servers' | 'roles' | 'agents' | 'languages' | 'modes' | 'playTimes';
type Single = 'platform' | 'mic' | 'playstyle';

/** Wizard steps; in settings (non-wizard) every step shows at once. */
const STEPS = [
  { title: 'Riot ID', controls: ['riotName', 'riotTag', 'region', 'servers', 'platform'] },
  { title: 'Rank & roles', controls: ['rank', 'peakRank', 'roles', 'agents'] },
  { title: 'How you play', controls: ['languages', 'mic', 'modes', 'playstyle', 'playTimes'] },
  { title: 'About you', controls: ['bio', 'showDiscord'] },
] as const;

/** The whole profile form. Limits mirror ProfileDto on the server, which re-checks everything. */
@Component({
  selector: 'app-profile-editor',
  imports: [ReactiveFormsModule, NgTemplateOutlet],
  templateUrl: './profile-editor.html',
})
export class ProfileEditor {
  private http = inject(HttpClient);
  protected meta = inject(MetaService);
  protected auth = inject(AuthService);

  wizard = input(false, { transform: booleanAttribute });
  saved = output<void>();

  protected steps = STEPS;
  protected step = signal(0);
  protected busy = signal(false);
  protected failure = signal<string | null>(null);
  protected savedOnce = signal(false);

  private fb = inject(FormBuilder).nonNullable;
  protected form = this.fb.group({
    riotName: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(16)]],
    riotTag: ['', [Validators.required, Validators.pattern(/^[\p{L}\p{N}]{3,5}$/u)]],
    region: ['', Validators.required],
    servers: this.fb.control<string[]>([]),
    platform: this.fb.control<string | null>(null),
    rank: this.fb.control<number | null>(null, Validators.required),
    peakRank: this.fb.control<number | null>(null),
    roles: this.fb.control<string[]>([], Validators.required),
    agents: this.fb.control<string[]>([]),
    languages: this.fb.control<string[]>([]),
    mic: this.fb.control<string | null>(null),
    modes: this.fb.control<string[]>([]),
    playstyle: this.fb.control<string | null>(null),
    playTimes: this.fb.control<string[]>([]),
    bio: ['', Validators.maxLength(300)],
    showDiscord: [false],
  });

  constructor() {
    this.http.get<Profile>('/api/me/profile').subscribe((p) => this.form.patchValue({ ...p, bio: p.bio ?? '' }));
  }

  protected show(i: number) {
    return !this.wizard() || this.step() === i;
  }

  protected stepInvalid() {
    return this.steps[this.step()].controls.some((c) => this.form.controls[c].invalid);
  }

  protected next() {
    this.steps[this.step()].controls.forEach((c) => this.form.controls[c].markAsTouched());
    if (!this.stepInvalid()) this.step.update((s) => s + 1);
  }

  protected showError(control: keyof typeof this.form.controls) {
    const c = this.form.controls[control];
    return c.invalid && c.touched;
  }

  protected has(key: Multi, id: string) {
    return this.form.controls[key].value.includes(id);
  }

  protected full(key: Multi, max: number | undefined) {
    return !!max && this.form.controls[key].value.length >= max;
  }

  protected toggle(key: Multi, id: string) {
    const c = this.form.controls[key];
    c.setValue(this.has(key, id) ? c.value.filter((v) => v !== id) : [...c.value, id]);
    c.markAsTouched();
    c.markAsDirty();
  }

  protected value(key: Single) {
    return this.form.controls[key].value;
  }

  protected pick(key: Single, id: string) {
    this.form.controls[key].setValue(id);
    this.form.controls[key].markAsDirty();
  }

  protected regionServers(m: Meta) {
    return m.regions.find((r) => r.id === this.form.controls.region.value)?.servers ?? [];
  }

  protected agentsFor(m: Meta, role: string) {
    return m.agents.filter((a) => a.role === role);
  }

  protected async save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.failure.set('Some required fields are missing or invalid.');
      return;
    }
    this.busy.set(true);
    this.failure.set(null);
    try {
      await firstValueFrom(this.http.put('/api/me/profile', this.form.getRawValue()));
      this.auth.user.update((u) => u && { ...u, onboarded: true });
      this.form.markAsPristine();
      this.savedOnce.set(true);
      this.saved.emit();
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
