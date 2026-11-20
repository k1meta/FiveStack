import { expect, test } from '@playwright/test';
import { signUp, unique } from './support';

test('a direct message shows up live in the open thread, without a reload', async ({ browser }) => {
  const [a, b] = [await browser.newPage(), await browser.newPage()]; // separate contexts: separate sessions
  await signUp(a, `Ana${unique()}`);
  const bId = await signUp(b, `Bo${unique()}`);

  await a.goto(`/u/${bId}`);
  await a.getByRole('button', { name: 'Message', exact: true }).click();
  await expect(a).toHaveURL(/\/messages\/[0-9a-f-]+$/);
  const thread = new URL(a.url()).pathname;

  // B opens the (empty) thread and its hub connection finishes the SignalR handshake.
  const hub = b.waitForEvent('websocket', (ws) => ws.url().includes('/hubs/chat'));
  await b.goto(thread);
  await (await hub).waitForEvent('framereceived');
  await b.waitForLoadState('networkidle');
  const refetches: string[] = [];
  b.on(
    'request',
    (r) => r.method() === 'GET' && r.url().includes('/messages') && refetches.push(r.url()),
  );

  const text = `gg wp ${unique()}`;
  await a.getByPlaceholder('Message').fill(text);
  await a.getByRole('button', { name: 'Send' }).click();

  await expect(b.getByText(text).first()).toBeVisible();
  expect(refetches).toEqual([]); // it came over the WebSocket, not a reload of the thread
});
