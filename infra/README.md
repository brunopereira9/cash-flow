# Ambiente local

Comando canônico aguardando dependências saudáveis: `docker compose -f infra/compose/compose.yaml up --build -d --wait`.
Os jobs de migração usam a opção `--migrate`; os serviços Core e Summary expõem `/readyz`.

Configure a chave local do New Relic em `infra/.env.local` (arquivo ignorado pelo Git) usando `NEW_RELIC_LICENSE_KEY`, e suba com `docker compose --env-file infra/.env.local -f infra/compose/compose.yaml up --build -d`. A forma canônica sem credencial externa é `docker compose -f infra/compose/compose.yaml up --build -d`. O OpenTelemetry Collector recebe traces, métricas e logs das APIs via OTLP e exporta para New Relic; a chave não deve ser colocada em arquivos versionados.

Verificar: `curl http://localhost:5080/healthz`, `curl http://localhost:5081/healthz` e aguardar o Compose com `--wait`.

Migrations do PostgreSQL: o arquivo `infra/postgres/001-init.sql` cria os três bancos no primeiro boot; as tabelas são aplicadas explicitamente antes de iniciar as APIs:

```powershell
dotnet ef database update --project backend/Core/src/Api/Core.Api.csproj --startup-project backend/Core/src/Api/Core.Api.csproj
dotnet ef database update --project backend/Summary/src/Api/Summary.Api.csproj --startup-project backend/Summary/src/Api/Summary.Api.csproj
docker compose --env-file infra/.env.local -f infra/compose/compose.yaml up --build -d
```

O runtime das APIs não aplica migrations. Para repetir em um ambiente limpo, execute `docker compose -f infra/compose/compose.yaml down -v`, rode os dois comandos `dotnet ef` novamente e suba o Compose. O seed do Keycloak é explícito no arquivo `infra/keycloak/cashflow-realm.json`, importado por `--import-realm`; valide com `curl http://localhost:18081/realms/cashflow/.well-known/openid-configuration`.

Verificar bancos: `docker compose -f infra/compose/compose.yaml exec postgres psql -U cashflow -d postgres -c "\l"`.

Reset local destrutivo: `docker compose -f infra/compose/compose.yaml down -v`.

As APIs aceitam somente JWTs emitidos para `cashflow-front`. Em cada chamada de negócio, elas consultam a Admin API do Keycloak com o cliente de serviço `cashflow-api`; indisponibilidade ou timeout de dois segundos responde `503`, e conta desativada ou sem papel `operator` responde `403` mesmo com um JWT ainda válido. Obter um token de desenvolvimento: `curl -X POST http://localhost:18081/realms/cashflow/protocol/openid-connect/token -d "grant_type=password" -d "client_id=cashflow-front" -d "username=demo-operator" -d "password=username123"`.

O realm local também cria `demo-admin`, `demo-operator` e `demo-auditor`, todos com a senha insegura `username123` somente para desenvolvimento. Core usa `cashflow-api` e Summary usa `cashflow-summary`, com credenciais de serviço distintas; nenhuma aplicação consulta tabelas internas do Keycloak.
