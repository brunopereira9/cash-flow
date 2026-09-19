# RFC-002-ARQUITETURA-INTERNA-BACKEND — Hexagonal ou Clean Architecture

Status: aprovado  
Data: 2026-09-19  
Relacionada: [RFC-001-ARQUITETURA-FLUXO-CAIXA](RFC-001-ARQUITETURA-FLUXO-CAIXA.md)

## Contexto

O RFC-001 definiu dois processos backend (`Core.Api` e `Summary.Api`), mas ainda não definiu
como cada processo organizará dependências, casos de uso, domínio, persistência e adaptadores.
Essa decisão precisa ocorrer antes da criação do scaffold porque influencia projetos, referências
do .NET, testes e localização dos contratos.

As alternativas consideradas são Hexagonal Architecture (Ports and Adapters) e Clean Architecture.
Ambas podem atender aos bounded contexts definidos; a decisão deve evitar cerimônia sem benefício
para o escopo do teste.

## Critérios

- proteger regras de negócio contra EF Core, ASP.NET, RabbitMQ e Keycloak;
- tornar casos de uso testáveis sem infraestrutura;
- permitir testes de integração por adaptador;
- manter dependências claras entre `Core.Api` e `Summary.Api`;
- permitir evolução futura de módulos para processos separados;
- não criar projetos ou abstrações sem necessidade demonstrável.

## Opção A — Hexagonal Architecture

Cada aplicação possui um núcleo com regras e casos de uso. O núcleo define portas de entrada e
saída; adaptadores externos implementam essas portas.

```text
              adaptadores de entrada
                       ↓
                 [ núcleo ]
                       ↑
              adaptadores de saída
```

Exemplos de portas de saída: `ILedgerRepository`, `IKeycloakAuthorizationReader`,
`IEventPublisher` e `IClock`. Controllers, consumers, EF Core, RabbitMQ e Keycloak são
adaptadores.

### Vantagens

- comunica diretamente a regra mais importante: o domínio depende de portas, não de detalhes;
- funciona bem para módulos e bounded contexts;
- não exige uma quantidade fixa de camadas ou projetos;
- facilita substituir adaptadores em testes.

### Riscos

- equipes podem chamar qualquer classe de “porta” sem definir ownership e direção;
- sem disciplina, casos de uso, domínio e infraestrutura acabam misturados no mesmo projeto;
- não determina, por si só, onde ocorre a composição do host e o registro das implementações concretas. Essa convenção precisa ser definida pelo projeto;

## Opção B — Clean Architecture

Cada aplicação é organizada em anéis com dependências apontando para o centro:

```text
Api → Infrastructure → Application → Domain
```

Na prática, `Infrastructure` implementa interfaces definidas em `Application` ou `Domain`, e a
composição ocorre na camada `Api`.

### Vantagens

- fornece uma convenção visual e de projetos fácil de revisar;
- torna regras de dependência verificáveis por testes de arquitetura;
- separa claramente domínio, casos de uso, infraestrutura e entrega HTTP;
- é familiar para equipes .NET e facilita onboarding.

### Riscos

- pode gerar camadas artificiais, serviços anêmicos e excesso de mapeamentos;
- o nome “Clean” frequentemente vira uma estrutura de pastas sem isolamento real;
- a separação rígida pode dificultar módulos pequenos se aplicada mecanicamente.

## Comparação

| Critério | Hexagonal | Clean |
|---|---|---|
| Testabilidade | forte, centrada em portas | forte, centrada em dependências por camada |
| Clareza de adaptadores | explícita | explícita, mas distribuída em Infrastructure/Api |
| Cerimônia | menor | maior se dividida em muitos projetos |
| Adequação ao teste | muito boa | muito boa |
| Evolução para serviços | natural por bounded context | natural por projeto/processo |
| Risco principal | portas sem disciplina | camadas artificiais |

## Proposta recomendada

Adotar **Clean Architecture pragmática com portas e adaptadores hexagonais**. Clean será a
organização estrutural dos projetos; Hexagonal será a regra de dependência e a linguagem dos
contratos externos.

Cada processo terá, no mínimo:

```text
src/
├── Domain/
├── Application/
├── Infrastructure/
└── Api/
```

`Application` e `Domain` definirão ports/interfaces quando precisarem de recursos externos.
`Infrastructure` conterá os adapters de EF Core, Keycloak, RabbitMQ e OpenTelemetry. `Api`
conterá endpoints, consumers, composição de dependências e configuração do host.

Não será criado um projeto separado para cada entidade ou caso de uso. A divisão em projetos
será feita por fronteira de processo e dependência real; a organização interna poderá usar
módulos `Ledger`, `Identity`, `Audit`, `Integration`, `Projection` e `Query`.

## Decisão solicitada

Confirmar a proposta híbrida: Clean Architecture como estrutura de dependências do processo e
Hexagonal Architecture como princípio de portas e adaptadores. Após aprovação, será criado
`ADR-005-CLEAN-HEXAGONAL-BACKEND.md` e o scaffold seguirá essa decisão.
