# Operação, retry, DLQ e teste de carga

## Retry e reprocessamento

- O Outbox Relay mantém eventos não publicados com `PublishedAt = null` e tenta novamente no ciclo seguinte.
- O consumidor confirma mensagens somente após a projeção transacional.
- Falhas de processamento são reentregues enquanto forem transitórias.
- Payloads inválidos não são reentregues; são encaminhados à DLQ.
- A DLQ deve ser inspecionada pelo operador antes de qualquer reprocessamento.

Para inspecionar localmente:

```powershell
docker compose -f infra/compose/compose.yaml up --build -d --wait
docker compose -f infra/compose/compose.yaml exec rabbitmq rabbitmqctl list_queues name messages messages_ready messages_unacknowledged
```

## Teste de carga

O cenário é executado por:

```powershell
docker compose -f infra/compose/compose.yaml --profile load run --rm load
```

O seed usa `demo-operator` e cria lançamentos pelo Core API. A carga principal consulta o consolidado a 50 RPS. O teste falha se a taxa de respostas não-200 exceder 5% ou se o p95 exceder 500 ms.

Resultado de cada execução deve ser anexado nesta seção com data, duração, total de requisições, taxa de falhas e p95.

