# Estado das decisões

## Decisions

| ID | Status | Decisão | Fonte |
|---|---|---|---|
| ADR-001 | active | Dois processos backend: Core.Api e Summary.Api; frontend separado | `.design/ADRs/ADR-001-ARQUITETURA-APLICACAO.md` |
| ADR-002 | active | Uma instância PostgreSQL com bancos lógicos separados; EF Core por contexto | `.design/ADRs/ADR-002-PERSISTENCIA-POSTGRES-EFCORE.md` |
| ADR-003 | active | Keycloak, token de 1 hora, autorização online e timeout de 2 s fail-closed | `.design/ADRs/ADR-003-SEGURANCA-KEYCLOAK.md` |
| ADR-004 | active | OpenTelemetry, Collector, New Relic e eventos versionados | `.design/ADRs/ADR-004-OBSERVABILIDADE-E-EVENTOS.md` |
| ADR-005 | active | Clean Architecture pragmática com ports e adapters hexagonais | `.design/ADRs/ADR-005-CLEAN-HEXAGONAL-BACKEND.md` |

## Handoff

RFC e ADRs aprovados. Próximo trabalho: criar o scaffold do monorepo conforme `RFC-001` e,
depois, escrever o plano da feature no formato `tlc-spec-lean` antes de implementar código.
