import { defineConfig, devices } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5671';
const frontendUrl = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5173';
const frontendPort = new URL(frontendUrl).port || '5173';
const fakeGatePort = process.env.E2E_FAKE_GATE_PORT ?? '18084';
const fakeGateUrl = `http://127.0.0.1:${fakeGatePort}`;
const defaultConnectionString =
  'Host=127.0.0.1;Port=5432;Database=waterblocks;Username=postgres;Password=postgres;Include Error Detail=True';

export default defineConfig({
  testDir: './tests',
  workers: 1,
  timeout: 30_000,
  expect: {
    timeout: 5_000,
  },
  use: {
    baseURL: frontendUrl,
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command: 'node fake-gate-simulator.mjs',
      url: `${fakeGateUrl}/_api/accounts`,
      timeout: 30_000,
      reuseExistingServer: !process.env.CI,
      env: {
        FAKE_GATE_SIMULATOR_PORT: fakeGatePort,
      },
    },
    {
      command: `dotnet run --project ../Waterblocks.Api/Waterblocks.Api.csproj --urls ${apiUrl}`,
      url: `${apiUrl}/Health`,
      ignoreHTTPSErrors: true,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        FRONTEND_ORIGIN: frontendUrl,
        ARCHIVE_ALL_WORKSPACES_ENABLED:
          process.env.ARCHIVE_ALL_WORKSPACES_ENABLED ?? 'true',
        ConnectionStrings__DefaultConnection:
          process.env.ConnectionStrings__DefaultConnection ?? defaultConnectionString,
        GATE_SIMULATOR_BASE_URL: process.env.GATE_SIMULATOR_BASE_URL ?? fakeGateUrl,
      },
    },
    {
      command: `npm --prefix ../waterblocks-admin run dev -- --host 127.0.0.1 --port ${frontendPort}`,
      url: frontendUrl,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: {
        VITE_API_BASE_URL: apiUrl,
      },
    },
  ],
});
