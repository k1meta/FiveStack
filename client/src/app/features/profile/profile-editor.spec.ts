import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Meta } from '../../core/meta.service';
import { Profile, ProfileEditor } from './profile-editor';

const META: Meta = {
  regions: [{ id: 'eu', name: 'Europe', servers: [{ id: 'frankfurt', name: 'Frankfurt' }] }],
  platforms: [{ id: 'pc', name: 'PC' }],
  ranks: [{ tier: 14, name: 'Gold 3', icon: '' }],
  roles: [{ id: 'controller', name: 'Controller' }],
  agents: [{ id: 'omen', name: 'Omen', role: 'controller', icon: '' }],
  languages: [],
  mic: [],
  modes: [],
  playstyles: [],
  playTimes: [],
};

const EMPTY: Profile = {
  riotName: '', riotTag: '', region: '', servers: [], platform: null, rank: null, peakRank: null, roles: [],
  agents: [], languages: [], mic: null, modes: [], playstyle: null, playTimes: [], bio: null, showDiscord: false,
};

describe('ProfileEditor (wizard)', () => {
  it('blocks Next until required fields are set, then saves meta ids', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(ProfileEditor);
    fixture.componentRef.setInput('wizard', true);
    let saved = false;
    fixture.componentInstance.saved.subscribe(() => (saved = true));
    fixture.detectChanges();
    http.expectOne('/api/me/profile').flush(EMPTY);
    http.expectOne('/api/meta').flush(META);
    await fixture.whenStable();

    const el = fixture.nativeElement as HTMLElement;
    const button = (text: string) => [...el.querySelectorAll('button')].find((b) => b.textContent!.trim() === text)!;
    const click = async (text: string) => {
      button(text).click();
      await fixture.whenStable();
    };
    const type = (name: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(`[formcontrolname=${name}]`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const choose = async (name: string, index: number) => {
      const select = el.querySelector<HTMLSelectElement>(`select[formcontrolname=${name}]`)!;
      select.selectedIndex = index;
      select.dispatchEvent(new Event('change'));
      await fixture.whenStable();
    };
    const chip = async (text: string) => {
      const label = [...el.querySelectorAll('label.chip')].find((l) => l.textContent!.trim() === text)!;
      label.querySelector('input')!.click();
      await fixture.whenStable();
    };

    await click('Next');
    expect(el.textContent).toContain('3-16 characters.'); // still on step 1

    type('riotName', 'Ana');
    type('riotTag', 'EUW1');
    await choose('region', 1);
    await chip('Frankfurt');
    await click('Next');

    await choose('rank', 1);
    await chip('Controller');
    await chip('Omen');
    await click('Next');
    await click('Next');
    await click('Finish');

    const put = http.expectOne({ method: 'PUT', url: '/api/me/profile' });
    expect(put.request.body).toMatchObject({
      riotName: 'Ana', riotTag: 'EUW1', region: 'eu', servers: ['frankfurt'], rank: 14,
      roles: ['controller'], agents: ['omen'],
    });
    put.flush(null);
    await fixture.whenStable();
    expect(saved).toBe(true);
    http.verify();
  });
});
