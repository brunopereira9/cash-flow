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

Os serviços de infraestrutura, migrations e testes de comportamento são mantidos nas pastas `infra/`, `backend/` e `front/`.
