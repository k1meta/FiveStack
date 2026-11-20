import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

// TODO(M6): replace with the project contact address before shipping.
const CONTACT_EMAIL = 'contact@example.com';
const UPDATED = 'November 14, 2026';

const prose =
  'mx-auto block max-w-3xl px-4 py-12 [&_h1]:font-display [&_h1]:text-5xl [&_h1]:uppercase [&_h1]:tracking-wide ' +
  '[&_h2]:mt-10 [&_h2]:text-lg [&_h2]:font-semibold [&_h2]:uppercase [&_h2]:tracking-wider [&_p]:mt-3 [&_p]:text-val-bone/80 ' +
  '[&_ul]:mt-3 [&_ul]:list-disc [&_ul]:space-y-1 [&_ul]:pl-6 [&_ul]:text-val-bone/80 [&_a]:text-val-teal [&_a]:underline';

@Component({
  selector: 'app-privacy-page',
  imports: [RouterLink],
  host: { class: prose },
  template: `
    <h1>Privacy</h1>
    <p class="text-sm">Last updated {{ updated }}</p>

    <h2>What we store</h2>
    <ul>
      <li>
        Your account: email address and a hashed password, or the id that Google or Discord gives us
        when you sign in with them.
      </li>
      <li>
        Your Discord username, if you link Discord. It is shown on your profile only when you turn
        that on.
      </li>
      <li>
        Your profile: Riot ID, region, servers, platform, ranks, roles, agents, languages, mic,
        modes, playstyle, play times and bio. Profiles are public.
      </li>
      <li>Squad posts, join requests, chat messages and the players you block.</li>
      <li>When you were last active, used for "online now" and sorting.</li>
    </ul>

    <h2>Cookies</h2>
    <p>
      We set one sign-in cookie, plus a short-lived cookie during Google or Discord sign-in. Both
      are needed for the site to work. There are no analytics, advertising or tracking cookies.
    </p>

    <h2>Third parties</h2>
    <ul>
      <li>Google and Discord, only if you choose to sign in with them.</li>
      <li>
        valorant-api.com serves agent and rank icons. Your browser loads these directly. Fonts are
        served by FiveStack itself.
      </li>
      <li>The app is hosted on Render and the database on Neon.</li>
      <li>
        Grafana Cloud stores the app's technical logs and performance metrics (request paths,
        timings, errors) for up to 14 days. They contain no IP addresses, emails or message text.
      </li>
    </ul>
    <p>We don't sell or share your data with anyone else.</p>

    <h2>Deleting your data</h2>
    <p>
      Delete your account any time from <a routerLink="/settings">Settings</a>. That removes your
      account, profile, squad posts, join requests, blocks and the messages you sent. For anything
      else, email <a [href]="'mailto:' + email">{{ email }}</a>.
    </p>
  `,
})
export class PrivacyPage {
  protected email = CONTACT_EMAIL;
  protected updated = UPDATED;
}

@Component({
  selector: 'app-terms-page',
  host: { class: prose },
  template: `
    <h1>Terms</h1>
    <p class="text-sm">Last updated {{ updated }}</p>

    <h2>The service</h2>
    <p>
      FiveStack is a free, hobby project for finding Valorant teammates. It is provided as is,
      without any warranty, and may change or go offline at any time.
    </p>

    <h2>Your account</h2>
    <ul>
      <li>You must be at least 13 years old.</li>
      <li>Keep your profile honest. Don't impersonate other players.</li>
      <li>You are responsible for what you post and send.</li>
    </ul>

    <h2>Don't</h2>
    <ul>
      <li>Harass, threaten or hate on other players.</li>
      <li>Spam, advertise, or sell accounts, boosting or cheats.</li>
      <li>Try to break, scrape or overload the site.</li>
    </ul>
    <p>We may remove content or accounts that break these rules.</p>

    <h2>Riot Games</h2>
    <p>
      FiveStack isn't endorsed by Riot Games and doesn't reflect the views or opinions of Riot Games
      or anyone officially involved in producing or managing Riot Games properties. Riot Games and
      all associated properties are trademarks or registered trademarks of Riot Games, Inc.
    </p>

    <h2>Contact</h2>
    <p>
      <a [href]="'mailto:' + email">{{ email }}</a>
    </p>
  `,
})
export class TermsPage {
  protected email = CONTACT_EMAIL;
  protected updated = UPDATED;
}
