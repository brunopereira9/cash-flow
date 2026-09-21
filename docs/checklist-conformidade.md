# Checklist de conformidade do teste

Atualizado em 21/09/2026.

Legenda: ✅ atendido | ⚠️ parcialmente atendido | ❌ não atendido/comprovado

## Requisitos de negócio

- ✅ Serviço de controle de lançamentos.
- ✅ Controle de débitos e créditos.
- ✅ Serviço de consolidado diário.
- ✅ Cálculo do saldo diário consolidado.

## Requisitos técnicos obrigatórios

- ✅ Desenho da solução — diagramas C4 níveis 1, 2 e 3 em `docs/architecture/`.
- ✅ Implementação em C#.
- ✅ Testes automatizados.
- ✅ README com instruções de execução.
- ✅ README explica o funcionamento básico.
- ⚠️ Repositório público no GitHub — existe remote configurado, mas a disponibilidade pública não foi validada nesta revisão.
- ✅ Documentações principais no repositório — arquitetura, decisões, trade-offs e operação estão documentados.
- ✅ Possibilidade de execução local via Docker Compose.
- ✅ Comandos de testes documentados.

## Alta disponibilidade e resiliência

- ✅ Core continua aceitando lançamentos sem o Summary.
- ✅ Outbox Pattern implementado.
- ✅ Comunicação assíncrona via RabbitMQ.
- ✅ Eventos pendentes podem ser publicados posteriormente.
- ⚠️ Retry básico implementado no relay e no consumidor; não há política completa de backoff e limite de tentativas.
- ⚠️ Reprocessamento controlado documentado via RabbitMQ Management, mas ainda manual.
- ✅ Idempotência na projeção do Summary.
- ✅ Idempotência na criação com tratamento de concorrência e comparação da intenção original.
- ✅ Tratamento de indisponibilidade do Summary.
- ✅ Health checks dos serviços.
- ⚠️ Recuperação básica do consumidor RabbitMQ.
- ❌ Redundância e failover efetivamente configurados.
- ✅ Teste demonstrando Core disponível durante a indisponibilidade do Summary.
- ✅ Teste de recuperação do Summary após o retorno, com reprocessamento do evento pendente.

## Performance e escalabilidade

- ✅ Cenário configurado para 50 requisições por segundo.
- ✅ Limite de perda de até 5% configurado no k6.
- ✅ Limite de latência p95 configurado.
- ✅ Teste de carga usa o fluxo real Core → Outbox → RabbitMQ → Summary.
- ✅ Resultado real documentado em `docs/architecture/operations.md`.
- ✅ Fluxo completo Core → Outbox → RabbitMQ → Summary validado no seed do teste.
- ❌ Teste de carga específico do serviço de lançamentos.
- ❌ Teste de carga durante indisponibilidade do Summary.
- ❌ Cache implementado.
- ⚠️ A arquitetura permite evolução horizontal, mas não há implantação com múltiplas réplicas.
- ❌ Múltiplas réplicas configuradas.
- ❌ Balanceamento de carga configurado.
- ❌ Alta disponibilidade do PostgreSQL configurada.
- ❌ Alta disponibilidade do RabbitMQ configurada.
- ✅ Existem métricas e telemetria OpenTelemetry.
- ⚠️ Métricas de backlog, throughput e recuperação da fila são descritas operacionalmente, mas não há painel completo validado para todas elas.

## Segurança

- ✅ Autenticação JWT via Keycloak.
- ✅ Autorização por papéis.
- ✅ Validação do usuário ativo no Keycloak.
- ✅ Controle de acesso para operações administrativas.
- ✅ Validação de entradas, incluindo tipo restrito a `credit` ou `debit`.
- ✅ Endpoint `/internal/events` protegido pelo middleware de autenticação/autorização do Summary.
- ⚠️ Existe autenticação para acesso ao endpoint interno, mas não há identidade técnica exclusiva Core → Summary; o fluxo principal usa RabbitMQ.
- ✅ O ator da auditoria é derivado do claim `sub`; `X-Actor-Id` não é mais confiável.
- ✅ Não há possibilidade de falsificar o ator usando `X-Actor-Id`.
- ⚠️ Há testes de autenticação e autorização, mas não há teste específico de acesso não autorizado ao `/internal/events`.
- ❌ Rate limiting implementado.
- ⚠️ Segredos locais estão separados por configuração, mas existem credenciais de desenvolvimento no Compose/realm.
- ❌ Política de rotação de segredos documentada.
- ⚠️ HTTPS está desabilitado no ambiente local; não há configuração produtiva demonstrada.

## Arquitetura e boas práticas

- ✅ Arquitetura baseada em serviços separados, com Core e Summary isolados.
- ✅ Separação entre Core e Summary.
- ✅ Bancos separados por contexto.
- ✅ Separação entre domínio, aplicação e infraestrutura.
- ✅ Contratos de eventos versionados.
- ✅ Uso de Outbox e Inbox Pattern.
- ✅ Consistência eventual implementada.
- ✅ Auditoria implementada.
- ✅ Controle de versão para atualizações.
- ⚠️ Uso de SOLID e boas práticas é observável na estrutura, mas não existe avaliação formal de cada princípio.
- ✅ Padrões arquiteturais estão documentados em `docs/architecture/decisions.md`.
- ✅ Trade-offs documentados.
- ✅ Decisões arquiteturais documentadas.
- ✅ Diagrama de componentes C4 nível 3.
- ✅ Diagrama dos principais fluxos descrito na documentação operacional e no README.

## Documentação

- ✅ README principal.
- ✅ Documentação de infraestrutura.
- ✅ Documentação básica do frontend.
- ✅ Documentação de contratos de eventos.
- ✅ Documento arquitetural geral em `docs/architecture/`.
- ✅ Diagrama geral da solução — C4 nível 1.
- ✅ Diagrama de containers — C4 nível 2.
- ✅ Diagrama de componentes — C4 nível 3.
- ✅ Fluxo de lançamentos documentado.
- ✅ Fluxo do consolidado documentado.
- ✅ Fluxo de falha e recuperação documentado.
- ✅ Requisitos não funcionais registrados.
- ⚠️ Metas de disponibilidade estão descritas conceitualmente, mas não há ambiente produtivo HA validado.
- ✅ Resultado de performance documentado.
- ✅ Trade-offs documentados.
- ⚠️ Evoluções futuras estão indicadas pelos trade-offs de produção, mas podem ser detalhadas em uma seção própria.
- ⚠️ Não há ADRs individuais; as decisões estão consolidadas em `docs/architecture/decisions.md`.

## Testes

- ✅ Testes arquiteturais.
- ✅ Testes unitários.
- ✅ Testes de integração do Core.
- ✅ Testes de integração do Summary.
- ✅ Teste de deduplicação de eventos.
- ✅ Teste de atualização de projeção.
- ✅ Teste de exclusão.
- ✅ Teste de consistência do consolidado.
- ✅ Teste de dados obsoletos.
- ✅ Teste de Summary indisponível.
- ✅ Teste de autorização.
- ✅ Teste de concorrência de lançamentos.
- ✅ Teste de Core disponível com Summary fora do ar.
- ✅ Teste de recuperação após retorno do Summary.
- ⚠️ DLQ é configurada e mensagens inválidas são encaminhadas, mas não há teste automatizado dedicado à DLQ.
- ⚠️ Reprocessamento está documentado como procedimento manual, sem teste automatizado dedicado.
- ❌ Teste específico de acesso não autorizado ao `/internal/events`.
- ✅ Teste para tipo de lançamento inválido.
- ✅ Teste de idempotência concorrente com a mesma chave.
- ✅ Script de carga com resultado real versionado na documentação.
- ❌ Testes de carga integrados ao CI.
- ❌ Relatório de cobertura documentado.

## Correções prioritárias

1. ✅ Proteger o endpoint `/internal/events`.
2. ✅ Remover a confiança no header `X-Actor-Id`.
3. ✅ Validar apenas os tipos `credit` e `debit`.
4. ✅ Corrigir idempotência concorrente.
5. ✅ Criar teste de indisponibilidade e recuperação do Summary.
6. ✅ Ajustar o teste de carga para usar o fluxo real.
7. ✅ Criar diagramas, decisões arquiteturais e trade-offs.
8. ✅ Documentar DLQ, retry e reprocessamento.
9. ✅ Demonstrar resultados reais do teste de 50 RPS.

## Evidências principais

- `4f2a35e` — proteção de eventos internos e fluxo real do teste de carga.
- `c9d2ca3` — identidade do ator e validação de tipos.
- `98d2bdf` — idempotência concorrente preservando conflito para intenção diferente.
- `76d26e1` — teste de indisponibilidade e recuperação do Summary.
- `a68f380` — documentação C4, decisões e operação.
- `163827a` — teste real de 50 RPS e resultado documentado.
- `2966ad4` — procedimento de reprocessamento controlado da DLQ.
