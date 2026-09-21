# Fluxo de caixa

Scaffold do monorepo definido no RFC-001: `Core.Api`, `Summary.Api`, frontend React/Vite, contratos de eventos e testes .NET separados por contexto.

## Rodar localmente com Docker

### Pré-requisitos

- Docker Desktop instalado e em execução, com suporte ao Docker Compose.
- Portas locais livres: `5173`, `5080`, `5081`, `18081`, `4317` e `4318`.

Na raiz do projeto, suba o ambiente completo com:

```powershell
docker compose -f infra/compose/compose.yaml up --build -d --wait
```

O Compose cria os bancos no PostgreSQL, executa as migrations das APIs e aguarda as dependências ficarem saudáveis antes de iniciar o Core e o Summary.

### Acessar a aplicação

- Frontend: <http://localhost:5173>
- Core API: <http://localhost:5080>
- Summary API: <http://localhost:5081>
- Keycloak: <http://localhost:18081>
- RabbitMQ Management: <http://localhost:15672> (`admin` / `local-rabbitmq-admin`)

Usuários de desenvolvimento do Keycloak usam a senha `username123`. Para acessar as funcionalidades protegidas, use o usuário `demo-operator`.

Para verificar se as APIs estão prontas:

```powershell
curl http://localhost:5080/healthz
curl http://localhost:5081/healthz
```

Para acompanhar os logs:

```powershell
docker compose -f infra/compose/compose.yaml logs -f
```

### Executar o teste de carga

Com o ambiente em execução, rode o perfil de carga do Compose:

```powershell
docker compose -f infra/compose/compose.yaml --profile load run --rm load
```

Esse comando executa o cenário definido em `infra/load/daily-summary-50rps.js` com o k6, aguardando a Summary API ficar saudável antes do início do teste.

Para parar os containers, preservando os dados locais:

```powershell
docker compose -f infra/compose/compose.yaml down
```

Para remover também os volumes e recriar o ambiente do zero — operação destrutiva para os dados locais:

```powershell
docker compose -f infra/compose/compose.yaml down -v
```

O ambiente de infraestrutura possui instruções adicionais em [`infra/README.md`](infra/README.md), incluindo observabilidade, banco de dados, autenticação e testes de carga.

## Comandos disponíveis

```powershell
dotnet build backend/CashFlow.slnx
dotnet test backend/CashFlow.slnx
cd front; npm install; npm run build
```

## Executar os testes

Restaure as dependências e compile a solução antes de executar a suíte:

```powershell
dotnet restore backend/CashFlow.slnx
dotnet build backend/CashFlow.slnx --no-restore
```

Testes unitários e contratos arquiteturais:

```powershell
dotnet test backend/Core/tests/Core.UnitTests/Core.UnitTests.csproj --no-restore
dotnet test backend/tests/ArchitectureTests/ArchitectureTests.csproj --no-restore
```

Testes de integração devem ser executados separadamente para evitar disputa por portas, containers e recursos do Docker:

```powershell
dotnet test backend/Core/tests/Core.IntegrationTests/Core.IntegrationTests.csproj --no-restore
dotnet test backend/Summary/tests/Summary.IntegrationTests/Summary.IntegrationTests.csproj --no-restore
```

Os testes de integração do Summary criam um container RabbitMQ temporário e, portanto, exigem o Docker Desktop em execução. A fixture usa as credenciais locais `integration/integration` e remove o container ao final.

Para executar tudo de uma vez:

```powershell
dotnet test backend/CashFlow.slnx --no-restore
```

Essa forma é útil para uma verificação rápida, mas a execução separada dos projetos de integração é mais previsível em máquinas com recursos limitados. Os testes de timeout do Keycloak dependem do tempo de inicialização do `TestServer` e podem apresentar variação quando todos os assemblies são executados simultaneamente.

Os serviços de infraestrutura, migrations e testes de comportamento são mantidos nas pastas `infra/`, `backend/` e `front/`.
