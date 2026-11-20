import { expect, test } from '@playwright/test';
import { signUp, unique } from './support';

test('a player posts a squad, finds it on the board, then closes it', async ({ page }) => {
  await signUp(page, `Host${unique()}`);
  const title = `E2E squad ${unique()}`;

  await page.goto('/squads/new');
  await expect(page.getByLabel('Region')).toHaveValue('eu'); // prefilled from the profile
  await page.getByLabel('Title').fill(title);
  await page.getByRole('button', { name: 'Post squad' }).click();
  await expect(page.getByRole('heading', { name: title })).toBeVisible();

  await page.goto('/squads?region=eu');
  await page.getByText(title).click();
  page.once('dialog', async (d) => {
    expect(d.message()).toContain('Close this squad?');
    await d.accept();
  });
  await page.getByRole('button', { name: 'Close squad' }).click();
  await expect(page.getByRole('button', { name: 'Close squad' })).toHaveCount(0);

  await page.goto('/squads?region=eu');
  await expect(page.getByRole('heading', { name: 'Squads' })).toBeVisible();
  await expect(page.getByText(title)).toHaveCount(0);
});
