# ADR-002-PERSISTENCIA-POSTGRES-EFCORE

Status: aprovado  
Origem: [RFC-001-ARQUITETURA-FLUXO-CAIXA](../RFCs/RFC-001-ARQUITETURA-FLUXO-CAIXA.md)

## Decisão

Usar uma única instância PostgreSQL no Compose, com os bancos lógicos `core_db`, `summary_db` e
`keycloak_db`. `Core.Api` acessa apenas `core_db`; `Summary.Api`, apenas `summary_db`; e o
Keycloak, apenas `keycloak_db`.

EF Core será o padrão de persistência. Cada contexto terá `DbContext` e migrations próprios,
versionados e aplicados por comando explícito. Não haverá foreign key entre bancos nem modelo EF
compartilhado entre processos.

## Consequências

A instância única reduz a operação local, mas é um ponto único de falha e não representa alta
disponibilidade. A separação lógica preserva ownership e a integração assíncrona por eventos.
