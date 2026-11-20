import { expect, type Page } from '@playwright/test';

/** A short unique suffix, so names and titles from parallel or repeated runs don't collide. */
export const unique = () => crypto.randomUUID().slice(0, 6);

/**
 * Registers, signs in and onboards a fresh player through the API. `page.request` shares the page's cookies,
 * so the page is signed in afterwards. Returns the player's id.
 */
export async function signUp(page: Page, riotName: string): Promise<string> {
  const creds = { email: `${riotName.toLowerCase()}@e2e.test`, password: 'Passw0rd!' };
  expect((await page.request.post('/api/auth/register', { data: creds })).ok()).toBe(true);
  expect((await page.request.post('/api/auth/login?useCookies=true', { data: creds })).ok()).toBe(
    true,
  );
  const profile = {
    riotName,
    riotTag: 'E2E',
    region: 'eu',
    servers: ['frankfurt'],
    rank: 12,
    roles: ['controller'],
    agents: ['omen'],
  };
  expect((await page.request.put('/api/me/profile', { data: profile })).ok()).toBe(true);
  return (await (await page.request.get('/api/auth/me')).json()).id;
}
