# Fluxo de caixa

Scaffold do monorepo definido no RFC-001: `Core.Api`, `Summary.Api`, frontend React/Vite, contratos de eventos e testes .NET separados por contexto.

## Comandos disponíveis

```powershell
dotnet build backend/CashFlow.slnx
dotnet test backend/CashFlow.slnx
cd front; npm install; npm run build
```

Os serviços de infraestrutura, migrations e os testes de comportamento serão adicionados conforme os checks aprovados.
