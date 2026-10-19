# PROMPT — novo chat (copiar e colar)

Você é meu agente de correção do servidor **CoC 2015 / UCS Zero** (`coctestezero`).
Antes de qualquer coisa, leia **nesta ordem**:

1. `/home/eg/Downloads/CoC_RC_Handoff_2026-10-19/ESTADO_ATUAL_2026-10-19_0815.md` — **estado real de agora** (leia primeiro).
2. `/home/eg/Downloads/CoC_RC_Handoff_2026-10-19/HANDOFF.md` — diagnóstico antigo de tropas/campanha/popup (use só as partes que não conflitam com o estado atual).
3. `/home/eg/Downloads/CoC_RC_Handoff_2026-10-19/NOTAS_2026-10-19_RC1.md` — histórico detalhado dos testes.

Evidências em `/home/eg/Downloads/CoC_RC_Handoff_2026-10-19/evidence/` e `/home/eg/Downloads/CoC_2015_Workspace_v2/archive/evidencias/`.

## REGRA SUPREMA (acima de todas)

**Eu estou SEMPRE com o jogo aberto do meu lado.**

- **NÃO TENHA MEDO de abrir `question`** para me perguntar o que apareceu na tela, se mudou, qual número/ícone/cargo/liga/percentual, etc.
- Não presuma, não espere, não ache que "vai incomodar". Perguntar é **mais rápido** do que implementar errado e refazer.
- Em qualquer dúvida visual (troféus, liga, cargo, % de destruição, popup, tropas, chat, resultado de batalha), **pergunte via `question` e espere a resposta** antes de continuar.
- Eu consigo ver a tela enquanto jogo; use isso a favor do diagnóstico.

## ECONOMIA DE CONTEXTO (obrigatória)

- O chat tem **~200k de contexto**. Não leia o código inteiro nem varra o repositório por padrão.
- **Nunca releia tudo.** Leia só o arquivo/trecho necessário: primeiro `grep`/`glob`, depois `read` com `offset`/`limit`.
- Não repita arquivos já lidos; use o `ESTADO_ATUAL` e as notas como memória.
- Evite `cat` de arquivos grandes e `git diff` gigante. Prefira buscas focadas.
- Se o contexto estiver enchendo: **atualize o handoff e continue em nova sessão**, em vez de reler o mundo.

## Regra de ouro

**Uma mudança por vez.** Depois de cada mudança: build, restart, avisar, abrir uma `question`, esperar eu testar no celular e anotar o resultado. Nunca declare "pronto/concluído" sem a minha validação no jogo. Estou sempre com o jogo aberto; peça ações in-game via `question`. Prove tudo com **log + DB + pacote gerado/captura**. Não regredir R-T7 (doações, login, vínculo de conta, mural).

## Estado em uma frase

O servidor está rodando (pid **105609**, iniciado **07:43:02**, watcher 103130) com o binário que tem **R-C1 (promoção)**, **R-C2a (ordem 0-based)**, **R-C3 (clã 8000/1)** validados e a **instrumentação do comando 603**. O próximo problema é o **R-C4: ataque multiplayer não atualiza troféus**; o cliente manda o comando **603** com 4 bytes (provável % de destruição ×100) e o servidor ignora.

## O que já foi validado (NÃO REFAZER)

- **R-C1 promoção:** Ancião e Co-líder funcionam; "Meu Clã" e perfil mostram o cargo; DB `role: 3`. Log `[UCS] Promote: vizinho -> role 3`.
- **R-C2a ordem 0-based no 24301:** líder em 1º; liga Campeão agora aparece certa no clã/chat.
- **R-C3 clã:** 24301 manda score no 1º dos 9 zeros e nível no 10º; clã migrado para **8000/1/0**; "Meu Clã" e busca mostram **8000/1**. Usuário: "EXATO!! FUNCIONOU!".
- DB clã 2: `score=8000`, `level=1`, `exp=0`, `members=[{1,role2},{2,role3}]`. Jogadores 1 e 2: score 4000.

## Próximo passo (R-C4)

1. Fazer **um ataque controlado** e anotar:
   - troféus do atacante e do defensor (topo da vila inimiga);
   - **% de destruição** e **delta de troféus** mostrados na tela de resultado.
2. Comparar o % com o hex do 603 em `bin/Release/unknown-commands.log` (amostras: `00000236` = 566 e `00000115` = 277).
3. Implementar:
   - `EndOfBattleCommand` para `0x025B` (lê o int do 603);
   - guardar adversário (id + score) no `SearchOpponentCommand`;
   - aplicar o resultado no `GoHomeMessage` (vitória se destruição ≥ 50%);
   - atualizar os dois scores, `Alliance.RecalculateScore()` nos dois clãs e salvar jogadores + alianças.
4. Só depois voltar para tropas/persistência/campanha/chat/battle log.

## Arquivos-chave

- Source: `/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/`
- Log: `/home/eg/Downloads/coctestezero/server/ucs-zero.log`
- DB: `/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/ucsdb`
- Dump 603: `/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/unknown-commands.log`
- Backup pré-R-C3: `/home/eg/Downloads/CoC_2015_Workspace_v2/backups/ucsdb-zero-2026-10-19-0730-antes-RC3.db`
- Git: HEAD `cb88f4c`, working tree **uncommitted** (não resetar).

## Primeira resposta esperada

Ler o `ESTADO_ATUAL_2026-10-19_0815.md`, confirmar que entendeu o estado do processo/binário e **abrir a `question` do ataque controlado do R-C4**. Não refaça R-C1/R-C2a/R-C3 e não altere código antes de ler.
