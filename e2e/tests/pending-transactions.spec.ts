import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5671';

type Workspace = {
  id: string;
  name: string;
};

type AdminEnvelope<T> = {
  data: T;
  error?: { message: string; code: string } | null;
};

async function apiGet<T>(
  request: APIRequestContext,
  path: string,
  workspaceId?: string,
): Promise<T> {
  const response = await request.get(`${apiUrl}${path}`, {
    headers: workspaceId ? { 'X-Workspace-Id': workspaceId } : undefined,
  });
  expect(response.ok()).toBeTruthy();
  const body = await response.json() as AdminEnvelope<T>;
  expect(body.error ?? null).toBeNull();
  return body.data;
}

async function apiPost<T>(
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
  const body = await response.json() as AdminEnvelope<T>;
  expect(body.error ?? null).toBeNull();
  return body.data;
}

async function getWorkspaces(request: APIRequestContext) {
  return apiGet<Workspace[]>(request, '/admin/workspaces');
}

async function createWorkspace(request: APIRequestContext, name: string) {
  return apiPost<Workspace>(request, '/admin/workspaces', { name });
}

async function ensureDefaultWorkspace(request: APIRequestContext) {
  const existing = await getWorkspaces(request);
  const defaultWorkspace = existing.find((workspace) => workspace.name === 'Default');
  if (defaultWorkspace) {
    return defaultWorkspace;
  }

  return createWorkspace(request, 'Default');
}

async function createVault(request: APIRequestContext, workspaceId: string, name: string) {
  return apiPost<{ id: string; name: string }>(request, '/admin/vaults', { name }, workspaceId);
}

async function createWallet(request: APIRequestContext, workspaceId: string, vaultId: string, assetId: string) {
  return apiPost<{ depositAddress?: string }>(request, `/admin/vaults/${vaultId}/wallets`, { assetId }, workspaceId);
}

async function createTransaction(
  request: APIRequestContext,
  workspaceId: string,
  payload: Record<string, unknown>,
) {
  return apiPost<{ id: string }>(request, '/admin/transactions', payload, workspaceId);
}

async function cancelTransaction(request: APIRequestContext, workspaceId: string, transactionId: string) {
  return apiPost<{ id: string; state: string }>(
    request,
    `/admin/transactions/${encodeURIComponent(transactionId)}/cancel`,
    {},
    workspaceId,
  );
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
    {
      apiBaseUrl: apiUrl,
      currentWorkspaceId: workspaceId,
    },
  );
}

test('shows all-workspaces pending transactions in the header dropdown and navigates to details', async ({
  page,
  request,
}) => {
  await ensureDefaultWorkspace(request);

  const senderWorkspace = await createWorkspace(request, `Pending Sender ${Date.now()}`);
  const receiverWorkspace = await createWorkspace(request, `Pending Receiver ${Date.now()}`);

  const senderVault = await createVault(request, senderWorkspace.id, 'Sender Vault');
  const receiverVault = await createVault(request, receiverWorkspace.id, 'Receiver Vault');
  const senderWallet = await createWallet(request, senderWorkspace.id, senderVault.id, 'BTC');
  const receiverWallet = await createWallet(request, receiverWorkspace.id, receiverVault.id, 'BTC');

  const senderAddress = senderWallet.depositAddress;
  const receiverAddress = receiverWallet.depositAddress;
  expect(senderAddress).toBeTruthy();
  expect(receiverAddress).toBeTruthy();

  await createTransaction(request, senderWorkspace.id, {
    assetId: 'BTC',
    sourceAddress: 'external-funder',
    destinationAddress: senderAddress,
    amount: '10',
  });

  await createTransaction(request, senderWorkspace.id, {
    assetId: 'BTC',
    sourceAddress: 'external-pending-source',
    destinationAddress: senderAddress,
    amount: '2',
    initialState: 'SUBMITTED',
  });

  const crossWorkspaceTransaction = await createTransaction(request, senderWorkspace.id, {
    assetId: 'BTC',
    sourceAddress: senderAddress,
    destinationAddress: receiverAddress,
    amount: '1',
  });

  await primeAdminSession(page, receiverWorkspace.id);
  await page.goto('/transactions');

  const pendingTrigger = page.getByRole('button', { name: '2 pending transactions' });
  await expect(pendingTrigger).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('.workspace-select')).toHaveValue(receiverWorkspace.id);

  await pendingTrigger.click();

  const dropdown = page.locator('.pending-transactions-dropdown');
  await expect(dropdown).toBeVisible();
  await expect(dropdown.locator('select')).toHaveCount(0);
  await expect(dropdown).toContainText('1 BTC');
  await expect(dropdown).toContainText('SUBMITTED');
  await expect(dropdown).toContainText(senderWorkspace.name);
  await expect(dropdown).toContainText(senderAddress!);
  await expect(dropdown).toContainText(receiverWorkspace.name);
  await expect(dropdown).toContainText(receiverAddress!);
  await expect(dropdown).toContainText('Source address name');
  await expect(dropdown).toContainText('Destination address name');

  await dropdown.locator('.pending-transaction-row').first().click();

  await expect(dropdown).toHaveCount(0);
  await expect(page.locator('.workspace-select')).toHaveValue(senderWorkspace.id);
  await expect(page).toHaveURL(new RegExp(`/transactions/${encodeURIComponent(crossWorkspaceTransaction.id)}$`));
  await expect(page.locator('.detail-panel')).toBeVisible();
  await expect(page.locator('.detail-panel')).toContainText('Transaction Details');
  await expect(page.locator('.detail-panel')).toContainText(crossWorkspaceTransaction.id);
  await expect(page.locator('.detail-panel')).toContainText(senderAddress!);
  await expect(page.locator('.detail-panel')).toContainText(receiverAddress!);

  await cancelTransaction(request, senderWorkspace.id, crossWorkspaceTransaction.id);

  await expect(page.getByRole('button', { name: '1 pending transactions' })).toBeVisible({ timeout: 15_000 });
  await page.getByRole('button', { name: '1 pending transactions' }).click();
  await expect(page.locator('.pending-transactions-dropdown')).toBeVisible();
  await expect(page.locator('.pending-transactions-dropdown')).not.toContainText(receiverWorkspace.name);
});
