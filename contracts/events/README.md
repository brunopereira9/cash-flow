# Event contracts

Os contratos versionados entre `Core.Api` e `Summary.Api` serão definidos nesta pasta. Nenhum serviço referencia entidades de domínio do outro.

## Telemetria segura

Logs, métricas e traces identificam o evento por `eventId`, nome versionado, resultado e duração.
Não registrar tokens, senhas, segredos, payloads completos, descrições ou valores financeiros brutos.
