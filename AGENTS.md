# backend — Ticketing modular monolith (TV2 scope: Orders + Notifications)

The full delivery picture — module flows, run commands, the endpoint authorization
contract for TV1's host, and known limitations — lives in
[TV2-DELIVERY.md](TV2-DELIVERY.md). `dotnet test` before declaring anything done;
module-boundary rules live beside the tests (`tests/*/Architecture`).

## Environment facts no config file confesses

- **Local run without PostgreSQL**: start any API with
  `-- --Persistence:Provider=InMemory`. The provider switch lives in each Program.cs
  because EF Core rejects two providers registered for one context type, so DbContext
  registration stays in the composition root.
- **Ports**: Orders.Api 5001, Notifications.Api 5002, mock Ticketing 5003.
  `node scripts/mock-ticketing.js` stands in for TV4's module;
  `POST /__set-tickets` seeds demo tickets for the FR-22 join.
- **Mimosa security hook** scans file writes: author source through Write/Edit, never
  Bash heredocs, and keep credential-looking literals out of test sources — generate
  them at runtime.
- **Windows Git Bash**: stop a backgrounded `dotnet run` with
  `taskkill //F //IM <AppName>.exe`; `kill $!` only kills the shell wrapper and leaves
  the app holding file locks.

Review enforces the coding standards in [CODING_STANDARDS.md](CODING_STANDARDS.md)
plus the mechanical check `scripts/check-bare-result.sh`, which CI runs.
