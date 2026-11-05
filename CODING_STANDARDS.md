# Coding standards (reviewer-facing)

Enforced mechanically by `scripts/check-bare-result.sh` (non-generic Result factories
are only legal where a non-generic Result is the declared type), which CI runs. The
rules below are judgement calls no grep can express; a reviewer applies them to every
diff touching the named surfaces.

## Pin external-protocol tests to the spec, not to the implementation

A test for an external protocol — payment gateway signing, SMTP dialect, webhook
payload — must derive its expected values from the protocol's own documentation. An
"independent" helper that shares one assumption with the code under test cannot catch a
wrong assumption: the VNPay HMAC helper hashed URL-encoded values because the service
did, so the whole suite stayed green while both deviated from the VNPay spec. When the
spec's exact example bytes are unavailable, name the unverified assumption in the test
and in TV2-DELIVERY.md.

## One EF database per test factory, no racing workers

EF InMemory databases are global by name across service providers, so two factories
sharing a name observe each other's rows. Name every InMemory database uniquely per
factory (for example `orders-tests-{Guid:N}`). Gate background workers (retry loops,
outbox delivery) behind a config flag so assertions observe state that only the test
itself produced; the seeding the global setup performs through the real API is the one
allowed exception.
