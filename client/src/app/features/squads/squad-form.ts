import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Meta, MetaService } from '../../core/meta.service';
import { errorMessage } from '../../core/util';
import { Profile } from '../profile/profile-editor';

/** Min rank above max rank is the one cross-field rule; the server re-checks it. */
function rankOrder(g: AbstractControl): ValidationErrors | null {
  const { rankMin, rankMax } = g.value as { rankMin: number | null; rankMax: number | null };
  return rankMin !== null && rankMax !== null && rankMin > rankMax ? { rankOrder: true } : null;
}

const MAX_AGENTS = 5;

/** New squad post at /squads/new. Limits mirror SquadInput on the server. */
@Component({
  selector: 'app-squad-form',
  imports: [ReactiveFormsModule],
  templateUrl: './squad-form.html',
})
export class SquadForm {
  private http = inject(HttpClient);
  private router = inject(Router);
  protected meta = inject(MetaService);

  protected busy = signal(false);
  protected failure = signal<string | null>(null);
  protected maxAgents = MAX_AGENTS;

  private fb = inject(FormBuilder).nonNullable;
  protected form = this.fb.group(
    {
      title: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(60)]],
      mode: ['competitive', Validators.required],
      region: ['', Validators.required],
      server: this.fb.control<string | null>(null),
      rankMin: this.fb.control<number | null>(null),
      rankMax: this.fb.control<number | null>(null),
      neededRoles: this.fb.control<string[]>([]),
      neededAgents: this.fb.control<string[]>([]),
      slotsOpen: [1],
      micRequired: [false],
    },
    { validators: rankOrder },
  );

  constructor() {
    // Start from where the host plays.
    this.http
      .get<Profile>('/api/me/profile')
      .subscribe((p) => this.form.patchValue({ region: p.region, server: p.servers[0] ?? null }));
    this.form.controls.region.valueChanges.subscribe(() =>
      this.form.controls.server.setValue(null),
    );
  }

  protected has(key: 'neededRoles' | 'neededAgents', id: string) {
    return this.form.controls[key].value.includes(id);
  }

  protected toggle(key: 'neededRoles' | 'neededAgents', id: string) {
    const c = this.form.controls[key];
    c.setValue(this.has(key, id) ? c.value.filter((v) => v !== id) : [...c.value, id]);
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
      this.failure.set(
        this.form.hasError('rankOrder')
          ? 'Max rank is below min rank.'
          : 'Fill in the required fields.',
      );
      return;
    }
    this.busy.set(true);
    this.failure.set(null);
    try {
      const { id } = await firstValueFrom(
        this.http.post<{ id: string }>('/api/squads', this.form.getRawValue()),
      );
      await this.router.navigate(['/squads', id]);
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
