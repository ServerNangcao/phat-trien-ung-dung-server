import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  timeout: 30_000,
  globalSetup: "./e2e/global-setup.ts",
  use: {
    baseURL: "http://localhost:4173",
  },
  webServer: [
    {
      // --no-launch-profile is mandatory: launchSettings.json hard-codes
      // ASPNETCORE_ENVIRONMENT=Development, which silently overrides the env below
      // and reloads the developer's real user-secrets (sandbox credentials). Without
      // it the seed's DEMO-signed IPN fails signature verification and every paid
      // order lands as Pending instead of Paid.
      command:
        "dotnet run --project src/Modules/Orders/Orders.Api --no-launch-profile --urls http://localhost:5001 -- --Persistence:Provider=InMemory --IntegrationDelivery:Enabled=false",
      env: { ASPNETCORE_ENVIRONMENT: "Testing" },
      url: "http://localhost:5001/health",
      cwd: "..",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command: "node scripts/mock-ticketing.js",
      url: "http://localhost:5003/api/ticketing/reservations/probe",
      cwd: "..",
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
    },
    {
      command: "npm run preview -- --port 4173 --strictPort",
      url: "http://localhost:4173/",
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
});
