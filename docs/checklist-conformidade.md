# Checklist de conformidade do teste

Atualizado em 21/09/2026.

Legenda: ✅ atendido | ⚠️ parcialmente atendido | ❌ não atendido/comprovado

## Requisitos de negócio

- [✅ Serviço de controle de lançamentos.](checklist/001-ADR-SERVI-O-DE-CONTROLE-DE-LAN-AMENTOS.md)
- [✅ Controle de débitos e créditos.](checklist/002-ADR-CONTROLE-DE-D-BITOS-E-CR-DITOS.md)
- [✅ Serviço de consolidado diário.](checklist/003-ADR-SERVI-O-DE-CONSOLIDADO-DI-RIO.md)
- [✅ Cálculo do saldo diário consolidado.](checklist/004-ADR-C-LCULO-DO-SALDO-DI-RIO-CONSOLIDADO.md)

## Requisitos técnicos obrigatórios

- [✅ Desenho da solução — diagramas C4 níveis 1, 2 e 3 em `docs/architecture/`.](checklist/005-ADR-DESENHO-DA-SOLU-O-DIAGRAMAS-C4-N-VEIS-1-2-E-3-EM-DOCS-ARCH.md)
- [✅ Implementação em C#.](checklist/006-ADR-IMPLEMENTA-O-EM-C.md)
- [✅ Testes automatizados.](checklist/007-ADR-TESTES-AUTOMATIZADOS.md)
- [✅ README com instruções de execução.](checklist/008-ADR-README-COM-INSTRU-ES-DE-EXECU-O.md)
- [✅ README explica o funcionamento básico.](checklist/009-ADR-README-EXPLICA-O-FUNCIONAMENTO-B-SICO.md)
- [⚠️ Repositório público no GitHub — existe remote configurado, mas a disponibilidade pública não foi validada nesta revisão.](checklist/010-ADR-REPOSIT-RIO-P-BLICO-NO-GITHUB-EXISTE-REMOTE-CONFIGURADO-MA.md)
- [✅ Documentações principais no repositório — arquitetura, decisões, trade-offs e operação estão documentados.](checklist/011-ADR-DOCUMENTA-ES-PRINCIPAIS-NO-REPOSIT-RIO-ARQUITETURA-DECIS-E.md)
- [✅ Possibilidade de execução local via Docker Compose.](checklist/012-ADR-POSSIBILIDADE-DE-EXECU-O-LOCAL-VIA-DOCKER-COMPOSE.md)
- [✅ Comandos de testes documentados.](checklist/013-ADR-COMANDOS-DE-TESTES-DOCUMENTADOS.md)

## Alta disponibilidade e resiliência

- [✅ Core continua aceitando lançamentos sem o Summary.](checklist/014-RES-CORE-CONTINUA-ACEITANDO-LAN-AMENTOS-SEM-O-SUMMARY.md)
- [✅ Outbox Pattern implementado.](checklist/015-RES-OUTBOX-PATTERN-IMPLEMENTADO.md)
- [✅ Comunicação assíncrona via RabbitMQ.](checklist/016-RES-COMUNICA-O-ASS-NCRONA-VIA-RABBITMQ.md)
- [✅ Eventos pendentes podem ser publicados posteriormente.](checklist/017-RES-EVENTOS-PENDENTES-PODEM-SER-PUBLICADOS-POSTERIORMENTE.md)
- [⚠️ Retry básico implementado no relay e no consumidor; não há política completa de backoff e limite de tentativas.](checklist/018-RES-RETRY-B-SICO-IMPLEMENTADO-NO-RELAY-E-NO-CONSUMIDOR-N-O-H-P.md)
- [⚠️ Reprocessamento controlado documentado via RabbitMQ Management, mas ainda manual.](checklist/019-RES-REPROCESSAMENTO-CONTROLADO-DOCUMENTADO-VIA-RABBITMQ-MANAGE.md)
- [✅ Idempotência na projeção do Summary.](checklist/020-RES-IDEMPOT-NCIA-NA-PROJE-O-DO-SUMMARY.md)
- [✅ Idempotência na criação com tratamento de concorrência e comparação da intenção original.](checklist/021-RES-IDEMPOT-NCIA-NA-CRIA-O-COM-TRATAMENTO-DE-CONCORR-NCIA-E-CO.md)
- [✅ Tratamento de indisponibilidade do Summary.](checklist/022-RES-TRATAMENTO-DE-INDISPONIBILIDADE-DO-SUMMARY.md)
- [✅ Health checks dos serviços.](checklist/023-RES-HEALTH-CHECKS-DOS-SERVI-OS.md)
- [⚠️ Recuperação básica do consumidor RabbitMQ.](checklist/024-RES-RECUPERA-O-B-SICA-DO-CONSUMIDOR-RABBITMQ.md)
- [❌ Redundância e failover efetivamente configurados.](checklist/025-RES-REDUND-NCIA-E-FAILOVER-EFETIVAMENTE-CONFIGURADOS.md)
- [✅ Teste demonstrando Core disponível durante a indisponibilidade do Summary.](checklist/026-RES-TESTE-DEMONSTRANDO-CORE-DISPON-VEL-DURANTE-A-INDISPONIBILI.md)
- [✅ Teste de recuperação do Summary após o retorno, com reprocessamento do evento pendente.](checklist/027-RES-TESTE-DE-RECUPERA-O-DO-SUMMARY-AP-S-O-RETORNO-COM-REPROCES.md)

## Performance e escalabilidade

- [✅ Cenário configurado para 50 requisições por segundo.](checklist/028-PERF-CEN-RIO-CONFIGURADO-PARA-50-REQUISI-ES-POR-SEGUNDO.md)
- [✅ Limite de perda de até 5% configurado no k6.](checklist/029-PERF-LIMITE-DE-PERDA-DE-AT-5-CONFIGURADO-NO-K6.md)
- [✅ Limite de latência p95 configurado.](checklist/030-PERF-LIMITE-DE-LAT-NCIA-P95-CONFIGURADO.md)
- [✅ Teste de carga usa o fluxo real Core → Outbox → RabbitMQ → Summary.](checklist/031-PERF-TESTE-DE-CARGA-USA-O-FLUXO-REAL-CORE-OUTBOX-RABBITMQ-SUMMA.md)
- [✅ Resultado real documentado em `docs/architecture/operations.md`.](checklist/032-PERF-RESULTADO-REAL-DOCUMENTADO-EM-DOCS-ARCHITECTURE-OPERATIONS.md)
- [✅ Fluxo completo Core → Outbox → RabbitMQ → Summary validado no seed do teste.](checklist/033-PERF-FLUXO-COMPLETO-CORE-OUTBOX-RABBITMQ-SUMMARY-VALIDADO-NO-SE.md)
- [❌ Teste de carga específico do serviço de lançamentos.](checklist/034-PERF-TESTE-DE-CARGA-ESPEC-FICO-DO-SERVI-O-DE-LAN-AMENTOS.md)
- [❌ Teste de carga durante indisponibilidade do Summary.](checklist/035-PERF-TESTE-DE-CARGA-DURANTE-INDISPONIBILIDADE-DO-SUMMARY.md)
- [❌ Cache implementado.](checklist/036-PERF-CACHE-IMPLEMENTADO.md)
- [⚠️ A arquitetura permite evolução horizontal, mas não há implantação com múltiplas réplicas.](checklist/037-PERF-A-ARQUITETURA-PERMITE-EVOLU-O-HORIZONTAL-MAS-N-O-H-IMPLANT.md)
- [❌ Múltiplas réplicas configuradas.](checklist/038-PERF-M-LTIPLAS-R-PLICAS-CONFIGURADAS.md)
- [❌ Balanceamento de carga configurado.](checklist/039-PERF-BALANCEAMENTO-DE-CARGA-CONFIGURADO.md)
- [❌ Alta disponibilidade do PostgreSQL configurada.](checklist/040-PERF-ALTA-DISPONIBILIDADE-DO-POSTGRESQL-CONFIGURADA.md)
- [❌ Alta disponibilidade do RabbitMQ configurada.](checklist/041-PERF-ALTA-DISPONIBILIDADE-DO-RABBITMQ-CONFIGURADA.md)
- [✅ Existem métricas e telemetria OpenTelemetry.](checklist/042-PERF-EXISTEM-M-TRICAS-E-TELEMETRIA-OPENTELEMETRY.md)
- [⚠️ Métricas de backlog, throughput e recuperação da fila são descritas operacionalmente, mas não há painel completo validado para todas elas.](checklist/043-PERF-M-TRICAS-DE-BACKLOG-THROUGHPUT-E-RECUPERA-O-DA-FILA-S-O-DE.md)

## Segurança

- [✅ Autenticação JWT via Keycloak.](checklist/044-SEC-AUTENTICA-O-JWT-VIA-KEYCLOAK.md)
- [✅ Autorização por papéis.](checklist/045-SEC-AUTORIZA-O-POR-PAP-IS.md)
- [✅ Validação do usuário ativo no Keycloak.](checklist/046-SEC-VALIDA-O-DO-USU-RIO-ATIVO-NO-KEYCLOAK.md)
- [✅ Controle de acesso para operações administrativas.](checklist/047-SEC-CONTROLE-DE-ACESSO-PARA-OPERA-ES-ADMINISTRATIVAS.md)
- [✅ Validação de entradas, incluindo tipo restrito a `credit` ou `debit`.](checklist/048-SEC-VALIDA-O-DE-ENTRADAS-INCLUINDO-TIPO-RESTRITO-A-CREDIT-OU-D.md)
- [✅ Endpoint `/internal/events` protegido pelo middleware de autenticação/autorização do Summary.](checklist/049-SEC-ENDPOINT-INTERNAL-EVENTS-PROTEGIDO-PELO-MIDDLEWARE-DE-AUTE.md)
- [⚠️ Existe autenticação para acesso ao endpoint interno, mas não há identidade técnica exclusiva Core → Summary; o fluxo principal usa RabbitMQ.](checklist/050-SEC-EXISTE-AUTENTICA-O-PARA-ACESSO-AO-ENDPOINT-INTERNO-MAS-N-O.md)
- [✅ O ator da auditoria é derivado do claim `sub`; `X-Actor-Id` não é mais confiável.](checklist/051-SEC-O-ATOR-DA-AUDITORIA-DERIVADO-DO-CLAIM-SUB-X-ACTOR-ID-N-O-M.md)
- [✅ Não há possibilidade de falsificar o ator usando `X-Actor-Id`.](checklist/052-SEC-N-O-H-POSSIBILIDADE-DE-FALSIFICAR-O-ATOR-USANDO-X-ACTOR-ID.md)
- [⚠️ Há testes de autenticação e autorização, mas não há teste específico de acesso não autorizado ao `/internal/events`.](checklist/053-SEC-H-TESTES-DE-AUTENTICA-O-E-AUTORIZA-O-MAS-N-O-H-TESTE-ESPEC.md)
- [❌ Rate limiting implementado.](checklist/054-SEC-RATE-LIMITING-IMPLEMENTADO.md)
- [⚠️ Segredos locais estão separados por configuração, mas existem credenciais de desenvolvimento no Compose/realm.](checklist/055-SEC-SEGREDOS-LOCAIS-EST-O-SEPARADOS-POR-CONFIGURA-O-MAS-EXISTE.md)
- [❌ Política de rotação de segredos documentada.](checklist/056-SEC-POL-TICA-DE-ROTA-O-DE-SEGREDOS-DOCUMENTADA.md)
- [⚠️ HTTPS está desabilitado no ambiente local; não há configuração produtiva demonstrada.](checklist/057-SEC-HTTPS-EST-DESABILITADO-NO-AMBIENTE-LOCAL-N-O-H-CONFIGURA-O.md)

## Arquitetura e boas práticas

- [✅ Arquitetura baseada em serviços separados, com Core e Summary isolados.](checklist/058-ARCH-ARQUITETURA-BASEADA-EM-SERVI-OS-SEPARADOS-COM-CORE-E-SUMMA.md)
- [✅ Separação entre Core e Summary.](checklist/059-ARCH-SEPARA-O-ENTRE-CORE-E-SUMMARY.md)
- [✅ Bancos separados por contexto.](checklist/060-ARCH-BANCOS-SEPARADOS-POR-CONTEXTO.md)
- [✅ Separação entre domínio, aplicação e infraestrutura.](checklist/061-ARCH-SEPARA-O-ENTRE-DOM-NIO-APLICA-O-E-INFRAESTRUTURA.md)
- [✅ Contratos de eventos versionados.](checklist/062-ARCH-CONTRATOS-DE-EVENTOS-VERSIONADOS.md)
- [✅ Uso de Outbox e Inbox Pattern.](checklist/063-ARCH-USO-DE-OUTBOX-E-INBOX-PATTERN.md)
- [✅ Consistência eventual implementada.](checklist/064-ARCH-CONSIST-NCIA-EVENTUAL-IMPLEMENTADA.md)
- [✅ Auditoria implementada.](checklist/065-ARCH-AUDITORIA-IMPLEMENTADA.md)
- [✅ Controle de versão para atualizações.](checklist/066-ARCH-CONTROLE-DE-VERS-O-PARA-ATUALIZA-ES.md)
- [⚠️ Uso de SOLID e boas práticas é observável na estrutura, mas não existe avaliação formal de cada princípio.](checklist/067-ARCH-USO-DE-SOLID-E-BOAS-PR-TICAS-OBSERV-VEL-NA-ESTRUTURA-MAS-N.md)
- [✅ Padrões arquiteturais estão documentados em `docs/architecture/decisions.md`.](checklist/068-ARCH-PADR-ES-ARQUITETURAIS-EST-O-DOCUMENTADOS-EM-DOCS-ARCHITECT.md)
- [✅ Trade-offs documentados.](checklist/069-ARCH-TRADE-OFFS-DOCUMENTADOS.md)
- [✅ Decisões arquiteturais documentadas.](checklist/070-ARCH-DECIS-ES-ARQUITETURAIS-DOCUMENTADAS.md)
- [✅ Diagrama de componentes C4 nível 3.](checklist/071-ARCH-DIAGRAMA-DE-COMPONENTES-C4-N-VEL-3.md)
- [✅ Diagrama dos principais fluxos descrito na documentação operacional e no README.](checklist/072-ARCH-DIAGRAMA-DOS-PRINCIPAIS-FLUXOS-DESCRITO-NA-DOCUMENTA-O-OPE.md)

## Documentação

- [✅ README principal.](checklist/073-DOC-README-PRINCIPAL.md)
- [✅ Documentação de infraestrutura.](checklist/074-DOC-DOCUMENTA-O-DE-INFRAESTRUTURA.md)
- [✅ Documentação básica do frontend.](checklist/075-DOC-DOCUMENTA-O-B-SICA-DO-FRONTEND.md)
- [✅ Documentação de contratos de eventos.](checklist/076-DOC-DOCUMENTA-O-DE-CONTRATOS-DE-EVENTOS.md)
- [✅ Documento arquitetural geral em `docs/architecture/`.](checklist/077-DOC-DOCUMENTO-ARQUITETURAL-GERAL-EM-DOCS-ARCHITECTURE.md)
- [✅ Diagrama geral da solução — C4 nível 1.](checklist/078-DOC-DIAGRAMA-GERAL-DA-SOLU-O-C4-N-VEL-1.md)
- [✅ Diagrama de containers — C4 nível 2.](checklist/079-DOC-DIAGRAMA-DE-CONTAINERS-C4-N-VEL-2.md)
- [✅ Diagrama de componentes — C4 nível 3.](checklist/080-DOC-DIAGRAMA-DE-COMPONENTES-C4-N-VEL-3.md)
- [✅ Fluxo de lançamentos documentado.](checklist/081-DOC-FLUXO-DE-LAN-AMENTOS-DOCUMENTADO.md)
- [✅ Fluxo do consolidado documentado.](checklist/082-DOC-FLUXO-DO-CONSOLIDADO-DOCUMENTADO.md)
- [✅ Fluxo de falha e recuperação documentado.](checklist/083-DOC-FLUXO-DE-FALHA-E-RECUPERA-O-DOCUMENTADO.md)
- [✅ Requisitos não funcionais registrados.](checklist/084-DOC-REQUISITOS-N-O-FUNCIONAIS-REGISTRADOS.md)
- [⚠️ Metas de disponibilidade estão descritas conceitualmente, mas não há ambiente produtivo HA validado.](checklist/085-DOC-METAS-DE-DISPONIBILIDADE-EST-O-DESCRITAS-CONCEITUALMENTE-M.md)
- [✅ Resultado de performance documentado.](checklist/086-DOC-RESULTADO-DE-PERFORMANCE-DOCUMENTADO.md)
- [✅ Trade-offs documentados.](checklist/087-DOC-TRADE-OFFS-DOCUMENTADOS.md)
- [⚠️ Evoluções futuras estão indicadas pelos trade-offs de produção, mas podem ser detalhadas em uma seção própria.](checklist/088-DOC-EVOLU-ES-FUTURAS-EST-O-INDICADAS-PELOS-TRADE-OFFS-DE-PRODU.md)
- [⚠️ Não há ADRs individuais; as decisões estão consolidadas em `docs/architecture/decisions.md`.](checklist/089-DOC-N-O-H-ADRS-INDIVIDUAIS-AS-DECIS-ES-EST-O-CONSOLIDADAS-EM-D.md)

## Testes

- [✅ Testes arquiteturais.](checklist/090-TEST-TESTES-ARQUITETURAIS.md)
- [✅ Testes unitários.](checklist/091-TEST-TESTES-UNIT-RIOS.md)
- [✅ Testes de integração do Core.](checklist/092-TEST-TESTES-DE-INTEGRA-O-DO-CORE.md)
- [✅ Testes de integração do Summary.](checklist/093-TEST-TESTES-DE-INTEGRA-O-DO-SUMMARY.md)
- [✅ Teste de deduplicação de eventos.](checklist/094-TEST-TESTE-DE-DEDUPLICA-O-DE-EVENTOS.md)
- [✅ Teste de atualização de projeção.](checklist/095-TEST-TESTE-DE-ATUALIZA-O-DE-PROJE-O.md)
- [✅ Teste de exclusão.](checklist/096-TEST-TESTE-DE-EXCLUS-O.md)
- [✅ Teste de consistência do consolidado.](checklist/097-TEST-TESTE-DE-CONSIST-NCIA-DO-CONSOLIDADO.md)
- [✅ Teste de dados obsoletos.](checklist/098-TEST-TESTE-DE-DADOS-OBSOLETOS.md)
- [✅ Teste de Summary indisponível.](checklist/099-TEST-TESTE-DE-SUMMARY-INDISPON-VEL.md)
- [✅ Teste de autorização.](checklist/100-TEST-TESTE-DE-AUTORIZA-O.md)
- [✅ Teste de concorrência de lançamentos.](checklist/101-TEST-TESTE-DE-CONCORR-NCIA-DE-LAN-AMENTOS.md)
- [✅ Teste de Core disponível com Summary fora do ar.](checklist/102-TEST-TESTE-DE-CORE-DISPON-VEL-COM-SUMMARY-FORA-DO-AR.md)
- [✅ Teste de recuperação após retorno do Summary.](checklist/103-TEST-TESTE-DE-RECUPERA-O-AP-S-RETORNO-DO-SUMMARY.md)
- [⚠️ DLQ é configurada e mensagens inválidas são encaminhadas, mas não há teste automatizado dedicado à DLQ.](checklist/104-TEST-DLQ-CONFIGURADA-E-MENSAGENS-INV-LIDAS-S-O-ENCAMINHADAS-MAS.md)
- [⚠️ Reprocessamento está documentado como procedimento manual, sem teste automatizado dedicado.](checklist/105-TEST-REPROCESSAMENTO-EST-DOCUMENTADO-COMO-PROCEDIMENTO-MANUAL-S.md)
- [❌ Teste específico de acesso não autorizado ao `/internal/events`.](checklist/106-TEST-TESTE-ESPEC-FICO-DE-ACESSO-N-O-AUTORIZADO-AO-INTERNAL-EVEN.md)
- [✅ Teste para tipo de lançamento inválido.](checklist/107-TEST-TESTE-PARA-TIPO-DE-LAN-AMENTO-INV-LIDO.md)
- [✅ Teste de idempotência concorrente com a mesma chave.](checklist/108-TEST-TESTE-DE-IDEMPOT-NCIA-CONCORRENTE-COM-A-MESMA-CHAVE.md)
- [✅ Script de carga com resultado real versionado na documentação.](checklist/109-TEST-SCRIPT-DE-CARGA-COM-RESULTADO-REAL-VERSIONADO-NA-DOCUMENTA.md)
- [❌ Testes de carga integrados ao CI.](checklist/110-TEST-TESTES-DE-CARGA-INTEGRADOS-AO-CI.md)
- [❌ Relatório de cobertura documentado.](checklist/111-TEST-RELAT-RIO-DE-COBERTURA-DOCUMENTADO.md)

## Correções prioritárias

1. [✅ Proteger o endpoint `/internal/events`.](checklist/112-FIX-PROTEGER-O-ENDPOINT-INTERNAL-EVENTS.md)
2. [✅ Remover a confiança no header `X-Actor-Id`.](checklist/113-FIX-REMOVER-A-CONFIAN-A-NO-HEADER-X-ACTOR-ID.md)
3. [✅ Validar apenas os tipos `credit` e `debit`.](checklist/114-FIX-VALIDAR-APENAS-OS-TIPOS-CREDIT-E-DEBIT.md)
4. [✅ Corrigir idempotência concorrente.](checklist/115-FIX-CORRIGIR-IDEMPOT-NCIA-CONCORRENTE.md)
5. [✅ Criar teste de indisponibilidade e recuperação do Summary.](checklist/116-FIX-CRIAR-TESTE-DE-INDISPONIBILIDADE-E-RECUPERA-O-DO-SUMMARY.md)
6. [✅ Ajustar o teste de carga para usar o fluxo real.](checklist/117-FIX-AJUSTAR-O-TESTE-DE-CARGA-PARA-USAR-O-FLUXO-REAL.md)
7. [✅ Criar diagramas, decisões arquiteturais e trade-offs.](checklist/118-FIX-CRIAR-DIAGRAMAS-DECIS-ES-ARQUITETURAIS-E-TRADE-OFFS.md)
8. [✅ Documentar DLQ, retry e reprocessamento.](checklist/119-FIX-DOCUMENTAR-DLQ-RETRY-E-REPROCESSAMENTO.md)
9. [✅ Demonstrar resultados reais do teste de 50 RPS.](checklist/120-FIX-DEMONSTRAR-RESULTADOS-REAIS-DO-TESTE-DE-50-RPS.md)

## Evidências principais

- [`4f2a35e` — proteção de eventos internos e fluxo real do teste de carga.](checklist/121-EVD-4F2A35E-PROTE-O-DE-EVENTOS-INTERNOS-E-FLUXO-REAL-DO-TESTE.md)
- [`c9d2ca3` — identidade do ator e validação de tipos.](checklist/122-EVD-C9D2CA3-IDENTIDADE-DO-ATOR-E-VALIDA-O-DE-TIPOS.md)
- [`98d2bdf` — idempotência concorrente preservando conflito para intenção diferente.](checklist/123-EVD-98D2BDF-IDEMPOT-NCIA-CONCORRENTE-PRESERVANDO-CONFLITO-PARA.md)
- [`76d26e1` — teste de indisponibilidade e recuperação do Summary.](checklist/124-EVD-76D26E1-TESTE-DE-INDISPONIBILIDADE-E-RECUPERA-O-DO-SUMMARY.md)
- [`a68f380` — documentação C4, decisões e operação.](checklist/125-EVD-A68F380-DOCUMENTA-O-C4-DECIS-ES-E-OPERA-O.md)
- [`163827a` — teste real de 50 RPS e resultado documentado.](checklist/126-EVD-163827A-TESTE-REAL-DE-50-RPS-E-RESULTADO-DOCUMENTADO.md)
- [`2966ad4` — procedimento de reprocessamento controlado da DLQ.](checklist/127-EVD-2966AD4-PROCEDIMENTO-DE-REPROCESSAMENTO-CONTROLADO-DA-DLQ.md)


