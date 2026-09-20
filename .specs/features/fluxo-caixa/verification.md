# Fluxo de caixa verification

**Verdict**: PASS
**Profile**: light
**Diff range**: `27f0328061fe542abd7b36fa0d5904baa407629c..working-tree`
**Round**: 12 - full independent re-verification after the final proof updates
**Verifier**: independent sub-agent (author != verifier)

## Checks

| Check | Proof run | Evidence | Result |
| --- | --- | --- | --- |
| C1 | Batched `dotnet test` Core proof selecting the six named Core tests — exit 0; 6 passed, 0 failed | `backend/Core/tests/Core.IntegrationTests/LedgerEntryCreationTests.cs:46-61` asserts valid creation/default `America/Sao_Paulo`, current/past dates, positive/two-decimal/type/description/future-date validation and `422` cases. | PASS |
| C2 | Same Core batch — the named test passed | `backend/Core/tests/Core.IntegrationTests/LedgerEntryCreationTests.cs:74-92` asserts repeated same actor/key returns `200` and same id, changed intent returns `409`, different actor is isolated, and entry/attempt/audit/outbox counts increase only for the two real creations. | PASS |
| C3 | Same Core batch — the named test passed | `backend/Core/tests/Core.IntegrationTests/LedgerEntryConcurrencyTests.cs:22-33` asserts successful update, stale PUT and stale DELETE `409`, unchanged current amount/version, and identical before/after counts for entries, audits and outbox. | PASS |
| C4 | Same Core batch — the named test passed | `backend/Core/tests/Core.IntegrationTests/AuditTrailTests.cs:23-47` performs create/edit/logical delete, asserts `201/200/204`, three records, actor/correlation for each mutation, absence of `token`/`password`, and three persisted DB audit rows. | PASS |
| C5 | Batched `dotnet test` Summary proof selecting the four named Summary tests — exit 0; 4 passed, 0 failed | `backend/Summary/tests/Summary.IntegrationTests/ProjectionConsumerTests.cs:53-72` proves Core creation reaches Summary through the configured Rabbit fixture, Core has entry/audit/published outbox, duplicate EventId leaves one inbox row; `:74-84` proves older logical version does not overwrite version 2. | PASS |
| C6 | Same Summary batch — the named test passed | `backend/Summary/tests/Summary.IntegrationTests/DailySummaryFreshnessTests.cs:20-45` creates, edits and moves date, logically deletes, waits with a 30-second bound, asserts affected-day totals/balances, `current`, and `asOf` fresher than 30 seconds. | PASS |
| C7 | Same Summary batch — the named test passed | `backend/Summary/tests/Summary.IntegrationTests/DailySummaryAvailabilityTests.cs:12-28` asserts `503`/`unavailable`, seeds a summary with `AsOf` 31 seconds old and balance `10`, then asserts `stale`, balance `10`, and not `current`. | PASS |
| C8 | Same Core batch — the named test passed | `backend/Core/tests/Core.IntegrationTests/AuthenticationTests.cs:44-50` sends malformed, expired, wrong-issuer and wrong-audience tokens, asserts `401` for each, and asserts `(0, 0, 0)` entries/audits/outbox. | PASS |
| C9 | Same Core batch — the named test passed | `backend/Core/tests/Core.IntegrationTests/CurrentAuthorizationTests.cs:20-25` asserts `503`, exact configuration value `"2"`, zero entries/audits/outbox and a bounded observation around the timeout; production applies `CancelAfter` from the configured value at `backend/Core/src/Infrastructure/CurrentKeycloakAuthorization.cs:17-25`. | PASS |
| C10 | Core selector `...CurrentRoleTests.AppliesDisabledAccountAndRoleChangeOnTheNextRequestWithTheSameJwt` and Summary selector `...SummaryCurrentAuthorizationTests.AppliesCurrentAccountStateAndRoleToTheNextSummaryRequest` — exit 0; 1 Core and 1 Summary test passed | `backend/Core/tests/Core.IntegrationTests/CurrentAuthorizationTests.cs:72-80` asserts the same JWT changes from `200` to `403` after account disable and role change; `backend/Summary/tests/Summary.IntegrationTests/CurrentAuthorizationTests.cs:83-91` asserts the same behavior in Summary. | PASS |
| C11 | Exact command with `KEYCLOAK_URL=http://host.docker.internal:18081` and `--profile load` — Compose build/up/readiness succeeded; smoke k6 completed 1 s with 1029/1029 checks and 0% failures; `down -v` completed | `infra/compose/compose.yaml:2-121` and the command output show migrations plus all seven services healthy. The smoke exercised token acquisition, baseline, concentrated seed, current freshness and totals at `infra/load/daily-summary-50rps.js:49-70,99-120`. | PASS |
| C12 | Official result available in the task history/environment for the exact command `docker compose --file infra/compose/compose.yaml run --rm load k6 run --tag testid=FC12 infra/load/daily-summary-50rps.js` | Official 50 req/s for 5 minutes result: `failed_summary_responses = 1.59%` (threshold `<= 5%`), `summary_latency p95 = 52.645944 ms` (threshold `< 500 ms`), 0 interrupted iterations, 392 dropped iterations / 100 VUs. The workload and thresholds are declared at `infra/load/daily-summary-50rps.js:12-26`; concentrated-day seeding and delta checks are at `:65-112`. | PASS — official workload metrics satisfy both numeric obligations; dropped iterations are recorded as an operational caveat. |
| C13 | `npm --prefix front run test -- --runInBand front/tests/design-system-contract.test.tsx && npm --prefix front run build && npm --prefix front run test:e2e` — all green: 9 unit tests, production build, 2 Playwright tests | `front/tests/design-system-contract.test.tsx:11-44` checks applicable tokens, typography, component variants, operational states, responsive rules and touch targets; the build completed successfully and `front/tests/oidc.e2e.spec.ts:44-82` ran both E2E scenarios. | PASS |
| C14 | `python tools/validate_architecture.py --structure` — exit 0, `ARCHITECTURE VALIDATION: PASS` | `tools/validate_architecture.py:36-55` checks Core/Summary `Domain`, `Application`, `Infrastructure`, `Api` folders and forbidden dependency markers. | PASS |
| C15 | `python tools/validate_architecture.py --persistence` — exit 0, `ARCHITECTURE VALIDATION: PASS` | `tools/validate_architecture.py:57-71` checks versioned migrations, explicit `--migrate`/`MigrateAsync`, and migration Compose jobs. | PASS |
| C16 | `python tools/validate_architecture.py --observability` — exit 0, `ARCHITECTURE VALIDATION: PASS` | `tools/validate_architecture.py:74-87` checks OpenTelemetry/OTLP, collector traces/metrics/logs and sampling ratio. | PASS |
| C17 | `python tools/validate_architecture.py --identity-boundary` — exit 0, `ARCHITECTURE VALIDATION: PASS` | `tools/validate_architecture.py:90-99` checks distinct service credential markers and absence of Keycloak DB access. | PASS |
| C18 | `python tools/validate_architecture.py --identity-seed` — exit 0, `ARCHITECTURE VALIDATION: PASS` | `tools/validate_architecture.py:111-122` validates enabled `demo-admin`, `demo-operator`, `demo-auditor`, non-temporary credentials and local-only risk documentation. | PASS |
| C19 | `python tools/validate_architecture.py --compose-health` — exit 0, `ARCHITECTURE VALIDATION: PASS` | `tools/validate_architecture.py:125-138` checks healthchecks for all required services, `/readyz`, healthy dependency gates and migration completion ordering. | PASS |
| C20 | `python tools/validate_architecture.py --event-safety` — exit 0, `ARCHITECTURE VALIDATION: PASS` | `tools/validate_architecture.py:141-156` checks versioned event contract, forbidden credential fields and safe telemetry documentation. | PASS |
| C21 | `python tools/validate_architecture.py --docs` — exit 0, `ARCHITECTURE VALIDATION: PASS` | `tools/validate_architecture.py:159-163` checks the documented Compose command, migration, realm, reset, port, readiness and token terms. | PASS |
| C22 | `python tools/validate_frontend_architecture.py` — exit 0, `FRONTEND ARCHITECTURE VALIDATION: PASS` | The validator passed; `front/ARCHITECTURE.md` and the validator enforce the planned `app/features/components/lib` layout and reject prohibited Clean/Hexagonal frontend layers. | PASS |
| C23 | `dotnet test backend/tests/ArchitectureTests/ArchitectureTests.csproj --filter "FullyQualifiedName~BoundedContextDependencyTests"` — exit 0; 1 passed, 0 failed in `CashFlowArchitectureContractTests.dll` | `backend/tests/ArchitectureTests/ContractTests.cs:45-61` asserts independent DbContexts, `ProjectionEvent`, absence of cross-context entities and the versioned event contract. | PASS |

## Validation

| Command | Result |
| --- | --- |
| `python C:/Users/gilbe/.codex/skills/tlc-spec-lean/scripts/validate_plan.py fluxo-caixa` | exit 0; 0 errors, 16 warnings |
| `python C:/Users/gilbe/.codex/skills/tlc-spec-lean/scripts/validate_checks.py fluxo-caixa` | exit 0; 0 errors, 11 warnings; profile `light` |
| `python C:/Users/gilbe/.codex/skills/tlc-spec-lean/scripts/validate_verification.py fluxo-caixa` | run after this report; expected exit 0 |

## Coverage

Coverage was not recomputed under the approved `light` profile. The authored Coverage table in `checks.md` has no `Unproven` members, but this verifier did not upgrade that self-report to an independent Coverage recomputation.

## Faults injected

Not run: fault injection is not required by the approved `light` profile.

## Ranked gaps

None. All 23 checks have passing proof results with located evidence.

## Gate

`python C:/Users/gilbe/.codex/skills/tlc-spec-lean/scripts/validate_verification.py fluxo-caixa` — expected exit 0: all check rows are PASS and no gate contradiction remains.
