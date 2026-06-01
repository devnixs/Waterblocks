import { defineConfig, devices } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5671';
const frontendUrl = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5173';
const defaultConnectionString =
  'Host=127.0.0.1;Port=5432;Database=waterblocks;Username=postgres;Password=postgres;Include Error Detail=True';

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  expect: {
    timeout: 5_000,
  },
  use: {
    baseURL: frontendUrl,
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
      command: `dotnet run --project ../Waterblocks.Api/Waterblocks.Api.csproj --urls ${apiUrl}`,
      url: `${apiUrl}/Health`,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        FRONTEND_ORIGIN: frontendUrl,
        ConnectionStrings__DefaultConnection:
          process.env.ConnectionStrings__DefaultConnection ?? defaultConnectionString,
      },
    },
    {
      command: 'npm --prefix ../waterblocks-admin run dev -- --host 127.0.0.1 --port 5173',
      url: frontendUrl,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: {
        VITE_API_BASE_URL: apiUrl,
      },
    },
  ],
});
