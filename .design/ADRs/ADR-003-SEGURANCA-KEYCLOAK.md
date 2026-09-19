# ADR-003-SEGURANCA-KEYCLOAK

Status: aprovado  
Origem: [RFC-001-ARQUITETURA-FLUXO-CAIXA](../RFCs/RFC-001-ARQUITETURA-FLUXO-CAIXA.md)

## Decisão

Usar Authorization Code + PKCE no frontend, access token com duração de uma hora e validação
local de assinatura, issuer, audience e expiração. A autorização de negócio consulta o estado
atual do usuário e seus papéis no Keycloak em toda requisição.

O verificador terá timeout de `2 s`, configurável, sem fallback permissivo. Falha de verificação
retorna `503` e não inicia operação de negócio. Haverá credenciais separadas para leitura do Core,
leitura do Summary e administração de usuários.

As contas `admin`, `operador` e `auditor` serão criadas para demonstração local, com credenciais
documentadas como inseguras e não utilizáveis em produção.

## Consequências

Desativação e troca de papel têm efeito na próxima requisição mesmo com token antigo. A
disponibilidade das operações protegidas depende do Keycloak, por decisão consciente de segurança.
