# ADR-001-ARQUITETURA-APLICACAO

Status: aprovado  
Origem: [RFC-001-ARQUITETURA-FLUXO-CAIXA](../RFCs/RFC-001-ARQUITETURA-FLUXO-CAIXA.md)

## Contexto

O fluxo de caixa precisa separar escrita transacional, projeção de leitura e identidade sem
introduzir complexidade operacional desnecessária para a demonstração.

## Decisão

Adotar dois processos backend: `Core.Api`, com os módulos `Ledger`, `Identity`, `Audit` e
`Integration`, e `Summary.Api`, com `Projection` e `Query`. O frontend será uma aplicação
independente em `front`. Ledger e Identity permanecem módulos no mesmo processo nesta entrega.

## Consequências

O Ledger continua independente da disponibilidade do Summary. A separação posterior de Ledger e
Identity em processos próprios permanece possível, mas não faz parte desta entrega.
