# ADR-005-CLEAN-HEXAGONAL-BACKEND — Arquitetura interna do backend

Status: aprovado  
Origem: [RFC-002-ARQUITETURA-INTERNA-BACKEND](../RFCs/RFC-002-ARQUITETURA-INTERNA-BACKEND.md)

## Contexto

`Core.Api` e `Summary.Api` precisam manter regras de negócio independentes de EF Core, ASP.NET,
Keycloak, RabbitMQ e OpenTelemetry, sem criar uma quantidade artificial de projetos para o teste.

## Decisão

Adotar **Clean Architecture pragmática com portas e adaptadores hexagonais**.

Cada processo terá as áreas:

```text
src/
├── Domain/
├── Application/
├── Infrastructure/
└── Api/
```

`Domain` não depende de frameworks ou infraestrutura. `Application` contém casos de uso e portas.
`Infrastructure` implementa adapters de EF Core, Keycloak, RabbitMQ e OpenTelemetry. `Api`
contém endpoints, consumers, composição de dependências e configuração do host.

Não haverá projeto compartilhado de entidades entre `Core.Api` e `Summary.Api`. A divisão interna
por módulos (`Ledger`, `Identity`, `Audit`, `Integration`, `Projection` e `Query`) será feita
quando houver responsabilidade real, sem criar uma classe ou projeto por entidade.

## Consequências

As dependências podem ser verificadas por testes de arquitetura, os casos de uso podem ser
testados sem infraestrutura e os adaptadores podem evoluir sem alterar o domínio. A disciplina
de ports/adapters é obrigatória para evitar que a estrutura Clean vire apenas organização de
pastas.
