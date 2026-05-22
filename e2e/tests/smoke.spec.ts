import { expect, test } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5671';
const externalToExternalMessage =
  'You are trying to create a transaction from an external address to another external address. Are you sure the destination address exists?';

test('renders the admin login shell', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Waterblocks Admin' })).toBeVisible();
  await expect(page.getByText('Enter your email address to continue')).toBeVisible();
  await expect(page.getByPlaceholder('your@email.com')).toBeVisible();
});

test('shows the external-to-external transaction scope error', async ({ page, request }) => {
  const workspaceResponse = await request.post(`${apiUrl}/admin/workspaces`, {
    data: { name: `E2E External Error ${Date.now()}` },
  });
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json();
  const workspaceId = workspace.data.id;

  await page.addInitScript((id) => {
    localStorage.setItem('currentUserEmail', 'e2e@example.com');
    localStorage.setItem('workspaceId', id);
  }, workspaceId);

  await page.goto('/transactions');

  await expect(page.getByRole('heading', { name: /Transactions/ })).toBeVisible();
  await page.getByRole('button', { name: '+ New Transaction' }).click();

  const form = page.locator('form');
  await form.locator('select').nth(0).selectOption('BTC');
  await form.locator('select').nth(2).selectOption('ONE_TIME');
  await form.getByPlaceholder('Destination address').fill('external-btc-destination');
  await form.getByPlaceholder('0.00').fill('1.0');

  await form.getByRole('button', { name: 'Create Transaction' }).click();

  await expect(
    page.getByLabel('Notifications (F8)').getByText(`Error: ${externalToExternalMessage}`)
  ).toBeVisible();
});
