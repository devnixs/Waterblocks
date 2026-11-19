import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5671';
const fakeGateUrl = `http://127.0.0.1:${process.env.E2E_FAKE_GATE_PORT ?? '18084'}`;
const vaultName = 'Loan vault';
const accountName = 'Gate main';

type GateAccount = { name: string; key: string; secret: string };
type Transfer = { id: string; state: string };

function uniqueName(prefix: string) {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
}

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
  expect(response.ok(), await response.text()).toBeTruthy();
  const body = await response.json() as { data: T };
  return body.data;
}

async function getTransfers(request: APIRequestContext, accountId: string, workspaceId: string) {
  const response = await request.get(`${apiUrl}/admin/exchange-accounts/${accountId}/transfers`, {
    headers: { 'X-Workspace-Id': workspaceId },
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { data: Transfer[] }).data;
}

async function completeTransaction(request: APIRequestContext, transactionId: string, workspaceId: string) {
  for (const step of ['approve', 'sign', 'broadcast', 'confirm', 'complete']) {
    await postAdmin(request, `/admin/transactions/${transactionId}/${step}`, {}, workspaceId);
  }
}

async function createGateAccount(request: APIRequestContext) {
  const response = await request.post(`${fakeGateUrl}/_test/accounts`, { data: { name: uniqueName('e2e') } });
  expect(response.ok()).toBeTruthy();
  return await response.json() as GateAccount;
}

async function setGateBalance(request: APIRequestContext, account: GateAccount, amount: number) {
  const response = await request.put(`${fakeGateUrl}/_test/accounts/${account.name}/balances/BTC`, {
    data: { spot_available: amount },
  });
  expect(response.ok()).toBeTruthy();
}

async function createWorkspace(request: APIRequestContext) {
  return postAdmin<{ id: string; apiKeys: Array<{ key: string }> }>(
    request,
    '/admin/workspaces',
    { name: uniqueName('Off Exchange') },
  );
}

async function createFundedVault(request: APIRequestContext, workspaceId: string) {
  const vault = await postAdmin<{ id: string }>(request, '/admin/vaults', { name: vaultName }, workspaceId);
  const wallet = await postAdmin<{ depositAddress: string }>(
    request,
    `/admin/vaults/${vault.id}/wallets`,
    { assetId: 'BTC' },
    workspaceId,
  );
  await postAdmin(
    request,
    '/admin/transactions',
    { assetId: 'BTC', destinationAddress: wallet.depositAddress, amount: '2' },
    workspaceId,
  );
  return vault.id;
}

async function primeAdminSession(page: Page, workspaceId: string) {
  await page.addInitScript((currentWorkspaceId) => {
    localStorage.setItem('currentUserEmail', 'e2e@example.com');
    localStorage.setItem('workspaceId', currentWorkspaceId);
  }, workspaceId);
}

async function getProviders(request: APIRequestContext) {
  const response = await request.get(`${apiUrl}/admin/exchange-providers`);
  expect(response.ok()).toBeTruthy();
  return (await response.json() as { data: Array<{ exchangeType: string; name: string }> }).data;
}

async function fillConnectForm(dialog: Locator, account: GateAccount, secret = account.secret) {
  await dialog.getByRole('button', { name: /Gate\.io/ }).click();
  await dialog.getByLabel('Display name').fill(accountName);
  await dialog.getByLabel('API key').fill(account.key);
  await dialog.getByLabel('API secret', { exact: true }).fill(secret);
  await dialog.getByRole('button', { name: 'Connect account' }).click();
}

async function connectAccount(page: Page, account: GateAccount) {
  await page.goto('/exchanges');
  await page.getByRole('button', { name: 'Connect account' }).click();
  await fillConnectForm(page.getByRole('dialog'), account);
  await expect(page).toHaveURL(/\/exchanges\/[0-9a-f-]{36}$/);
  return page.url().split('/').pop()!;
}

function assetRow(page: Page) {
  return page.locator('tr[data-asset="BTC"]');
}

async function submitTransfer(page: Page, action: 'Add collateral' | 'Remove collateral', amount: string) {
  await assetRow(page).getByRole('button', { name: action }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByPlaceholder('Search by name, ID, or address...').click();
  await dialog.locator('.searchable-select-option', { hasText: vaultName }).click();
  await dialog.getByLabel('Amount', { exact: true }).fill(amount);
  await dialog.getByLabel('Deduct fees from amount').check();
  await expect(dialog.getByText('Recipient receives:')).toBeVisible();
  await dialog.getByRole('button', { name: action }).click();
  await expect(dialog).toBeHidden();
}

test('shows the empty state and the provider list', async ({ page, request }) => {
  const workspace = await createWorkspace(request);
  await primeAdminSession(page, workspace.id);

  await page.goto('/vaults');
  await expect(page.getByRole('heading', { name: /Vaults/ })).toBeVisible();
  await page.getByTitle('Keyboard shortcuts (?)').click();
  await expect(page.getByText('Navigate to Exchanges')).toBeVisible();
  await page.getByRole('button', { name: 'Close', exact: true }).first().click();
  await page.keyboard.press('5');
  await expect(page).toHaveURL(/\/exchanges$/);
  await expect(page.getByRole('link', { name: 'Exchanges' })).toHaveClass(/active/);

  await expect(page.getByRole('heading', { name: 'Connect an account' })).toBeVisible();
  await page.getByRole('button', { name: 'Connect account' }).click();
  const dialog = page.getByRole('dialog');
  const providers = await getProviders(request);
  await expect(dialog.locator('.exchange-provider')).toHaveText(
    providers.map((provider) => new RegExp(provider.name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))),
  );
  await expect(dialog.getByRole('button', { name: /Gate\.io/ })).toContainText('Off Exchange');
  await dialog.getByRole('button', { name: /Gate\.io/ }).click();
  await expect(dialog.getByText('Main account')).toBeVisible();
  await expect(dialog.getByLabel('API secret', { exact: true })).toHaveAttribute('type', 'password');
  await dialog.getByRole('button', { name: 'Show' }).click();
  await expect(dialog.getByLabel('API secret', { exact: true })).toHaveAttribute('type', 'text');
});

test('shows the message of a wrong key and keeps secrets out of browser storage', async ({ page, request }) => {
  const workspace = await createWorkspace(request);
  const gateAccount = await createGateAccount(request);
  await primeAdminSession(page, workspace.id);

  await page.goto('/exchanges');
  await page.getByRole('button', { name: 'Connect account' }).click();
  const dialog = page.getByRole('dialog');
  await fillConnectForm(dialog, gateAccount, 'wrong-secret');

  await expect(dialog.getByRole('alert')).toContainText('refused');
  await expect(page).toHaveURL(/\/exchanges$/);
  const storedValues = await page.evaluate(() => JSON.stringify({ ...localStorage, ...sessionStorage }));
  expect(storedValues).not.toContain(gateAccount.key);
  expect(storedValues).not.toContain('wrong-secret');

  await dialog.getByRole('button', { name: 'Cancel' }).click();
  await page.getByRole('button', { name: 'Connect account' }).click();
  await page.getByRole('dialog').getByRole('button', { name: /Gate\.io/ }).click();
  await expect(page.getByRole('dialog').getByLabel('API key')).toHaveValue('');
});

test('connects an account that was created through the API', async ({ page, request }) => {
  const workspace = await createWorkspace(request);
  const gateAccount = await createGateAccount(request);
  const created = await request.post(`${apiUrl}/exchange_accounts`, {
    data: { exchangeType: 'GATEIO', name: accountName },
    headers: { 'X-API-Key': workspace.apiKeys[0].key },
  });
  expect(created.status()).toBe(201);
  const { id } = await created.json() as { id: string };
  await primeAdminSession(page, workspace.id);

  await page.goto(`/exchanges/${id}`);

  await expect(page.getByText('This account has no simulator connection.')).toBeVisible();
  await expect(page.locator('tr[data-asset]')).toHaveCount(2);
  await page.getByLabel('Search assets').fill('usd');
  await expect(page.locator('tr[data-asset]')).toHaveCount(1);
  await expect(page.locator('tr[data-asset="USDT_ERC20"]')).toBeVisible();
  await page.getByLabel('Search assets').fill('');

  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('API key').fill(gateAccount.key);
  await dialog.getByLabel('API secret', { exact: true }).fill(gateAccount.secret);
  await dialog.getByRole('button', { name: 'Connect account' }).click();

  await expect(dialog).toBeHidden();
  await expect(page.getByText(gateAccount.name)).toBeVisible();
  await page.getByRole('button', { name: 'Refresh' }).click();
  await expect(page.getByText('This account has no simulator connection.')).toHaveCount(0);

  await page.goto('/exchanges');
  await expect(page.getByText('A workspace can have only one Gate.io account.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Connect account' })).toHaveCount(0);
});

test('connects an account, moves collateral and labels the transactions', async ({ page, request }) => {
  const workspace = await createWorkspace(request);
  await createFundedVault(request, workspace.id);
  const gateAccount = await createGateAccount(request);
  await primeAdminSession(page, workspace.id);

  const accountId = await connectAccount(page, gateAccount);

  await expect(page.getByLabel('Breadcrumb')).toContainText(`Exchanges>${accountName} (Main account)`);
  await expect(page.getByText(gateAccount.name)).toBeVisible();
  await expect(page.getByText(`${gateAccount.key.slice(0, 4)}****${gateAccount.key.slice(-4)}`)).toBeVisible();
  await expect(assetRow(page).getByRole('button', { name: 'Remove collateral' })).toBeDisabled();

  await submitTransfer(page, 'Add collateral', '1');
  await expect(page.getByText('ADD COLLATERAL', { exact: true })).toBeVisible();
  const [add] = await getTransfers(request, accountId, workspace.id);
  await completeTransaction(request, add.id, workspace.id);
  await expect(page.getByText('Applied at Gate.io', { exact: true })).toBeVisible({ timeout: 15_000 });
  await expect(assetRow(page)).toContainText('0.99990000', { timeout: 15_000 });

  await submitTransfer(page, 'Remove collateral', '0.4');
  await expect(page.getByText('REMOVE COLLATERAL', { exact: true })).toBeVisible();
  await expect(page.getByText('Applied at Gate.io', { exact: true })).toHaveCount(2, { timeout: 15_000 });
  const [removal] = await getTransfers(request, accountId, workspace.id);
  await completeTransaction(request, removal.id, workspace.id);
  await expect(assetRow(page)).toContainText('0.59990000', { timeout: 15_000 });

  await page.goto('/exchanges');
  await expect(page.locator('thead th')).toHaveText([
    'Name', 'Exchange', 'Simulator account', 'Connection', 'Collateral', 'Created',
  ]);
  const accountRow = page.getByRole('row', { name: new RegExp(accountName) });
  await expect(accountRow).toContainText(gateAccount.name);
  await expect(accountRow).toContainText('Gate.io');
  await expect(accountRow).toContainText('0.5999 BTC');
  await page.getByRole('row', { name: new RegExp(accountName) }).click();
  await expect(page).toHaveURL(new RegExp(`/exchanges/${accountId}$`));

  await page.goto('/transactions');
  await expect(page.getByRole('link', { name: accountName })).toHaveCount(2);
  await page.goto(`/transactions/${encodeURIComponent(removal.id)}`);
  const panel = page.locator('.detail-panel');
  await expect(panel.getByText(accountName, { exact: true })).toBeVisible();
  await expect(panel.getByText('EXTERNAL', { exact: true })).toHaveCount(0);
});

test('shows rejected removals', async ({ page, request }) => {
  const workspace = await createWorkspace(request);
  await createFundedVault(request, workspace.id);
  const gateAccount = await createGateAccount(request);
  await primeAdminSession(page, workspace.id);
  const accountId = await connectAccount(page, gateAccount);

  await submitTransfer(page, 'Add collateral', '1');
  await expect(page.getByText('ADD COLLATERAL', { exact: true })).toBeVisible();
  const [add] = await getTransfers(request, accountId, workspace.id);
  await completeTransaction(request, add.id, workspace.id);
  await expect(page.getByText('Applied at Gate.io', { exact: true })).toBeVisible({ timeout: 15_000 });

  await submitTransfer(page, 'Remove collateral', '5');
  const notifications = page.getByLabel('Notifications (F8)');
  await expect(notifications.getByText('The exchange rejected the removal')).toBeVisible();
  await expect(notifications.getByText(
    'The removal needs 5 BTC from the collateral account, but only 0.9999 BTC is free.',
  )).toBeVisible();
  await expect(page.locator('.exchange-transfer').first().locator('.state-badge')).toHaveText('CANCELLED');

  await setGateBalance(request, gateAccount, 0.1);
  await expect(assetRow(page)).toContainText('0.10000000', { timeout: 15_000 });
  await submitTransfer(page, 'Remove collateral', '0.5');
  await expect(page.getByText('Rejected by Gate.io', { exact: true })).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('.exchange-transfer').first().locator('.state-badge')).toHaveText('CANCELLED');
  await expect(assetRow(page).locator('td').nth(4)).toHaveText('0.00000000');
});
