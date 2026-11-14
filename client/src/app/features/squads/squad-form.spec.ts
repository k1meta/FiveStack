import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Meta } from '../../core/meta.service';
import { SquadForm } from './squad-form';

const META: Meta = {
  regions: [{ id: 'eu', name: 'Europe', servers: [{ id: 'frankfurt', name: 'Frankfurt' }] }],
  platforms: [],
  ranks: [
    { tier: 12, name: 'Gold 1', icon: '' },
    { tier: 18, name: 'Diamond 1', icon: '' },
  ],
  roles: [{ id: 'controller', name: 'Controller' }],
  agents: [],
  languages: [],
  mic: [],
  modes: [{ id: 'competitive', name: 'Competitive' }],
  playstyles: [],
  playTimes: [],
};

describe('SquadForm', () => {
  it('blocks a min rank above the max rank, then posts meta ids', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SquadForm);
    fixture.detectChanges();
    http.expectOne('/api/me/profile').flush({ region: 'eu', servers: ['frankfurt'] });
    http.expectOne('/api/meta').flush(META);
    await fixture.whenStable();

    const el = fixture.nativeElement as HTMLElement;
    const choose = async (name: string, index: number) => {
      const select = el.querySelector<HTMLSelectElement>(`select[formcontrolname=${name}]`)!;
      select.selectedIndex = index;
      select.dispatchEvent(new Event('change'));
      await fixture.whenStable();
    };
    const title = el.querySelector<HTMLInputElement>('[formcontrolname=title]')!;
    title.value = 'Need smokes';
    title.dispatchEvent(new Event('input'));

    await choose('rankMin', 2); // Diamond 1 (index 0 is "Any")
    await choose('rankMax', 1); // Gold 1
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    await fixture.whenStable();
    expect(el.textContent).toContain('Max rank is below min rank.');
    http.expectNone('/api/squads');

    await choose('rankMax', 2);
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    const post = http.expectOne({ method: 'POST', url: '/api/squads' });
    expect(post.request.body).toMatchObject({
      title: 'Need smokes', mode: 'competitive', region: 'eu', server: 'frankfurt', rankMin: 18, rankMax: 18,
    });
    post.flush({ id: 'abc' });
    http.verify();
  });
});
