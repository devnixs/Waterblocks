import { expect, test } from '@playwright/test';

test('renders the admin login shell', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Waterblocks Admin' })).toBeVisible();
  await expect(page.getByText('Enter your email address to continue')).toBeVisible();
  await expect(page.getByPlaceholder('your@email.com')).toBeVisible();
});
