import { NgTemplateOutlet } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { MetaService } from '../../core/meta.service';

@Component({
  selector: 'app-landing-page',
  imports: [RouterLink, NgTemplateOutlet],
  templateUrl: './landing-page.html',
})
export class LandingPage {
  protected auth = inject(AuthService);
  protected meta = inject(MetaService);

  protected readonly steps = [
    { title: 'Build your profile', body: 'Riot ID, rank, roles, agent pool, servers, languages and mic. Takes two minutes.' },
    { title: 'Find or post', body: 'Filter players by anything on their profile, or post a squad and say what you need.' },
    { title: 'Chat and queue', body: 'Accepted players land in a squad chat. Swap Discords and load in.' },
  ];

  protected agentsFor(role: string) {
    return this.meta.meta.value()?.agents.filter((a) => a.role === role) ?? [];
  }
}
