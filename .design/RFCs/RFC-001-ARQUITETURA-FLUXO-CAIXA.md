# RFC-001-ARQUITETURA-FLUXO-CAIXA — Arquitetura do fluxo de caixa

Status: aprovado  
Data: 2026-09-19  
Relacionada: [discovery do fluxo de caixa](../Discoveries/DISCOVERY-001-FLUXO-CAIXA.md)

## Convenção de identificação

Todos os RFCs e ADRs do projeto usam nomes de arquivo e identificadores no formato:

```text
RFC-001-DESCRICAO
ADR-001-DESCRICAO
```

O número é sequencial dentro do tipo de documento, com três algarismos. `DESCRICAO` usa letras
maiúsculas, palavras separadas por hífen e sem acentos. RFCs registram propostas ainda sujeitas a
revisão; ADRs registram decisões aprovadas e permanentes. Um RFC aprovado pode originar um ou mais
ADRs, preservando a referência ao RFC de origem.

## Decisão resumida

A solução será um monorepo com frontend e backend separados. O backend terá dois processos:
`Core.Api`, contendo os módulos `Ledger`, `Identity`, `Audit` e `Integration`, e
`Summary.Api`, contendo a projeção e a consulta do consolidado. Os processos usarão uma única
instância PostgreSQL no ambiente local, mas com bancos lógicos separados: `core_db` e
`summary_db`. O Keycloak terá o banco lógico `keycloak_db` na mesma instância local, sem que a
aplicação acesse suas tabelas.

EF Core será o padrão de persistência dos serviços .NET. Cada módulo que possui dados terá seu
próprio `DbContext`; não haverá um `DbContext` compartilhado entre `Core.Api` e `Summary.Api`.

## 1. Estrutura do monorepo

```text
/
├── front/
│   ├── src/
│   ├── tests/
│   └── package.json
├── backend/
│   ├── Core/
│   │   ├── src/Core.Api/
│   │   │   ├── Ledger/
│   │   │   ├── Identity/
│   │   │   ├── Audit/
│   │   │   └── Integration/
│   │   └── tests/
│   │       ├── Core.UnitTests/
│   │       └── Core.IntegrationTests/
│   ├── Summary/
│   │   ├── src/Summary.Api/
│   │   │   ├── Projection/
│   │   │   └── Query/
│   │   └── tests/
│   │       ├── Summary.UnitTests/
│   │       └── Summary.IntegrationTests/
│   ├── tests/ArchitectureTests/
│   └── CashFlow.sln
├── contracts/
│   └── events/
├── infra/
│   ├── compose/
│   ├── postgres/
│   ├── keycloak/
│   ├── rabbitmq/
│   ├── otel/
│   └── load/
├── docs/
└── README.md
```

### Crítica da proposta `front/backend`

Esta separação é melhor que misturar `web`, `src` e serviços no mesmo nível, porque torna o
limite de execução evidente. `front` e `backend` são nomes de áreas de produto; `infra` contém
somente ambiente e operação; `contracts` contém apenas contratos consumidos entre processos.

Os testes ficam dentro de cada aplicação (`front/tests`, `backend/Core/tests` e
`backend/Summary/tests`), pois pertencem ao ecossistema que testam e não misturam regras de
contextos diferentes. `backend/tests/ArchitectureTests` é a única exceção: verifica dependências
entre módulos .NET e, por isso, cruza as duas aplicações.
Não haverá uma biblioteca compartilhada de entidades ou regras de domínio. Compartilhar classes
de domínio entre `Core` e `Summary` criaria acoplamento e permitiria que a projeção se tornasse
uma segunda fonte de verdade.

## 2. Processos e fronteiras DDD

| Contexto | Processo | Responsabilidade | Fonte de verdade |
|---|---|---|---|
| Ledger | `Core.Api` | criar, editar, excluir logicamente e consultar lançamentos | `core_db` |
| Identity & Access | `Core.Api` + Keycloak | administrar usuários/papéis e verificar autorização atual | Keycloak |
| Audit | `Core.Api` | registrar mudanças de negócio e consultas autorizadas | `core_db` |
| Integration | `Core.Api` | outbox, publicação e correlação de eventos | `core_db` + RabbitMQ |
| Summary Projection | `Summary.Api` | consumir eventos, deduplicar e materializar saldos | `summary_db` |
| Summary Query | `Summary.Api` | consultar saldos e expor frescor | `summary_db` |

`Identity` não terá usuários locais. O módulo é uma ACL para o Keycloak e concentra a credencial
administrativa, políticas de escopo mínimo e a auditoria da operação iniciada pela aplicação.

## 3. PostgreSQL: uma instância, bancos lógicos separados

### Decisão

No Compose haverá uma única instância PostgreSQL (`postgres`) com três bancos lógicos:

- `core_db`: dados de Ledger, Audit, Outbox e tentativas de criação;
- `summary_db`: Inbox, entradas projetadas e resumo diário;
- `keycloak_db`: persistência interna do Keycloak, inacessível pela aplicação.

Dentro de `core_db`, os schemas lógicos serão `ledger`, `audit` e `integration`. Dentro de
`summary_db`, serão `projection` e `query` quando a separação for útil ao modelo.

### Motivo

Uma instância reduz o custo operacional e torna o ambiente de teste simples, sem perder a
propriedade lógica dos dados: cada aplicação usa uma database/schema e credenciais próprias. A
separação entre `core_db` e `summary_db` impede que o consolidado leia tabelas do Ledger e
preserva a demonstração de integração assíncrona.

Isso não é alta disponibilidade nem isolamento físico de produção. A documentação deve declarar
que o Compose tem um único ponto de falha. Em uma implantação real, a decisão pode evoluir para
instâncias, réplicas ou serviços gerenciados sem mudar os limites dos contextos.

### Regras de acesso

- `Core.Api` acessa somente `core_db`.
- `Summary.Api` acessa somente `summary_db`.
- O Keycloak acessa somente `keycloak_db`.
- Nenhuma aplicação consulta tabelas internas do Keycloak.
- Não haverá foreign key entre `core_db` e `summary_db`; a relação entre eles é o evento.

## 4. EF Core

EF Core será adotado desde o primeiro commit de implementação.

- Cada contexto persistente terá `DbContext` e migrations próprios.
- Migrations serão versionadas no repositório e executadas por comando explícito de infraestrutura;
  não serão executadas automaticamente no startup das APIs.
- O ambiente terá um comando documentado para aplicar migrations e outro para recriar os bancos
  locais da demonstração.
- Concorrência de `LedgerEntry` usará o mecanismo de versão do EF Core e transação explícita.
- A gravação de `LedgerEntry`, `AuditRecord` e `OutboxEvent` ocorrerá em uma única transação.
- O consumidor gravará `InboxEvent` e a projeção na mesma transação antes do `ack`.
- Queries do `Summary.Api` serão somente leitura e poderão usar `AsNoTracking`.
- Nenhuma entidade EF será reutilizada como DTO HTTP ou contrato de evento.

Os contratos de evento terão versões explícitas e serão mantidos em `contracts/events`; o código
de domínio de `Core` não será referenciado por `Summary`.

Os nomes dos eventos seguirão a forma `LedgerEntryCreated.v1`, `LedgerEntryUpdated.v1` e
`LedgerEntryDeleted.v1`. Consumidores deverão aceitar versões compatíveis durante a evolução do
contrato; uma alteração incompatível criará nova versão do evento.

## 5. Keycloak e segurança operacional

O ambiente de demonstração terá três contas iniciais:

| Usuário | Papel | Senha inicial de laboratório |
|---|---|---|
| `admin` | `admin` | `admin123` |
| `operador` | `operator` | `operador123` |
| `auditor` | `auditor` | `auditor123` |

Essas credenciais serão documentadas no README como **somente para o ambiente local de teste**.
Não serão usadas em produção, não serão aceitas como evidência de segurança de credenciais e o
Compose não será publicado diretamente na internet.

O frontend usará Authorization Code + PKCE (S256). O access token terá duração de uma hora e será
validado localmente por assinatura, issuer, audience e expiração. A autorização de negócio
continuará consultando o estado atual no Keycloak em cada requisição, conforme o spike já
aprovado; portanto, a duração de uma hora não permite que uma conta desativada ou um papel
revogado continue autorizado até o vencimento do token.

Haverá credenciais de máquina separadas para:

1. leitura de autorização do `Core.Api`;
2. leitura de autorização do `Summary.Api`;
3. administração de usuários pelo módulo `Identity`.

As duas primeiras terão somente permissões de leitura. Segredos, tokens e respostas do Admin REST
serão mascarados em logs e traces.

## 6. Observabilidade

`Summary.Api` devolverá, junto do saldo, o instante `asOf` e um estado de frescor:

- `current`: projeção dentro do limite de 30 segundos;
- `stale`: há atraso superior a 30 segundos;
- `unavailable`: projeção ou dependência indisponível.

Métricas mínimas: atraso da projeção, idade da outbox, falhas e retries de publicação, duplicatas
de inbox, tempo de processamento de eventos, latência/erro do verificador Keycloak e respostas
por status de autorização. O Collector encaminhará traces, métricas e logs ao New Relic. Nenhuma
telemetria conterá senha, token, segredo, valor financeiro bruto ou dado pessoal desnecessário.

Durante testes de carga e falhas, o Collector usará 100% de amostragem de traces. Em execução
normal, a amostragem será configurável por variável de ambiente; métricas e logs de erro serão
preservados independentemente da amostragem de traces.

## 7. Consequências e alternativas rejeitadas

- Instâncias PostgreSQL separadas: rejeitadas para o Compose por custo operacional adicional;
  continuam possíveis em produção sem alterar os contratos.
- Um único banco compartilhado por Core e Summary: rejeitado porque permitiria acoplamento por
  tabela e enfraqueceria a prova de integração assíncrona.
- EF Core compartilhando um modelo entre serviços: rejeitado porque mistura ownership e migrações.
- Usuários locais sincronizados com Keycloak: rejeitado porque criaria duas fontes de identidade.
- Dividir Ledger e Identity em processos agora: adiado; o ganho de isolamento não compensa a
  complexidade desta entrega, e a separação interna permite evolução posterior.

## 8. Decisão final do verificador Keycloak

O timeout por requisição será de `2 s`, configurável por ambiente, sem fallback permissivo. Falha
de rede, timeout, erro de autenticação da credencial de serviço ou resposta inválida retorna `503`
e não executa a operação de negócio.

O valor foi escolhido porque `100 ms` é agressivo demais para uma execução local com Docker,
rede interna e Keycloak sob carga, enquanto `5 s` poderia acumular centenas de chamadas pendentes
durante uma indisponibilidade a 50 requisições por segundo. A implementação deverá medir o
comportamento no Compose, mas qualquer ajuste posterior será operacional e não mudará a regra
fail-closed.
