const { defineConfig, devices } = require('@playwright/test');

const appE2eEnabled = Boolean(process.env.BIRDIEBUDDY_E2E_POSTGRES);

const webServer = [
  {
    command: 'node tests/browser/smoke-server.cjs',
    url: 'http://127.0.0.1:4173/live-round.html?id=1',
    reuseExistingServer: !process.env.CI,
    timeout: 30_000
  }
];

if (appE2eEnabled) {
  webServer.push({
    command: 'dotnet run --project BirdieBuddy.csproj --configuration Release --no-launch-profile',
    url: 'http://127.0.0.1:5187/health/ready',
    reuseExistingServer: false,
    timeout: 120_000,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      ASPNETCORE_URLS: 'http://127.0.0.1:5187',
      ConnectionStrings__DefaultConnection: process.env.BIRDIEBUDDY_E2E_POSTGRES,
      Authentication__RequireVerifiedEmail: 'false',
      RateLimiting__AuthPermitLimit: '100'
    }
  });
}

module.exports = defineConfig({
  testDir: './tests/e2e',
  outputDir: './output/playwright/results',
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI
    ? [['line'], ['html', { outputFolder: './output/playwright/report', open: 'never' }]]
    : 'line',
  use: {
    ...devices['iPhone 13'],
    browserName: 'chromium',
    locale: 'en-NZ',
    timezoneId: 'Pacific/Auckland',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  webServer,
  projects: [
    {
      name: 'fixture-mobile',
      testMatch: /fixture-mobile\.spec\.cjs/,
      use: { baseURL: 'http://127.0.0.1:4173' }
    },
    {
      name: 'app-mobile',
      testMatch: /app-mobile\.spec\.cjs/,
      use: { baseURL: 'http://127.0.0.1:5187' }
    }
  ]
});
