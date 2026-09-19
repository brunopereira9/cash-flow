# Fluxo de caixa checks

Profile: light
Plan: `.specs/features/fluxo-caixa/plan.md`

13 checks em 4 slices · 6 one-way doors · 0 questões abertas.

## Checks

### S1 - Ledger transacional e auditoria

**C1** - Uma criação aceita somente valor positivo com duas casas, tipo, descrição e data em `America/Sao_Paulo` não posterior a hoje; omitir a data usa o dia atual.
Proof: `dotnet test backend/Core/tests/Core.IntegrationTests/Core.IntegrationTests.csproj --filter FullyQualifiedName~LedgerEntryCreationTests.AcceptsValidEntryAndDefaultsBusinessDate`

**C2** - Repetir `POST /ledger/entries` com a mesma chave e a mesma intenção retorna o lançamento original sem novo lançamento, auditoria ou outbox; a mesma chave com intenção diferente retorna `409`.
Proof: `dotnet test backend/Core/tests/Core.IntegrationTests/Core.IntegrationTests.csproj --filter FullyQualifiedName~LedgerEntryCreationTests.IsIdempotentByActorAndAttemptKey`

**C3** - `PUT` ou `DELETE` com versão desatualizada responde `409` e não muda `LedgerEntry`, `AuditRecord` ou `OutboxEvent`.
Proof: `dotnet test backend/Core/tests/Core.IntegrationTests/Core.IntegrationTests.csproj --filter FullyQualifiedName~LedgerEntryConcurrencyTests.RejectsStaleVersionWithoutSideEffects`

**C4** - Criação, edição e exclusão lógica persistem um `AuditRecord` com ator autenticado e correlação, sem senha ou token.
Proof: `dotnet test backend/Core/tests/Core.IntegrationTests/Core.IntegrationTests.csproj --filter FullyQualifiedName~AuditTrailTests.PersistsBusinessMutationWithoutSecrets`

### S2 - Eventos e consolidado

**C5** - A transação confirmada grava `LedgerEntry`, `AuditRecord` e `OutboxEvent`; a projeção aplica cada `EventId` e versão lógica no máximo uma vez.
Proof: `dotnet test backend/Summary/tests/Summary.IntegrationTests/Summary.IntegrationTests.csproj --filter FullyQualifiedName~ProjectionConsumerTests.DeduplicatesRepeatedDelivery`

**C6** - Com Core, RabbitMQ e Summary saudáveis, criar, editar, mover de data ou excluir um lançamento atualiza os totais e o saldo do dia afetado em até 30 segundos.
Proof: `dotnet test backend/Summary/tests/Summary.IntegrationTests/Summary.IntegrationTests.csproj --filter FullyQualifiedName~DailySummaryFreshnessTests.ReflectsConfirmedLedgerMutationWithinThirtySeconds`

**C7** - `GET /summary/daily/{date}` responde `stale` para projeção com atraso acima de 30 segundos e `unavailable` quando a projeção não pode ser consultada, sem apresentar saldo antigo como `current`.
Proof: `dotnet test backend/Summary/tests/Summary.IntegrationTests/Summary.IntegrationTests.csproj --filter FullyQualifiedName~DailySummaryAvailabilityTests.NeverLabelsStaleOrUnavailableBalanceAsCurrent`

### S3 - Autorização e execução local

**C8** - Token inválido, expirado, com issuer incorreto ou audience incorreta recebe `401` sem mutação.
Proof: `dotnet test backend/Core/tests/Core.IntegrationTests/Core.IntegrationTests.csproj --filter FullyQualifiedName~AuthenticationTests.RejectsInvalidJwtWithoutMutation`

**C9** - Falha ou timeout de 2 segundos ao consultar o estado atual no Keycloak recebe `503` sem iniciar operação, auditoria ou outbox.
Proof: `dotnet test backend/Core/tests/Core.IntegrationTests/Core.IntegrationTests.csproj --filter FullyQualifiedName~CurrentAuthorizationTests.FailsClosedWhenKeycloakCannotConfirmState`

**C10** - A requisição seguinte após desativação de conta ou alteração de papel aplica o estado atual do Keycloak nas APIs Core e Summary, mesmo com JWT antigo.
Proof: `dotnet test backend/tests/ArchitectureTests/ArchitectureTests.csproj --filter FullyQualifiedName~CurrentAuthorizationContractTests.EnforcesCurrentKeycloakStateInBothApis`

**C11** - O Compose sobe frontend, Core, Summary, PostgreSQL, RabbitMQ, Keycloak e Collector; migrations, realm seed e reset são comandos documentados e explícitos.
Proof: `dotnet test backend/tests/ArchitectureTests/ArchitectureTests.csproj --filter FullyQualifiedName~LocalEnvironmentContractTests.DeclaresAllRequiredComposeServicesAndCommands`

### S4 - Carga e interface

**C12** - `GET /summary/daily/{date}` sob 50 req/s durante 5 minutos, com a massa de dia concentrado, mantém respostas malsucedidas em no máximo 5% e p95 abaixo de 500 ms.
Proof: `docker compose --file infra/compose/compose.yaml run --rm load k6 run --tag testid=FC12 infra/load/daily-summary-50rps.js`

**C13** - As views web usam os tokens, componentes, tipografia, cores, espaçamento e estados definidos em `.design/Frontend/DESIGN_SYSTEM.md`.
Proof: `npm --prefix front run test -- --runInBand front/tests/design-system-contract.test.tsx`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /ledger/entries` statuses (7) | `201` C1 · `200` C2 · `401` C8 · `403` C10 · `409` C2 · `422` C1 · `503` C9 | - |
| `GET /ledger/entries` statuses (4) | `200` C1 · `401` C8 · `403` C10 · `503` C9 | - |
| `PUT /ledger/entries/{id}` statuses (7) | `200` C6 · `401` C8 · `403` C10 · `404` C3 · `409` C3 · `422` C1 · `503` C9 | - |
| `DELETE /ledger/entries/{id}` statuses (6) | `204` C6 · `401` C8 · `403` C10 · `404` C3 · `409` C3 · `503` C9 | - |
| `GET /summary/daily/{date}` statuses (5) | `200` C6 · `401` C8 · `403` C10 · `404` C7 · `503` C7 | - |
| `GET /audit` statuses (4) | `200` C4 · `401` C8 · `403` C10 · `503` C9 | - |
| `POST/PUT /identity/users` statuses (7) | `200` C10 · `201` C10 · `401` C8 · `403` C10 · `409` C10 · `422` C10 · `503` C9 | - |
| event delivery (2) | outbox transaction C5 · inbox deduplication C5 | - |
| freshness states (3) | `current` C6 · `stale` C7 · `unavailable` C7 | - |
| authorization failure modes (4) | invalid token C8 · expired token C8 · issuer mismatch C8 · audience mismatch C8 | - |
| one-way process boundary (2) | Core owns writes C5 · Summary owns projection C5 | - |
| databases (3) | `core_db` C5 · `summary_db` C5 · `keycloak_db` C11 | - |
| event contract versions (3) | created `v1` C5 · updated `v1` C5 · deleted `v1` C5 | - |

## Swept

- validation: C1
- failure modes: C7, C9
- idempotency: C2, C5
- authorization: C8, C9, C10
- concurrency: C3
- data lifecycle: C3, C4
- dependency failure: C7, C9
- state transitions: C3, C5, C6
- observability: C4, C6, C7, C12

## Handoff

- S1-S2 touch Core and Summary contracts; S3 adds Keycloak and Compose; S4 adds load tooling and frontend. No code slice has started.
