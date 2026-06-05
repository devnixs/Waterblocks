import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5671';

type WorkspaceResponse = {
  data: {
    id: string;
    name: string;
  };
};

async function getWorkspaces(request: APIRequestContext) {
  const response = await request.get(`${apiUrl}/admin/workspaces`);
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  return body.data as Array<{ id: string; name: string }>;
}

async function createWorkspace(request: APIRequestContext, name: string) {
  const response = await request.post(`${apiUrl}/admin/workspaces`, {
    data: { name },
  });

  expect(response.ok()).toBeTruthy();

  const body = await response.json() as WorkspaceResponse;
  return body.data;
}

async function archiveWorkspace(request: APIRequestContext, workspaceId: string) {
  const response = await request.delete(`${apiUrl}/admin/workspaces/${encodeURIComponent(workspaceId)}`);
  expect(response.ok()).toBeTruthy();
  const body = await response.json() as { data: boolean };
  expect(body.data).toBeTruthy();
}

async function ensureDefaultWorkspace(request: APIRequestContext) {
  const existing = await getWorkspaces(request);
  const defaultWorkspace = existing.find((workspace) => workspace.name === 'Default');
  if (defaultWorkspace) {
    return defaultWorkspace;
  }

  return createWorkspace(request, 'Default');
}

async function primeAdminSession(
  page: Page,
  workspaceId: string,
  archiveAllWorkspacesEnabled: boolean
) {
  await page.addInitScript(
    ({ apiBaseUrl, currentWorkspaceId, enabled }) => {
      localStorage.setItem('currentUserEmail', 'e2e@example.com');
      localStorage.setItem('workspaceId', currentWorkspaceId);
      window.__WB_CONFIG__ = {
        ...(window.__WB_CONFIG__ ?? {}),
        apiBaseUrl,
        archiveAllWorkspacesEnabled: String(enabled),
      };
    },
    {
      apiBaseUrl: apiUrl,
      currentWorkspaceId: workspaceId,
      enabled: archiveAllWorkspacesEnabled,
    }
  );
}

function workspaceCard(page: Page, workspaceName: string) {
  return page.locator('.card').filter({ hasText: workspaceName });
}

function workspaceCardById(page: Page, workspaceId: string) {
  return page.locator('.card').filter({ hasText: workspaceId });
}

test('archives all non-default workspaces from the workspaces page when enabled', async ({
  page,
  request,
}) => {
  const defaultWorkspace = await ensureDefaultWorkspace(request);
  const alphaName = `Bulk Alpha ${Date.now()}`;
  const betaName = `Bulk Beta ${Date.now()}`;

  await createWorkspace(request, alphaName);
  await createWorkspace(request, betaName);
  await primeAdminSession(page, defaultWorkspace.id, true);

  await page.goto('/workspaces');
  await expect(workspaceCardById(page, defaultWorkspace.id)).toBeVisible({ timeout: 15_000 });

  await expect(page.getByRole('button', { name: 'Archive all workspaces' })).toBeVisible();
  await expect(workspaceCardById(page, defaultWorkspace.id)).toContainText('Default');
  await expect(workspaceCard(page, alphaName)).toBeVisible();
  await expect(workspaceCard(page, betaName)).toBeVisible();

  page.once('dialog', async (dialog) => {
    expect(dialog.message()).toContain('Archive all workspaces except Default');
    await dialog.accept();
  });
  await page.getByRole('button', { name: 'Archive all workspaces' }).click();

  await expect(
    page.getByLabel('Notifications (F8)').getByText('Workspaces archived')
  ).toBeVisible();
  await expect(workspaceCard(page, alphaName)).toHaveCount(0);
  await expect(workspaceCard(page, betaName)).toHaveCount(0);
  await expect(workspaceCardById(page, defaultWorkspace.id)).toBeVisible();
});

test('hides the bulk archive button when the runtime config does not enable it', async ({
  page,
  request,
}) => {
  const defaultWorkspace = await ensureDefaultWorkspace(request);

  await primeAdminSession(page, defaultWorkspace.id, false);
  await page.goto('/workspaces');
  await expect(workspaceCardById(page, defaultWorkspace.id)).toBeVisible({ timeout: 15_000 });

  await expect(
    page.getByRole('button', { name: 'Archive all workspaces' })
  ).toHaveCount(0);
});

test('updates the workspace selector and page cards live when workspaces change', async ({
  page,
  request,
}) => {
  const defaultWorkspace = await ensureDefaultWorkspace(request);
  const liveWorkspaceName = `Live Workspace ${Date.now()}`;

  await primeAdminSession(page, defaultWorkspace.id, true);
  await page.goto('/workspaces');
  await expect(workspaceCardById(page, defaultWorkspace.id)).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('.workspace-select')).toHaveValue(defaultWorkspace.id);

  const createdWorkspace = await createWorkspace(request, liveWorkspaceName);

  await expect(workspaceCard(page, liveWorkspaceName)).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('.workspace-select')).toContainText(liveWorkspaceName);

  await archiveWorkspace(request, createdWorkspace.id);

  await expect(workspaceCard(page, liveWorkspaceName)).toHaveCount(0, { timeout: 15_000 });
  await expect(page.locator('.workspace-select')).not.toContainText(liveWorkspaceName);
});
