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

## Observable

| Surface | Decisions |
|---|---|
| Web views | loading, empty, error e unauthorized; estados `current`, `stale` e `unavailable`; confirmação para exclusão; composição conforme `DESIGN_SYSTEM.md` |
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

## Sources

- `../../design/Discoveries/DISCOVERY-001-FLUXO-CAIXA.md`
- `../../design/ADRs/ADR-001-ARQUITETURA-APLICACAO.md`
- `../../design/ADRs/ADR-002-PERSISTENCIA-POSTGRES-EFCORE.md`
- `../../design/ADRs/ADR-003-SEGURANCA-KEYCLOAK.md`
- `../../design/ADRs/ADR-004-OBSERVABILIDADE-E-EVENTOS.md`
- `../../design/ADRs/ADR-005-CLEAN-HEXAGONAL-BACKEND.md`
- `../../design/Frontend/DESIGN_SYSTEM.md`
