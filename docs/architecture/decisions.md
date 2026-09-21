# Decisões arquiteturais e trade-offs

## Separação entre Core e Summary

O controle de lançamentos e o consolidado são bounded contexts separados, com bancos próprios. O Core é a fonte transacional; o Summary é uma projeção materializada para leitura.

Trade-off: a leitura pode ficar temporariamente stale, mas o lançamento não depende da disponibilidade do consolidado.

## Outbox + RabbitMQ + Inbox

O Core grava o lançamento, a auditoria e o evento Outbox na mesma transação. Um relay publica eventos confirmados. O Summary registra cada `eventId` na Inbox antes de aplicar a projeção.

Trade-off: há maior complexidade operacional e consistência eventual, compensadas por recuperação após falhas e ausência de perda silenciosa de eventos.

## Idempotência e controle de versão

Criações exigem `Idempotency-Key`; a intenção original é comparada quando a chave reaparece. Atualizações e projeções usam versões monotônicas.

Trade-off: clientes precisam conservar a chave e a versão, mas retries deixam de gerar duplicidade ou sobrescritas obsoletas.

## DLQ e mensagens inválidas

Payloads inválidos são rejeitados sem requeue e seguem para `cashflow.summary.dlq`. Falhas transitórias de processamento continuam elegíveis para retry.

Trade-off: mensagens permanentes são removidas do fluxo principal e exigem operação de reprocessamento, evitando poison-message loops.

## Escalabilidade

O Summary é stateless na API e pode ser replicado atrás de um balanceador. O RabbitMQ desacopla ingestão e projeção. O teste k6 mantém 50 RPS de leitura com limite de 5% de respostas falhas e p95 abaixo de 500 ms.

Trade-off: a entrega atual local usa uma instância de cada serviço; alta disponibilidade de produção exige réplicas, banco gerenciado e RabbitMQ em cluster.

