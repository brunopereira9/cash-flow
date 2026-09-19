# ADR-004-OBSERVABILIDADE-E-EVENTOS

Status: aprovado  
Origem: [RFC-001-ARQUITETURA-FLUXO-CAIXA](../RFCs/RFC-001-ARQUITETURA-FLUXO-CAIXA.md)

## Decisão

Usar OpenTelemetry nas APIs e consumidores, Collector em Docker e exportação para New Relic.
Durante carga e testes de falha, traces terão amostragem de 100%; em operação normal, a
amostragem será configurável. Métricas e logs de erro serão preservados.

Os eventos serão versionados, por exemplo `LedgerEntryCreated.v1`, `LedgerEntryUpdated.v1` e
`LedgerEntryDeleted.v1`, com contratos em `contracts/events`. O fluxo usará outbox, RabbitMQ,
inbox e confirmação após persistência da projeção.

O Summary exporá `asOf` e `freshnessStatus` (`current`, `stale` ou `unavailable`).

## Consequências

Falhas, atraso e replay tornam-se observáveis sem registrar tokens, senhas, segredos, valores
financeiros brutos ou dados pessoais desnecessários.
