# Operação, retry, DLQ e teste de carga

## Retry e reprocessamento

- O Outbox Relay mantém eventos não publicados com `PublishedAt = null` e tenta novamente no ciclo seguinte.
- O consumidor confirma mensagens somente após a projeção transacional.
- Falhas de processamento são reentregues enquanto forem transitórias.
- Payloads inválidos não são reentregues; são encaminhados à DLQ.
- A DLQ deve ser inspecionada pelo operador antes de qualquer reprocessamento.

### Reprocessamento manual controlado

1. Abra `http://localhost:15672` com o usuário local `admin`.
2. Em **Queues and Streams**, abra `cashflow.summary.dlq` e inspecione a mensagem e o motivo da falha.
3. Corrija a causa antes de republicar. Não reprocesse payloads inválidos sem alteração.
4. Use **Get Message(s)** com `Ack Mode = Nack message requeue true` para devolver a mensagem à fila somente durante a inspeção, ou publique uma cópia corrigida no exchange `cashflow.ledger` com a routing key do evento.
5. Confirme que o Summary aplicou a projeção e somente então reconheça/remova a mensagem original da DLQ.
6. Verifique a Inbox, o consolidado diário e os logs de `summary.projection.applied`.

Esse fluxo é deliberadamente manual no ambiente do teste. Em produção, deve ser substituído por uma operação autenticada, auditada e com limite de tentativas.

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

Resultado registrado em 21/09/2026, no ambiente Docker local, com seed de 20 lançamentos e duração de 30 segundos:

| Indicador | Resultado |
|---|---:|
| Taxa configurada | 50 RPS |
| Iterações de leitura | 1.500 |
| Requisições HTTP totais | 1.523 |
| Respostas Summary com falha | 0 (0,00%) |
| p95 da leitura do Summary | 92,49 ms |
| Critério de perda | aprovado (`<= 5%`) |
| Critério de latência | aprovado (`p95 < 500 ms`) |
| Seed pelo fluxo Core → Outbox → RabbitMQ → Summary | aprovado |

Essa execução é uma evidência local, não uma garantia de capacidade produtiva. Uma validação final deve repetir o cenário com a duração e o volume definidos para o ambiente de implantação.
