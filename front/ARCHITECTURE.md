# Arquitetura do frontend

O frontend usa uma organização simples por responsabilidade. Ele não replica a Clean Architecture ou a arquitetura hexagonal do backend.

```text
src/
  app/          # inicialização, rotas e providers
  features/     # funcionalidades do produto
  components/   # componentes visuais reutilizáveis
  lib/          # HTTP, autenticação e utilitários
```

## Regras

- Componentes cuidam da apresentação.
- Chamadas HTTP ficam em `features/<feature>/api.ts` ou em `lib/` quando forem compartilhadas.
- Estado e carregamento ficam em hooks da feature quando necessário.
- Tipos de uma feature ficam próximos dela.
- `lib/` contém integrações compartilhadas, como cliente HTTP e autenticação.
- Não criar camadas `domain`, `application`, `infrastructure`, `ports` ou `adapters` no frontend sem uma decisão específica que justifique a complexidade.

## Validação

Execute na raiz do repositório:

```powershell
python tools/validate_frontend_architecture.py
```

A validação verifica as pastas-base e impede a introdução acidental de camadas hexagonais/clean desnecessárias.
