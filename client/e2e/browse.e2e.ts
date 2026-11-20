import { expect, test } from '@playwright/test';

test('a visitor filters players by role, and the filter lands in the URL', async ({ page }) => {
  await page.goto('/players?region=na'); // E2E players are all EU, so this page holds only the fixed-seed demo players
  await expect(page.getByRole('heading', { name: 'Find players' })).toBeVisible();

  const cards = page.locator('a[href^="/u/"]');
  const allControllers = async () => {
    const texts = await cards.allInnerTexts();
    return texts.length > 0 && texts.every((t) => /controller/i.test(t));
  };
  await expect.poll(async () => (await cards.count()) > 0 && !(await allControllers())).toBe(true); // mixed demo players

  await page.getByRole('group', { name: 'Roles' }).getByText('Controller').click();

  await expect(page).toHaveURL(/region=na.*roles=controller|roles=controller.*region=na/);
  await expect.poll(allControllers).toBe(true);
});
