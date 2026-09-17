import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5671';

async function postAdmin<T>(
  request: APIRequestContext,
  path: string,
  data: unknown,
  workspaceId?: string,
): Promise<T> {
  const response = await request.post(`${apiUrl}${path}`, {
    data,
    headers: workspaceId ? { 'X-Workspace-Id': workspaceId } : undefined,
  });
  expect(response.ok()).toBeTruthy();
  const body = await response.json() as { data: T; error: unknown };
  expect(body.error ?? null).toBeNull();
  return body.data;
}

async function primeAdminSession(page: Page, workspaceId: string) {
  await page.addInitScript(
    ({ apiBaseUrl, currentWorkspaceId }) => {
      localStorage.setItem('currentUserEmail', 'e2e@example.com');
      localStorage.setItem('workspaceId', currentWorkspaceId);
      window.__WB_CONFIG__ = {
        ...(window.__WB_CONFIG__ ?? {}),
        apiBaseUrl,
      };
    },
    { apiBaseUrl: apiUrl, currentWorkspaceId: workspaceId },
  );
}

test('creates and labels a source-less BTC exchange deposit', async ({ page, request }) => {
  const transactionHash = Date.now().toString(16).padStart(64, '0');
  const workspace = await postAdmin<{ id: string }>(
    request,
    '/admin/workspaces',
    { name: `Source-less BTC ${Date.now()}` },
  );
  const vault = await postAdmin<{ id: string }>(
    request,
    '/admin/vaults',
    { name: 'Trading Deposit' },
    workspace.id,
  );
  await postAdmin<{ depositAddress: string }>(
    request,
    `/admin/vaults/${vault.id}/wallets`,
    { assetId: 'BTC' },
    workspace.id,
  );
  const secondaryWallet = await postAdmin<{ depositAddress: string }>(
    request,
    `/admin/vaults/${vault.id}/wallets`,
    { assetId: 'BTC' },
    workspace.id,
  );

  await primeAdminSession(page, workspace.id);
  await page.goto('/transactions');
  await page.getByRole('button', { name: '+ New Transaction' }).click();

  const form = page.locator('form');
  const assetSelect = form.locator('select').nth(0);
  const sourceSelect = form.locator('select').nth(1);
  const destinationSelect = form.locator('select').nth(2);

  await assetSelect.selectOption('ETH');
  await expect(sourceSelect.locator('option[value="SOURCELESS_EXCHANGE"]')).toHaveCount(0);

  await assetSelect.selectOption('BTC');
  await sourceSelect.selectOption('SOURCELESS_EXCHANGE');
  await expect(form.getByText('Fireblocks will return an UNKNOWN external source')).toBeVisible();
  await expect(destinationSelect.locator('option[value="ONE_TIME"]')).toBeEnabled();
  await destinationSelect.selectOption('ONE_TIME');
  await form.getByPlaceholder('Destination address').fill(secondaryWallet.depositAddress);

  await form.getByPlaceholder('0.00').fill('0.01654844');
  await form.getByPlaceholder('Leave empty for auto-generation').fill(transactionHash);
  await form.getByPlaceholder('0', { exact: true }).fill('36');
  await form.getByPlaceholder('967336').fill('967336');
  await form.getByPlaceholder('Block hash').fill(
    '000000000000000000022b9b810c2fac40e46e241d36df8a7a7fe5a67f0692f2',
  );
  await form.getByRole('button', { name: 'Create Transaction' }).click();

  await expect(page.getByLabel('Notifications (F8)').getByText('Transaction created')).toBeVisible();
  await expect(page.getByText('Coinbase / exchange (no source address)')).toBeVisible();

  await page.getByRole('button', { name: 'View' }).first().click();
  await expect(page.getByText('Coinbase / exchange (source address unavailable)')).toBeVisible();
  await expect(page.getByText('Output index:')).toBeVisible();
  await expect(page.getByText('36', { exact: true })).toBeVisible();
  await expect(page.getByText('967336', { exact: true })).toBeVisible();
  await expect(page.getByText(secondaryWallet.depositAddress, { exact: true })).toBeVisible();
});
