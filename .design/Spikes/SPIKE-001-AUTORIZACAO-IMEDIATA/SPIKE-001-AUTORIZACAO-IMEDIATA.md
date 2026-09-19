# Spike — autorização imediata diante de JWT antigo

## Decisão recomendada

Para esta entrega, `Core.Api` e `Summary.Api` devem aplicar autorização em duas etapas em **toda requisição de negócio**:

1. validar localmente o access token JWT (assinatura/JWKS, emissor, audiência e expiração);
2. consultar online o Keycloak, com credencial de serviço exclusiva da API, para ler o usuário identificado pelo `sub` e seus mapeamentos efetivos de papéis de negócio. A decisão usa somente esse estado retornado agora: `enabled == true` e exatamente um entre `admin`, `operator` e `auditor`.

O JWT autentica a requisição e vincula o sujeito; ele não é a fonte da decisão de autorização de negócio após a mudança. A API não deve confiar em `realm_access`/`resource_access` do token para decidir o papel atual. Um `sub` não encontrado, usuário desabilitado, zero papéis de negócio ou mais de um papel de negócio é uma negação comprovada. Erro de rede, timeout, `5xx`, falha de autenticação da credencial de serviço ou resposta impossível de interpretar é **indisponibilidade do verificador**, não falta de permissão: bloquear sem executar lógica de negócio, nem criar auditoria, outbox ou mudança de projeção.

Esta decisão cumpre literalmente o contrato “após confirmação, a próxima requisição” sem depender de uma janela de cache ou de propagação assíncrona. Não há garantia para uma requisição já em andamento, conforme decisão já registrada no discovery.

## Protocolo proposto para a futura implementação

1. `JwtBearer` faz a validação criptográfica e temporal normal. Token inválido/expirado, emissor ou audiência errados retorna `401`.
2. Um requisito/handler de autorização ASP.NET Core, comum aos dois processos, extrai o `sub` validado e chama um `CurrentAuthorizationVerifier` com timeout curto e cancelamento ligado à requisição.
3. O verificador obtém `GET /admin/realms/{realm}/users/{sub}` e os mapeamentos efetivos de papel de realm do mesmo usuário (por exemplo, `GET /admin/realms/{realm}/users/{sub}/role-mappings/realm/composite`). O segundo endpoint evita uma conclusão errada se um papel de negócio vier de papel composto. A configuração final precisa escolher deliberadamente realm roles ou client roles e usar os endpoints equivalentes de modo consistente com o mapper do token.
4. Somente após ambas as leituras bem-sucedidas, verifica `enabled` e a cardinalidade dos três papéis. A política da rota então verifica se o único papel atual satisfaz a matriz de acesso.
5. Para decisão negativa comprovada, responder `403` com código estável, por exemplo `authorization_state_denied`; para estado não confirmável, responder `503` com `authorization_verification_unavailable` e `Retry-After`. Não expor detalhes internos do Keycloak. Métricas, logs e rastros distinguem os dois resultados.

O handler deve ser aplicado antes de qualquer handler de endpoint e antes de abrir transação de mutação. Uma resposta de indisponibilidade deve ser deliberadamente convertida em `503`; o `AuthorizationHandler` padrão apenas produz falha de autorização, portanto será preciso um `IAuthorizationMiddlewareResultHandler` (ou middleware de resultado equivalente) que preserve essa semântica sem transformar indisponibilidade em `403`.

### Credencial e operação no Compose

- Criar um client confidencial de máquina para cada API (ou escopos separados se um único client for inevitável), com service account e somente as permissões administrativas mínimas de leitura que o experimento efetivamente comprovar. Nunca reutilizar a credencial que altera usuários/papéis no módulo Identity e nunca enviá-la ao navegador.
- Buscar e renovar o token de service account internamente; mascarar segredos, access tokens e respostas de administração em logs/traces.
- `Core.Api` e `Summary.Api` apontam para a URL interna do Keycloak no Compose. O issuer/JWKS do JWT deve continuar configurado para a URL que realmente consta no token e é alcançável pelos dois contextos, conforme o spike de identidade já previsto.
- Definir timeout inicial pequeno (por exemplo, 100 ms) e limite de conexões/retries explícitos; isso é uma hipótese a medir, não um SLO já provado. Não repetir automaticamente uma consulta de autorização em uma mutação, pois a primeira falha deve continuar fail-closed.
- Não usar circuit breaker que devolva uma decisão permissiva, cache TTL positivo/negativo, stale-while-revalidate ou fallback para claims do JWT. Eles reintroduzem uma janela em que a “próxima requisição” aceita estado revogado.

## Por que não somente introspecção

O endpoint de introspecção do Keycloak informa se o token está ativo e exige client confidencial. Ele é útil como verificação adicional de atividade/sessão se o experimento mostrar que atende ao caso, mas não é a fonte escolhida para o papel vigente: os papéis retornados podem ser mapeados a partir das claims do token, e o comportamento configurado de mappers de introspecção não demonstra, por si só, que uma mudança de papel posterior foi reavaliada. A própria documentação do Keycloak também afirma que encerrar sessões não revoga access tokens pendentes em geral; portanto não se deve inferir revogação imediata por logout.

Introspecção por requisição, sem a leitura atual de usuário e mapeamentos, fica rejeitada para este requisito. Pode ser adicionada depois da leitura administrativa somente se for necessário provar que o token foi revogado, mas aumenta uma chamada remota e disponibilidade acoplada sem substituir a leitura do estado atual.

## Alternativas comparadas

| Mecanismo | Próxima requisição após a confirmação | Impacto em 50 req/s | Falha do Keycloak | Segurança/operação | Veredito |
|---|---|---|---|---|---|
| Validar JWT local e claims | Não: token antigo conserva `enabled`/papéis até expirar | Excelente | Continua aceitando token válido | Simples, mas viola o requisito | Rejeitado |
| Introspecção online apenas | Prova atividade do token, mas não prova papel atual sem demonstrar reavaliação | Uma chamada síncrona por requisição | Pode falhar fechado | Client confidencial; sem ganho suficiente para papel | Rejeitado como solução única |
| Admin REST online: usuário + papéis efetivos | Sim, quando a alteração já foi confirmada pelo mesmo Keycloak consultado | Duas leituras por requisição; ~100 leituras/s no cenário de 50 req/s de Summary.Api, mais tráfego de Core.Api | `503`, bloqueia com segurança | Dois clients/segredos de leitura, timeout e pool HTTP | **Escolhido** |
| Réplica local de autorização por eventos | Não necessariamente: é assíncrona; exigiria barreira/ack global antes de confirmar a alteração | Muito baixo no caminho de leitura | Pode manter estado velho; fail-closed só evita aceitar depois de detectar a falha | Novo stream, armazenamento, ordem, replay, invalidação e monitoramento | Adiar; alternativa de escala futura |
| Estado compartilhado síncrono (por exemplo, Redis) atualizado pela tela | Pode atender apenas com escrita durável e confirmação de visibilidade para ambas APIs antes de responder | Baixo na leitura, uma leitura remota | Se indisponível, bloquear | Ainda cria fonte de autorização paralela ao Keycloak, reconciliação e recuperação | Complexidade injustificada no teste |

O custo da opção escolhida é intencional: a disponibilidade de operações protegidas fica dependente do Keycloak, como o discovery já aceita. Ela não conflita com o requisito de que a queda de `Summary.Api` não bloqueie Lançamentos; são dependências diferentes. A meta de 50 req/s é do consolidado e continua mensurável, mas não pode ser alegada como cumprida antes de medir as duas chamadas administrativas, o pool HTTP e o Keycloak no Compose.

## Fatos comprovados no experimento isolado

Foi executado o artefato reproduzível [`keycloak-current-auth`](SPIKE-001-AUTORIZACAO-IMEDIATA/experiments/keycloak-current-auth) contra `quay.io/keycloak/keycloak:26.7.4`, em `start-dev`, publicado apenas em `localhost:18080`. Ele cria um realm efêmero, um usuário com papel `operator`, dois clients confidenciais independentes (`core-auth-reader` e `summary-auth-reader`) e uma simulação mínima dos dois verificadores lógicos. Os binários gerados foram limpos após a execução.

O comando executado foi:

```powershell
docker run -d --name keycloak-current-auth-spike -p 18080:8080 -e KC_BOOTSTRAP_ADMIN_USERNAME=admin -e KC_BOOTSTRAP_ADMIN_PASSWORD=admin quay.io/keycloak/keycloak:26.7.4 start-dev
dotnet run --project .design/Spikes/SPIKE-001-AUTORIZACAO-IMEDIATA/experiments/keycloak-current-auth/KeycloakCurrentAuthSpike.csproj
```

Resultado da última rodada:

| Verificação | Resultado |
|---|---|
| JWT emitido antes das mudanças | continha `operator` antes de a conta/papel serem alterados |
| `Core` lógico, após desativação | leu `enabled=False`, preservando o JWT antigo como entrada |
| `Summary` lógico, após desativação | leu `enabled=False`, preservando o JWT antigo como entrada |
| `Core` lógico, após `operator` → `auditor` | leu unicamente `auditor` com o mesmo JWT antigo |
| `Summary` lógico, após `operator` → `auditor` | leu unicamente `auditor` com o mesmo JWT antigo |
| Privilégios da service account | somente `view-users` e `view-realm` do client `realm-management` bastaram para `GET user` e role mappings efetivos neste Keycloak/realm |
| Carga curta | 500 verificações lógicas em 10 s, agendadas a 50/s; zero falhas; p95 de 253,0 ms por verificação, cada uma com duas leituras administrativas; 1.000 leituras no total |
| Keycloak interrompido | os dois leitores receberam falha de transporte; o harness classificou o estado como não confirmável e tomou a decisão fail-closed `503 authorization_verification_unavailable` |

Isto prova que os endpoints escolhidos refletem imediatamente a desativação e a troca de papel já confirmadas no Keycloak e que a separação `403`/`503` é implementável no adaptador de autorização. Não prova ainda a integração HTTP de `Core.Api`/`Summary.Api`, a validação criptográfica do JWT pelo middleware ASP.NET Core, nem o requisito de carga de cinco minutos do produto.

## Evidências documentais verificadas

- A documentação do Keycloak declara que a introspecção retorna o estado ativo de access ou refresh token e somente clients confidenciais podem chamá-la. Também documenta que claims de introspecção são configuráveis por mappers.
- O guia administrativo do Keycloak declara que encerrar sessões não revoga access tokens pendentes; eles expiram naturalmente, salvo mecanismos específicos para adapters. Backchannel logout é notificação para clientes e não substitui a decisão online destas APIs .NET.
- A referência Admin REST expõe a leitura do usuário e de seus realm-role mappings; a referência também expõe o endpoint de mappings efetivos/composite.
- A documentação do ASP.NET Core exige validação de assinatura, issuer, audience e expiração para JWT bearer; suas políticas suportam requirements/handlers, e o evento `OnTokenValidated` ocorre após a validação. A verificação atual de autorização deve ficar no requisito/política, não substituir a validação do bearer token.

## Testes de encerramento do spike

O experimento isolado executou o núcleo de 1, 2, 4 e parte de 6 contra Keycloak real. Estes continuam sendo os critérios de encerramento da futura implementação, pois só ela pode provar que nenhuma operação de negócio foi iniciada:

1. Emitir um token de `operator`, desativar o usuário pela tela (o backend altera Keycloak), aguardar a confirmação da alteração e chamar uma rota de negócio em `Core.Api` e uma de `Summary.Api` com o mesmo token. Ambas devem negar; uma mutação deve comprovar zero novas linhas em lançamento, auditoria e outbox.
2. Emitir token de `admin`, trocar o papel para `operator`, e chamar uma rota exclusiva de `admin` nas duas APIs com o token antigo. Deve retornar `403`; uma rota permitida ao novo papel deve obedecer à matriz real. Repetir `operator` → `auditor` e verificar que mutação é negada e leitura permitida.
3. Criar estado ambíguo com dois papéis e estado sem papel diretamente no Keycloak. Ambas as APIs devem responder `403` e não executar alteração. Validar também `sub` inexistente e `enabled=false`.
4. Interromper ou isolar a conectividade de cada API com Keycloak depois de o JWT já ter passado na validação. Cada rota de negócio deve devolver `503` com o código de indisponibilidade; as tabelas de negócio, auditoria e outbox devem permanecer idênticas. Restaurar o acesso e confirmar retorno à decisão atual.
5. Registrar tracing/métricas separados para `token_invalid`, `authorization_denied` e `authorization_verifier_unavailable`, sem token, senha, segredo ou atributos pessoais sensíveis.
6. Repetir o teste de carga definido no discovery: `GET daily-summary`, 50 req/s por 5 min sobre a massa concentrada, com a verificação online ativa. A rodada isolada de 10 s mediu p95 de 253,0 ms e zero falhas para 500 verificações/1.000 leituras; ela é sinal favorável, não substitui os 5 min, a massa real nem prova o limite de até 5%.

## Riscos residuais e evolução

- A semântica depende de “confirmação” significar que o Admin REST do Keycloak já lê o estado novo. O teste deve provar isso para a versão de imagem fixada no Compose.
- Duas leituras podem observar estados intermediários se a troca de papel for implementada como remover e adicionar em chamadas separadas. O módulo Identity deve serializar a operação e só responder sucesso depois do estado final ser lido; durante uma observação intermediária, negar é seguro, ainda que temporariamente indisponibilize o usuário.
- As permissões mínimas exatas da service account e o comportamento de roles via grupos/composites dependem da configuração final do realm; ambos são itens de teste, não suposições deste documento.
- Um único Keycloak em `start-dev` no Compose é ponto único de falha. Isso é aceitável para demonstrar fail-closed localmente, não é desenho de alta disponibilidade de produção.
- Se um requisito futuro exigir manter autorização disponível durante a queda do IdP, será necessária uma fonte de estado revogável e fortemente sincronizada, com protocolo de confirmação e reconciliação; não basta cachear JWT.

## Fontes primárias

- [Keycloak — OIDC layers: introspection endpoint](https://www.keycloak.org/securing-apps/oidc-layers)
- [Keycloak — Server Administration Guide: sessões, logout e endpoints OIDC](https://www.keycloak.org/docs/latest/server_admin/)
- [Keycloak — Admin REST API](https://www.keycloak.org/docs-api/latest/rest-api/index.html)
- [ASP.NET Core — configurar JWT bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [ASP.NET Core — autorização baseada em políticas](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies)
- [ASP.NET Core — `JwtBearerEvents`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbearerevents)
