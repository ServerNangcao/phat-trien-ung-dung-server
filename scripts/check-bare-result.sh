#!/usr/bin/env bash
# Bans the non-generic Result factories everywhere except the few files where a
# non-generic Result is the declared type. SharedKernel exposes both `Result` and
# `Result<T>`; the non-generic type shadows the generic factory, so
# `Result.Success(x)` / `Result.Failure(e)` silently returns the wrong type
# (CS0029/CS1501) wherever a `Result<T>` is expected. This bit four times in one
# session. Everywhere else the call must be generic: `Result<T>.Success(...)`.
#
# Allowlist (declared non-generic Result):
#   - **/Infrastructure/Data/UnitOfWork.cs  (IUnitOfWork.CommitAsync returns Task<Result>)
#   - tests/Notifications.Tests/TestSupport/FakeEmailRepository.cs (FakeUnitOfWork double)
#   - src/Modules/Orders/Orders.Infrastructure/Ports/HttpTicketingModuleApi.cs (CancelReservationAsync returns Task<Result>)
#   - tests/Orders.Tests/TestSupport/FakeTicketingModuleApi.cs (CancelReservationAsync test double returns Task<Result>)
#   - tests/Orders.Tests/Application/SeatAllocationRaceConditionTests.cs (helper methods return Task<Result>)
# Extend the allowlist only with the same justification.
set -euo pipefail
cd "$(dirname "$0")/.."

violations=$(grep -rn --include='*.cs' -E '\bResult\.(Success|Failure)\(' src tests \
  | grep -v 'Data/UnitOfWork.cs' \
  | grep -v 'TestSupport/FakeEmailRepository.cs' \
  | grep -v 'Orders.Infrastructure/Ports/HttpTicketingModuleApi.cs' \
  | grep -v 'Orders.Tests/TestSupport/FakeTicketingModuleApi.cs' \
  | grep -v 'Orders.Tests/Application/SeatAllocationRaceConditionTests.cs' || true)

if [ -n "$violations" ]; then
  echo "check-bare-result: bare Result.Success/Failure found." >&2
  echo "Use Result<T>.Success(...) / Result<T>.Failure(...) unless the declared type is" >&2
  echo "the non-generic Result (see the allowlist inside this script)." >&2
  echo "$violations" >&2
  exit 1
fi

echo "check-bare-result: OK"
