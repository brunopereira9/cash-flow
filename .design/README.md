# Documentação de design

Esta pasta reúne os artefatos de descoberta, decisão arquitetural e investigação técnica do
projeto.

## Convenção de nomenclatura

Todo documento deve seguir o padrão:

```text
TYPE-001-DESCRICAO.md
```

Regras:

- `TYPE` identifica o tipo do artefato;
- o número é sequencial dentro do tipo, com três algarismos;
- `DESCRICAO` usa letras maiúsculas, palavras separadas por hífen e sem acentos;
- o nome do diretório deve refletir o tipo do documento;
- evidências executáveis de uma Spike ficam dentro da pasta da própria Spike.

## Tipos de documento

| Tipo | Diretório | Finalidade |
|---|---|---|
| `DISCOVERY` | `Discoveries/` | registrar problema, contexto, escopo, jornada e decisões iniciais |
| `RFC` | `RFCs/` | propor uma solução que ainda será revisada |
| `ADR` | `ADRs/` | registrar uma decisão arquitetural aprovada e permanente |
| `SPIKE` | `Spikes/` | documentar uma investigação técnica ou experimento controlado |

## Estrutura atual

```text
.design/
├── README.md
├── Frontend/
│   └── DESIGN_SYSTEM.md
├── Discoveries/
│   └── DISCOVERY-001-FLUXO-CAIXA.md
├── RFCs/
│   └── RFC-001-ARQUITETURA-FLUXO-CAIXA.md
├── ADRs/
│   ├── ADR-001-ARQUITETURA-APLICACAO.md
│   ├── ADR-002-PERSISTENCIA-POSTGRES-EFCORE.md
│   ├── ADR-003-SEGURANCA-KEYCLOAK.md
│   └── ADR-004-OBSERVABILIDADE-E-EVENTOS.md
└── Spikes/
    └── SPIKE-001-AUTORIZACAO-IMEDIATA/
        ├── SPIKE-001-AUTORIZACAO-IMEDIATA.md
        └── experiments/
```

Um documento sem artefatos auxiliares fica diretamente dentro do diretório do seu tipo. Quando
precisar de experimentos, imagens ou outros anexos, use uma pasta com o mesmo identificador do
documento, como ocorre com a Spike de autorização.

## Relação entre artefatos

O fluxo recomendado é:

```text
DISCOVERY → RFC → ADR → plano tlc-spec-lean → implementação
                 ↑
               SPIKE
```

- Um `DISCOVERY` descreve o problema e o espaço de decisão.
- Um `RFC` compara alternativas e propõe uma arquitetura.
- Um `ADR` congela uma decisão aprovada, normalmente originada em um RFC.
- Uma `SPIKE` responde uma dúvida técnica com evidência reproduzível; seus experimentos ficam
  dentro da pasta da Spike.
- O plano de implementação deve referenciar os ADRs ativos e não reabrir decisões congeladas sem
  um novo RFC ou ADR de supersessão.

## Design system do frontend

`Frontend/DESIGN_SYSTEM.md` é a fonte oficial para tokens, componentes, tipografia, cores,
espaçamento e linguagem visual do frontend. Alterações visuais devem atualizar esse guia ou
explicar explicitamente por que são específicas de uma tela.

## Links e manutenção

Links entre documentos devem usar caminhos relativos. Ao mover ou renomear um artefato, todos os
links no `.design` e em `.specs/STATE.md` devem ser atualizados na mesma alteração.

Decisões aprovadas também devem aparecer em `.specs/STATE.md`, usando o mesmo identificador do ADR.
