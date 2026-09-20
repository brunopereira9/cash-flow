# Plano — fluxo-caixa

## Problem

O comerciante precisa registrar créditos e débitos e consultar o saldo diário, mas o sistema
ainda não existe. A entrega precisa demonstrar escrita transacional independente do consolidado,
projeção assíncrona recuperável, autorização efetiva e execução local reproduzível.

## Criteria

Acceptance Criteria

1. **FC-001** O sistema SHALL aceitar lançamento com valor positivo de duas casas decimais, tipo, descrição e data hoje ou passada em `America/Sao_Paulo`.
2. **FC-002** WHEN uma tentativa de criação válida for repetida com a mesma chave e intenção THEN o sistema SHALL devolver o lançamento original sem novo lançamento, auditoria ou evento.
3. **FC-003** IF uma edição ou exclusão usar versão diferente da vigente THEN o sistema SHALL responder conflito sem alterar lançamento, auditoria ou outbox.
4. **FC-004** O sistema SHALL registrar criação, edição e exclusão lógica em auditoria persistente com ator autenticado e correlação, sem senha ou token.
5. **FC-005** O sistema SHALL publicar mudanças confirmadas por outbox e aplicar cada evento no consolidado no máximo uma vez por versão lógica, mesmo com entrega repetida.
6. **FC-006** WHEN uma alteração confirmada ocorrer com componentes saudáveis THEN o saldo correspondente SHALL aparecer em até 30 segundos.
7. **FC-007** IF o consolidado estiver atrasado ou indisponível THEN a consulta SHALL indicar `stale` ou `unavailable` e não classificar saldo antigo como atual.
8. **FC-008** IF o token for inválido, expirado, de emissor/audience incorretos THEN a API SHALL responder `401` sem mutação.
9. **FC-009** IF o estado atual do usuário/papel não puder ser confirmado no Keycloak THEN a API SHALL responder `503` sem iniciar operação, auditoria ou outbox.
10. **FC-010** WHEN a conta for desativada ou o papel alterado THEN a próxima requisição SHALL usar o estado novo mesmo com token antigo.
11. **FC-011** O sistema SHALL executar localmente por Docker Compose com frontend, APIs, PostgreSQL, RabbitMQ, Keycloak e Collector configurados.
12. **FC-012** Durante carga de `GET daily-summary` a 50 req/s por 5 minutos, o sistema SHALL manter no máximo 5% de respostas malsucedidas e p95 inferior a 500 ms.
13. **FC-013** As telas do frontend SHALL usar os tokens, componentes, tipografia, cores, espaçamento e estados definidos em `.design/Frontend/DESIGN_SYSTEM.md`.
14. **FC-014** WHEN uma pessoa não autenticada acessar uma tela protegida THEN o frontend SHALL iniciar login OIDC no Keycloak usando Authorization Code + PKCE (S256).
15. **FC-015** WHEN o callback OIDC for válido THEN o frontend SHALL estabelecer a sessão em memória e acessar as APIs usando o access token de uma hora; IF o callback falhar THEN SHALL exibir erro sem iniciar operação de negócio.
16. **FC-016** Cada processo backend SHALL seguir a estrutura `src/Domain`, `src/Application`, `src/Infrastructure` e `src/Api`; regras de domínio/aplicação não poderão depender de EF Core, ASP.NET, RabbitMQ, Keycloak ou OpenTelemetry.
17. **FC-024** O frontend SHALL organizar `auth`, `summary`, `ledger`, `audit` e `users` em `src/features/<feature>`, mantendo tela, API, tipos e hooks próximos; integrações compartilhadas ficam em `src/lib`, componentes em `src/components` e rotas/providers em `src/app`.
18. **FC-025** O backend SHALL separar os bounded contexts DDD `Ledger`, `Identity/Access` e `Summary/Projection`, cada um com linguagem ubíqua, entidades/agregados, value objects, invariantes, casos de uso e portas próprias; contextos não poderão compartilhar entidades EF ou DTOs nem depender diretamente uns dos outros.
18. **FC-017** Core e Summary SHALL usar migrations EF Core próprias, versionadas no repositório e aplicadas por comando explícito; o runtime não poderá depender de `EnsureCreated`.
18. **FC-018** As APIs e consumidores SHALL configurar OpenTelemetry com traces, métricas e logs exportáveis pelo Collector; amostragem de carga/teste deverá ser 100% e a normal deverá ser configurável.
19. **FC-019** Core, Summary e administração de usuários SHALL usar credenciais de serviço separadas, com ownership documentado e sem consultar tabelas internas do Keycloak.
20. **FC-020** O seed local SHALL criar contas demo distintas para `admin`, `operator` e `auditor`, com credenciais inseguras explicitamente documentadas como somente desenvolvimento.
21. **FC-021** O Compose SHALL expor health/readiness verificável para PostgreSQL, RabbitMQ, Keycloak, Core, Summary, Collector e frontend; o comando documentado deverá aguardar dependências saudáveis antes dos testes.
22. **FC-022** Os eventos SHALL obedecer aos contratos versionados em `contracts/events`, e as métricas/logs de outbox, RabbitMQ, inbox, Keycloak e projeção não poderão registrar tokens, senhas, segredos ou valores financeiros brutos.
23. **FC-023** A documentação operacional SHALL refletir as portas e comandos reais do Compose, incluindo migrations, seed, reset, token demo e validação de health.

## Observable

### Frontend feature matrix

| Feature | Scope | Access |
|---|---|---|
| `auth` | login OIDC PKCE, callback, logout, loading, erro e unauthorized | todos |
| `summary` | créditos, débitos, saldo diário, `asOf`, `current`, `stale`, `unavailable` | admin/operator/auditor leitura |
| `ledger` | listar/paginar/filtrar, criar, editar, exclusão lógica, idempotência e conflito de versão | admin/operator R/W; auditor leitura |
| `audit` | consulta paginada por ator, operação, período e correlação, com before/after sem segredos | admin/auditor leitura |
| `users` | listar, cadastrar, editar nome/e-mail/papel, desativar/reativar e proteger último admin | admin |

Fora desta entrega: restauração, exclusão definitiva, redefinição/troca obrigatória de senha e convite por e-mail.

### Backend DDD context map

| Contexto | Responsabilidade | Regra de fronteira | Evidência |
|---|---|---|---|
| `Ledger` | lançamentos, idempotência, versão e exclusão lógica | publica contratos de evento; não importa tipos de Identity/Summary | teste de dependência + casos de uso/invariantes |
| `Identity/Access` | usuários, papéis e autorização atual via Keycloak | expõe portas de autorização; não acessa tabelas do Keycloak nem entidades Ledger | teste de autorização atual + teste de dependência |
| `Summary/Projection` | inbox, projeção e resumo diário | consome eventos versionados; não referencia persistência ou entidades do Core | teste de replay/frescor + teste de dependência |

Core pode hospedar `Ledger` e `Identity/Access` no mesmo processo, porém em módulos/pastas
independentes. Summary permanece processo e contexto separado.

| Surface | Decisions |
|---|---|
| Web views | login, callback válido, erro de autenticação, loading, empty, error e unauthorized; estados `current`, `stale` e `unavailable`; confirmação para exclusão; composição conforme `DESIGN_SYSTEM.md` |
| Core API | payload de sucesso e erro com códigos; `401`, `403`, `409`, `422`, `503`; autorização por papel e rate limit conforme configuração local |
| Summary API | consulta diária com `asOf` e frescor; `401`, `403`, `404`, `503`; sem acesso ao banco do Core |
| RabbitMQ consumer | ack somente após transação; retry e mensagem rejeitada observáveis |
| Compose/demo | comando de subida, migrations explícitas, seed de Keycloak e reset documentado |

## Assumptions

| Assumption | Default | Rationale | Confirmed? |
|---|---|---|---|
| Timeout de autorização | `2 s`, configurável, fail-closed | ADR-003 aprovado | y |
| Access token | 1 hora | decisão aprovada pelo usuário | y |
| Banco local | uma instância PostgreSQL com três databases lógicos | ADR-002 aprovado | y |
| Arquitetura interna | Clean pragmática com ports/adapters hexagonais | ADR-005 aprovado | y |
| Fonte visual do frontend | `.design/Frontend/DESIGN_SYSTEM.md` | guia oficial do design system | y |
| Senhas demo | `username123`, somente local | requisito deliberado de demonstração | y |
| Retenção e volume de produção | n/a - não fornecidos pelo enunciado | fora do teste | n |

Open questions: none. As parametrizações restantes não bloqueiam a interpretação dos critérios.

## Out of scope

- múltiplos tenants ou comerciantes;
- saldo acumulado;
- restauração de lançamento excluído;
- redefinição e troca obrigatória de senha;
- alta disponibilidade e infraestrutura de produção;
- Event Sourcing;
- separação de Ledger e Identity em processos próprios;
- replay arbitrário sob múltiplos consumidores.

## Flow

1. `front` autentica no Keycloak e chama `Core.Api`.
2. `Core.Api` valida JWT, consulta autorização atual e executa casos de uso do módulo Ledger.
3. Ledger grava lançamento, auditoria e outbox em `core_db` na mesma transação.
4. Publisher publica evento versionado no RabbitMQ com confirmação.
5. `Summary.Api` consome, grava inbox e projeção em `summary_db` e confirma a mensagem após commit.
6. `front` consulta `Summary.Api`, que retorna totais, `asOf` e estado de frescor.

## Relations

- `LedgerEntry` possui zero ou muitos `AuditRecord`.
- `LedgerEntry` produz zero ou muitos `OutboxEvent`.
- Cada `OutboxEvent` pode ser registrado uma vez em `InboxEvent` por consumidor.
- Cada `LedgerEntry` possui no máximo uma representação vigente em `ProjectedEntry`.
- `ProjectedEntry` contribui para um `DailySummary` por data de negócio.
- `CreationAttempt` pertence ao usuário e identifica uma tentativa idempotente.
- `AuditRecord.ActorId` referencia o `sub` do Keycloak sem duplicar identidade local.

## Surface

| Route | In | Out | Statuses |
|---|---|---|---|
| `POST /ledger/entries` | lançamento + chave de tentativa | lançamento criado/existente | `201`, `200`, `401`, `403`, `409`, `422`, `503` |
| `GET /ledger/entries` | filtros e paginação | lançamentos ativos | `200`, `401`, `403`, `503` |
| `PUT /ledger/entries/{id}` | campos editáveis + versão | lançamento atualizado | `200`, `401`, `403`, `404`, `409`, `422`, `503` |
| `DELETE /ledger/entries/{id}` | versão | confirmação | `204`, `401`, `403`, `404`, `409`, `503` |
| `GET /summary/daily/{date}` | data | totais, saldo, `asOf`, frescor | `200`, `401`, `403`, `404`, `503` |
| `GET /audit` | filtros e paginação | registros de auditoria | `200`, `401`, `403`, `503` |
| `POST/PUT /identity/users` | dados de usuário/papel | usuário sem segredo | `200`, `201`, `401`, `403`, `409`, `422`, `503` |

## Landing

| Door | Literal shape | Rejected alternative |
|---|---|---|
| Process boundary | `Core.Api` com Ledger/Identity e `Summary.Api` separado | três serviços agora: complexidade maior sem benefício proporcional |
| Persistence boundary | `core_db`, `summary_db`, `keycloak_db` em uma instância PostgreSQL local | banco compartilhado entre Core e Summary: permitiria acoplamento por tabela |
| Integration delivery | outbox + RabbitMQ + inbox, entrega pelo menos uma vez | escrita direta no Summary: acoplaria disponibilidade e perderia recuperação |
| Authorization | JWT local + leitura online do usuário/papéis no Keycloak; erro `503` | confiar apenas nas claims: token antigo preservaria privilégio revogado |
| Internal architecture | Clean pragmática + ports/adapters | Clean rígida por dezenas de projetos: cerimônia sem valor para o escopo |
| Event contract | nomes versionados `LedgerEntryCreated.v1` etc. em `contracts/events` | DTOs de domínio compartilhados: acoplamento entre processos |

## Impact

| Area | Impact |
|---|---|
| Terms | `LedgerEntry`, `DailySummary`, `freshnessStatus`, `CreationAttempt` e papéis `admin/operator/auditor` passam a ser nomes públicos do sistema |
| Existing data | n/a - não há banco de aplicação existente; migrations iniciarão bases vazias |
| Operations | Docker Compose local terá ponto único de falha; segredos e credenciais demo não podem ser tratados como produção |
| Verification | requisitos de carga, autorização imediata, idempotência, replay e frescor exigirão testes de integração e evidência executável |
| Frontend visual language | `.design/Frontend/DESIGN_SYSTEM.md` é a fonte normativa para tokens e componentes; divergências específicas precisam ser justificadas |

## Traceability

| Criterion | Landing/Surface/Flow |
|---|---|
| FC-001 | Surface: `POST /ledger/entries`; Flow: Ledger |
| FC-002 | Landing: integração outbox/inbox; Surface: criação |
| FC-003 | Relations: `LedgerEntry.Version`; Surface: PUT/DELETE |
| FC-004 | Relations: `AuditRecord`; Surface: auditoria |
| FC-005 | Flow: outbox → RabbitMQ → Summary; Landing: entrega pelo menos uma vez |
| FC-006 | Surface: Summary; Impact: frescor |
| FC-007 | Surface: `GET /summary/daily/{date}` |
| FC-008 | Landing: autorização; Surface: todas as APIs |
| FC-009 | Landing: autorização online; Surface: todas as APIs |
| FC-010 | Landing: autorização online; Flow: Keycloak → APIs |
| FC-011 | Flow: Compose; Impact: Operations |
| FC-012 | Impact: Verification; Surface: Summary |
| FC-013 | Surface: Web views; Impact: Frontend visual language |
| FC-014 | Surface: Web views; Flow: front → Keycloak |
| FC-015 | Surface: Web views; Landing: Authorization |
| FC-016 | ADR-005/RFC-002: estrutura Clean + ports/adapters; `tools/validate_architecture.py` |
| FC-017 | ADR-002/RFC-001: DbContext e migrations por processo; comando explícito de infraestrutura |
| FC-018 | ADR-004: OpenTelemetry, Collector, traces/métricas/logs e amostragem |
| FC-019 | ADR-003: credenciais separadas e ACL para Keycloak |
| FC-020 | ADR-003: contas locais admin/operator/auditor e documentação de risco |
| FC-021 | ADR-004/RFC-001: execução local reproduzível e observável |
| FC-022 | ADR-004: contratos versionados e telemetria sem dados sensíveis |
| FC-023 | ADR-002/003/RFC-001: comandos e endpoints operacionais coerentes |
| FC-024 | `front/ARCHITECTURE.md`: features próximas, integrações em `lib` e sem camadas hexagonais artificiais |
| FC-025 | ADR-005/RFC-002: bounded contexts DDD, linguagem ubíqua, portas próprias e ausência de entidades/DTOs compartilhados |

## Sources

- `../../design/Discoveries/DISCOVERY-001-FLUXO-CAIXA.md`
- `../../design/ADRs/ADR-001-ARQUITETURA-APLICACAO.md`
- `../../design/ADRs/ADR-002-PERSISTENCIA-POSTGRES-EFCORE.md`
- `../../design/ADRs/ADR-003-SEGURANCA-KEYCLOAK.md`
- `../../design/ADRs/ADR-004-OBSERVABILIDADE-E-EVENTOS.md`
- `../../design/ADRs/ADR-005-CLEAN-HEXAGONAL-BACKEND.md`
- `../../design/Frontend/DESIGN_SYSTEM.md`

## Conformity gate

Before any feature verification is accepted, run `python tools/validate_architecture.py`.
The command is intentionally allowed to fail while the correction slice is in progress; its
violations are obligations, not warnings. A final green result requires zero violations and an
independent verifier must rerun it after implementation.

## Next execution slice — full-stack feature completion

This slice is planned with `tlc-spec-lean`: obligations are frozen before implementation;
the builder must not substitute frontend Clean/Hexagonal layers for the simple feature layout.

### Frozen obligations and contracts

| Surface | EARS obligation | Contract/entities | Authorization |
|---|---|---|---|
| `auth/login` | WHEN unauthenticated THEN start OIDC Authorization Code + PKCE S256; WHEN callback is invalid THEN show error and perform no business operation | `/authorize`, `/token`, `/logout`; in-memory session, state, verifier | all users |
| `summary` | WHEN requested THEN show daily credits, debits, balance, `asOf`; IF projection is delayed/unavailable THEN show `stale`/`unavailable` | `GET /summary/daily/{date}`; `DailySummary` | admin/operator/auditor read |
| `ledger` | WHEN valid create repeats same intent THEN return original; IF version is stale THEN return conflict; delete is logical | `POST/GET/PUT/DELETE /ledger/entries`; `LedgerEntry`, `CreationAttempt`, `AuditRecord`, `OutboxEvent` | admin/operator write; auditor read |
| `audit` | WHEN queried THEN paginate/filter by actor, operation, period and correlation without secrets | `GET /audit`; `AuditRecord` with before/after redacted | admin/auditor read |
| `users` | WHEN admin changes identity THEN persist through Keycloak Admin API; IF disabling last admin THEN reject | `POST/PUT /identity/users`; `UserSummary`, role and active state | admin only |

### Required implementation boundaries

- Backend keeps `Domain`, `Application`, `Infrastructure`, `Api` per process; domain/application
  cannot reference EF Core, ASP.NET, RabbitMQ, Keycloak or OpenTelemetry.
- Core owns ledger writes, audit and outbox in one transaction; Summary owns inbox and projection.
- Identity uses Keycloak Admin API and separate service credentials; no Keycloak database access.
- Frontend uses only `src/features/{auth,summary,ledger,audit,users}`, `src/lib`,
  `src/components` and `src/app`.
- Every mutation has an independent integration proof; every protected route has role and
  current-authorization proof; every async path has replay/freshness proof.

### Verification deliverables

1. Extend `tools/validate_architecture.py` and `tools/validate_frontend_architecture.py` to
   enforce the boundaries above and emit actionable violations.
2. Add independent backend integration tests for CRUD/version/idempotency/audit, current
   Keycloak authorization, user lifecycle and last-admin protection.
3. Add independent frontend tests for feature layout, OIDC states, role visibility, summary
   freshness states, ledger conflict/confirmation and audit redaction.
4. Create and execute Playwright plans covering login, summary, ledger mutation, audit and
   admin users at desktop/mobile widths, including console/network error assertions.
5. A fresh verifier reruns all checks, validators and Playwrights, updates `verification.md`,
   and the final gate is blocked until all required evidence is green.

### DDD verification additions

- Add a backend architecture test that fails if `Ledger`, `Identity/Access` or
  `Summary/Projection` imports another context's domain/entity/EF/DTO types.
- Require each context to expose its own domain model, application use cases and ports, with
  adapters isolated under Infrastructure.
- Require event contracts to cross contexts instead of direct object references.
- Record independent evidence in the verification report, including the exact dependency-test
  command and its passing output.

## Execution roadmap

| Slice | Scope | Depends on | Done when |
|---|---|---|---|
| E1 | Backend context skeleton: `Ledger`, `Identity/Access`, `Summary/Projection`, domain models, value objects, ports and adapters | FC-025 | architecture dependency test passes and solution builds |
| E2 | Ledger use cases: create/list/update/logical-delete, idempotency, optimistic version and audit/outbox transaction | E1 | integration tests prove success, conflict, replay safety and no side effects |
| E3 | Identity/Access: current authorization, user CRUD through Keycloak Admin API, role rules, last-admin protection and separate credentials | E1 | integration tests prove admin/operator/auditor matrix and fail-closed behavior |
| E4 | Summary/Projection: versioned event contracts, inbox deduplication, projection freshness and daily query | E1, E2 | Core-to-Rabbit-to-Summary test proves eventual consistency and replay behavior |
| E5 | Frontend `auth` and `summary`: login/callback/logout, session states and daily freshness states | E3, E4 | component tests and Playwright cover success, loading, error, unauthorized, stale and unavailable |
| E6 | Frontend `ledger`: paginated/filterable list, create/edit/delete confirmation, conflict and idempotency feedback | E2, E5 | component tests and Playwright prove mutation and failure states |
| E7 | Frontend `audit` and `users`: filters, redacted before/after, admin-only management and last-admin error | E3, E5 | role visibility tests, redaction tests and Playwright admin/operator/auditor journeys pass |
| E8 | Operational conformance: migrations, healthchecks, OpenTelemetry/New Relic, contracts, docs and architectural validators | E1-E7 | architecture validators pass with zero violations and Compose readiness is executable |
| E9 | Independent verification and audit: fresh verifier reruns checks, Playwright plans and evidence review | E8 | `verification.md` is PASS and `validate_verification.py` exits 0 |

### Delegation and evidence rules

- Each slice receives one builder and a separate verifier; the verifier must not be the builder.
- A slice cannot be marked complete from compilation alone: it needs its named behavioral proof.
- Playwright plans must be created before execution and must record viewport, seed, route,
  expected UI states, network/console assertions and cleanup.
- Validator failures become tracked obligations in this plan, not warnings to suppress.
- E9 must start from a fresh context and may reopen any earlier slice that lacks independent
  evidence.
