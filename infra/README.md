# Ambiente local

Comando canônico aguardando dependências saudáveis: `docker compose -f infra/compose/compose.yaml up --build -d --wait`.
Os jobs de migração usam a opção `--migrate`; os serviços Core e Summary expõem `/readyz`.

Para desenvolvimento com hot reload, use o override abaixo. O backend roda com `dotnet watch`, o frontend com Vite e o código local é montado dentro dos containers:

```powershell
docker compose -f infra/compose/compose.yaml -f infra/compose/compose.dev.yaml up --build
```

O frontend de desenvolvimento fica disponível em `http://localhost:5174`. O Rider pode se conectar aos processos .NET dos containers pela configuração **Attach to Process > Docker**. Os processos do Core e Summary ficam expostos, respectivamente, nas portas `5005` e `5006`; o `dotnet watch` recompila e reinicia o processo quando os arquivos são alterados. Para encerrar o ambiente, pressione `Ctrl+C` ou execute `docker compose -f infra/compose/compose.yaml -f infra/compose/compose.dev.yaml down`.

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
