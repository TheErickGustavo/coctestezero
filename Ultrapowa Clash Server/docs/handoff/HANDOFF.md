> **ATENÇÃO — LEIA PRIMEIRO:**
> Este `HANDOFF.md` é de 06:15 e está **desatualizado** no estado do servidor e no plano.
> O estado real de 19/10 **08:15** está em
> `/home/eg/Downloads/CoC_RC_Handoff_2026-10-19/ESTADO_ATUAL_2026-10-19_0815.md`.
> Use este arquivo apenas para o diagnóstico antigo de tropas/campanha/popup.
> R-C1 (promoção), R-C2a (ordem 0-based) e R-C3 (clã 8000/1) já foram **validados pelo usuário** — não refazer.
> O próximo passo é **R-C4: comando 603 / troféus de batalha PvP**.
>
> **REGRA SUPREMA:** o usuário está SEMPRE com o jogo aberto do lado dele. A IA deve **abrir `question` SEM MEDO** para perguntar o que apareceu na tela, se mudou, qual número/ícone/cargo/liga/percentual etc. Perguntar é mais rápido do que implementar errado.
> **ECONOMIA DE CONTEXTO:** o chat tem ~200k; não reler o código inteiro. Usar `grep`/`glob` e `read` com `offset`/`limit`; não repetir arquivos já lidos; se encher, atualizar o handoff e continuar em nova sessão.

# HANDOFF — CoC 2015 / UCS Zero — 2026-10-19 06:15 (horário local)

> **Leia este arquivo inteiro antes de tocar no servidor.**
> Ele substitui as afirmações otimistas do `HANDOFF.md` antigo e do `SPEC.md`.
> Status: **nada foi validado pelo usuário ainda**. O que está marcado [V] é prova de código/log/DB/captura; [P] é hipótese forte; [?] é desconhecido.

---

## 0. Resumo em linguagem simples

1. **O pacote de correções “R-C” existiu, foi commitado e depois foi desfeito.**
   - Commit `e4e3648` (19/10 05:15:22): “R-C: promote (14306), persistencia no logout, clã score/nível, liga por troféus, TH level avatar, suporte N contas”.
   - Logo depois, `git reset` voltou para `cb88f4c` (o commit anterior). O `git reflog` prova isso.
   - O `ucs.exe` em disco foi recompilado às 05:22 **sem** essas correções.
   - O processo que está rodando agora (pid 94733, iniciado 05:14:38) ainda tem parte do pacote R-C na memória. **Se ele reiniciar, o servidor volta a ficar pior** (14306 some, liga volta a Ouro 1, save no logout some, clã volta a 10.000/nível 10).
2. **A promoção para Ancião falhou mesmo quando o handler estava ativo**, por um bug de comparação de cargos: `if (m_vRole >= requesterMember.GetRole()) return;` — Ancião = 3, Chefe = 2, então `3 >= 2` e o método retorna sem promover. O log mostra 14306 chegando 2x, mas **não** mostra a linha `[UCS] Promote:`; o DB continua com os cargos antigos.
3. **Clã 10.000 troféus / nível 10**: esses valores estão gravados no DB desde a criação do clã (defaults do construtor). A busca (`24310`) lê exatamente esses valores. O “Meu Clã” (`24301`) não manda score/nível no cabeçalho (manda 9 zeros), então mostra 0/nível 1.
4. **Tropas**: o DB prova contadores negativos e desalinhados com os acampamentos. Ex.: 17 bárbaros nos camps, mas `-22` no avatar; 11 gigantes nos camps, `-9` no avatar; 39 arqueiros nos camps, `0` no avatar; 4 dragões nos camps, `5` no avatar. O ataque usa o avatar, não os camps.
5. **Campanha**: os dois goldens de PvE são replays com o **avatar da captura** (id 3752, sem clã, F10=3), não com o avatar do jogador. E as 3 listas de progresso NPC do avatar são sempre zeradas no `Encode()`. Por isso só aparecem as vilas do tutorial e as tropas não batem.
6. **“CV 3”**: o pacote R-C chutou que o campo `F10` do avatar era o TH (`GetTownHallLevel() + 1`). O binário rodando gera `F10 = 10` (TH 9 + 1). Se o popup **ainda** mostra 3, então `F10` **não** é o campo. O avatar do golden de PvE tem `F10=3`, o que pode explicar a confusão de “CV3” na campanha.
7. **O commit `e4e3648` é recuperável** (está no reflog e o patch completo foi salvo em `evidence/e4e-RC-promote-persist-clan-league-TH.patch`). Não é “tudo perdido”; dá para restaurar a base e corrigir o que estava errado.

---

## 1. Regras do usuário (invioláveis)

- **Uma mudança de cada vez.** Não juntar 3 correções e pedir “testa tudo”.
- Depois de cada mudança: **build**, **restart**, avisar o usuário, **abrir uma `question`**, esperar a resposta e anotar o resultado.
- **Nunca** declarar “pronto/concluído” sem o usuário validar no celular.
- O usuário está sempre com o jogo aberto. Pedir ações in-game via `question`.
- Prova vem de **log + DB + captura**. O vídeo antigo não será enviado.
- Não regredir o que já funciona de R-T7 (doações, login, vínculo de conta, mural).
- Suportar mais contas (2, 5, 10) é requisito futuro, **não agora**.
- Se o usuário disser “não mudou nada”, **parar** e comparar o binário em execução com o source antes de propor outra coisa.

---

## 2. Ambiente e comandos

### Caminhos
- Repositório: `/home/eg/Downloads/coctestezero/server` (o `.git` fica aqui; o projeto C# está em `Ultrapowa Clash Server/`).
- Projeto: `/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/`.
- Binário: `.../bin/Release/ucs.exe` (assembly Mono, ~296 KB).
- DB: `.../bin/Release/ucsdb` (SQLite; **não há CLI `sqlite3` instalado**; usar Python `sqlite3`).
- Log: `/home/eg/Downloads/coctestezero/server/ucs-zero.log`.
- Config: `.../bin/Release/ucsconf.config`.
- Build: `bash /home/eg/Downloads/coctestezero/scripts/build-server.sh`.
- Watcher: `/home/eg/Downloads/coctestezero/scripts/watch-server.sh` (reinicia `mono ucs.exe` em 3s).
- ADB: `export PATH=/home/eg/Downloads/coc-2015-lab/tools/platform-tools:$PATH`; device `RQ8N709D49P`; app `com.supercell.coctestezero`; `adb reverse tcp:9341 tcp:9340`.

### Verificações antes de qualquer teste
```bash
ps -eo pid,lstart,cmd | grep 'mono ucs.exe' | grep -v grep
stat -c '%y %s %n' '/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/ucs.exe'
cd /home/eg/Downloads/coctestezero/server && git rev-parse --short HEAD && git reflog -5
```
**Regra de ouro:** se o `ucs.exe` for mais novo que o processo, o processo está rodando o binário antigo. Rebuild sem restart não muda nada; restart sem rebuild carrega o binário em disco (que pode estar pior).

### Build + restart (quando for a hora)
```bash
bash /home/eg/Downloads/coctestezero/scripts/build-server.sh
pkill -xf "mono ucs.exe"    # o watcher reergue em 3s
```
Depois confirmar que o processo novo subiu e que o log não tem stack trace.

### DB (Python)
```python
import sqlite3, json
con = sqlite3.connect('/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/ucsdb')
# tabelas: Player(PlayerId, AccountStatus, AccountPrivileges, LastUpdateTime, Avatar TEXT, GameObjects TEXT)
#          Clan(ClanId, LastUpdateTime, Data TEXT)
```

---

## 3. Estado atual (19/10/2026 ~06:15)

### Git
- `HEAD = cb88f4c` (X4c-fix, 18/10 23:33).
- `e4e3648` (R-C) **não está em nenhum branch**; só no reflog.
- `git reflog`:
  ```
  cb88f4c HEAD@{0}: reset: moving to HEAD~1
  e4e3648 HEAD@{1}: commit: R-C: promote (14306), persistencia no logout, clã score/nível, liga por troféus, TH level avatar, suporte N contas
  cb88f4c HEAD@{2}: commit: X4c-fix...
  ```
- `git diff e4e3648` (working tree) mostra que faltam justamente 4 arquivos:
  - `Core/ResourcesManager.cs` (save no DropClient)
  - `Logic/Alliance.cs` (defaults 0/0/1)
  - `Logic/ClientAvatar.cs` (UpdateLeague/SetScore, F10 = TH+1)
  - `PacketProcessing/MessageFactory.cs` (registros 14262 e 14306)
- Os arquivos Google/Promote continuam staged/na árvore; o handler `PromoteAllianceMemberMessage.cs` é idêntico ao do e4e (inclusive o bug do guard).

### Processo vs binário
- Processo `mono ucs.exe` pid **94733**, iniciado **05:14:38**.
- `ucs.exe` em disco: **05:22:00**, 303.104 bytes (build do source revertido).
- Log: em **05:15** o 14306 foi reconhecido (`[R] 14306 PromoteAllianceMemberMessage`), o que só acontece no binário antigo (o source atual tem o registro comentado).
- `generated-24301.bin` de **05:15** mostra league 16 nos membros (UpdateLeague do e4e).
- `generated-24101.bin` de **05:58** mostra `F10 = 10` (TH 9 + 1 do e4e).
- **Conclusão:** o processo em memória tem o comportamento R-C; o binário em disco é o revertido. Qualquer restart agora regride.

### DB (snapshot em `evidence/ucsdb-snapshot-2026-10-19.db`)
- Player 1 `olamundo`: TH=9, score=4000, gems=131, tutorial=13, avatar_level=500.
- Player 2 `vizinho`: TH=9, score=4000, avatar_level=500.
- Clã 2 `meucla1`: score=10000, alliance_level=10, alliance_experience=8599; membros `{1: role 2 (chefe), 2: role 1 (membro)}`.
- Clã 1 `meucla`: vazio, mesmos defaults 10000/10/8599.
- `npc_stars`, `npc_looted_gold`, `npc_looted_elixir`: vazios.
- `google_bind.json`: não existe (bind sempre retorna `bound=0`).

---

## 4. Achados por sintoma

### R-C1 — “Ancião” não aparece [V]

**Causa 1 (registro):** o source atual tem `MessageFactory.cs:33` comentado:
```csharp
//m_vMessages.Add(14306, );//Promote
```
No e4e era:
```csharp
m_vMessages.Add(14262, typeof(BindGoogleServiceAccountMessage));
m_vMessages.Add(14306, typeof(PromoteAllianceMemberMessage));
```
O binário em execução ainda reconhece 14306; o binário em disco (05:22) **não** reconhece. Após um restart, o cliente manda 14306 e o servidor responde `Unhandled Message`.

**Causa 2 (guard de cargo):** `PromoteAllianceMemberMessage.cs:55-60`:
```csharp
if (!targetMember.HasLowerRoleThan(requesterMember.GetRole()))
    return;

//Nao permite promover acima do proprio cargo.
if (m_vRole >= requesterMember.GetRole())
    return;
```
Cargos: `1 = membro, 2 = chefe, 3 = ancião, 4 = co-líder`. O teste numérico `3 >= 2` é verdadeiro, então **chefe não consegue promover ninguém para ancião**. O `m_vRoleTable = {1,1,4,2,3}` mapeia autoridade (chefe 4 > co-líder 3 > ancião 2 > membro 1), mas o guard não usa essa tabela.

**Prova no log:**
```
48721:[R] 14306 PromoteAllianceMemberMessage (1, olamundo)
48734:[R] 14306 PromoteAllianceMemberMessage (1, olamundo)
```
Nenhuma linha `[UCS] Promote:` depois. DB continua `{1:2, 2:1}`.

**Correção:** registrar 14306 e trocar o guard por comparação de autoridade:
```csharp
if (RoleAuthority(m_vRole) >= RoleAuthority(requesterMember.GetRole()))
    return;
```
onde `RoleAuthority(1)=1, 2=4, 3=2, 4=3` (a mesma tabela). Também validar `m_vRole` em 1..4 e não promover para chefe (2) se o requisitante não for chefe; co-líder pode promover para ancião, etc. Depois do `SetRole`, o cliente já pede `14302` e recebe `24301` atualizado; não precisa de pacote extra, mas salvar o clã imediatamente é recomendável.

---

### R-C2 — Busca de clã mostra 10.000 troféus / nível 10 [V]

**Causa:** o DB tem esses valores. O construtor atual (`Alliance.cs:54-56`) cria clãs com:
```csharp
m_vScore = 10000;
m_vAllianceExperience = 8599;
m_vAllianceLevel = 10;
```
O clã 2 foi criado com esses defaults e nunca recalculou. `EncodeFullEntry()` (usado pelo `24310`) manda `m_vScore`, `m_vAllianceExperience`, `m_vAllianceLevel` direto. O e4e mudou os defaults para `0/0/1`, mas isso **não corrige clãs já existentes** no DB.

**Fórmula real:** no CoC 2015, troféus do clã = soma dos troféus dos membros (até 50). Para 2 membros × 4000 = **8000**. O `24301` real de um clã de 50 membros tem soma 13.645; é o valor esperado na busca. Nível do clã = experiência (guerras); sem guerras, **nível 1 / exp 0**.

**Correção:** no load/save ou antes do encode, recalcular:
- `score = soma(avatar.GetScore())` dos membros;
- `experience/level` reais (para o clã atual, 0/1);
- persistir no DB e mandar no `EncodeFullEntry`.
Cuidado: o usuário pode achar que “deveria ser 4.000” (só ele). Explicar que o jogo real soma os dois; validar no teste.

---

### R-C3 — “Meu Clã” mostra 0 troféus / nível 1 / liga Ouro 1 [V/P]

**O que o `24301` manda:** cabeçalho com 9 zeros (igual ao real), descrição, e entradas de membro com `role, level, league, score, donated, received, 0, order, 0, 0, u8 1, u8 1, id`. O `24301` **não tem campo de score/nível do clã**; o cliente provavelmente calcula a partir dos membros. O real também manda 9 zeros, então o “0 total” não é só um campo faltando.

**Bug encontrado no `24301`:** a ordem do membro está **1-based** no nosso encoder, mas o real é **0-based**:
```
real conn27-121: membro 0 f7=0, membro 1 f7=1, membro 2 f7=2
nosso gerado:    membro 0 f7=1, membro 1 f7=2
```
`AllianceDataMessage.cs` usa `pack.AddInt32(memberOrder + 1)`. O correto é `memberOrder`. Isso pode afetar a ordenação e o cálculo do total no cliente.

**Liga:** o e4e adicionou `UpdateLeague()` (4000 → id 16 = Champion) e o `24301` passou a mandar 16 nos membros (gerado às 05:15). Mas o `ClientAvatar.Encode()` continua mandando **10 fixo** no campo de liga (offset 112) e **não serializa** `m_vLeagueId`. O perfil (`24334`) usa o avatar, então pode continuar mostrando Ouro 1 mesmo com o clã mostrando Champion. Não sabemos ainda qual campo o perfil usa; o real manda 10 em todas as capturas, então provavelmente o cliente deriva a liga do score (F7) ou de outro campo. **Precisa de teste visual separado:** perfil vs lista de membros do clã.

**Correção mínima:** corrigir a ordem para 0-based; recalcular score/nível do clã; manter `UpdateLeague()` no load/score; investigar o campo de liga do perfil antes de mexer no `Encode()`.

---

### R-C4 — Tropas compradas vs tropas no ataque [V]

**Prova no DB (snapshot):**

| Tipo | Acampamentos | Avatar (`units`) | Castelo | Produção |
|---|---:|---:|---:|---:|
| Bárbaro 4000000 | 17 | **-22** | 0 | 5 |
| Arqueiro 4000001 | 39 | 0 | 0 | 0 |
| Gigante 4000003 | 11 | **-9** | 0 | 0 |
| Dragão 4000008 | 4 | 5 | 1 | 7 |
| Mago 4000006 | 0 | 0 | 8 | 0 |

Player 2: 17 bárbaros nos camps vs **-23** no avatar; 40 arqueiros, 20 gigantes, 15 magos nos camps vs 0 no avatar.

**Causas prováveis (todas no código):**
1. `UnitStorageComponent.Load()` carrega os camps, mas **não reconstrói** os contadores do avatar. O avatar é carregado do JSON com os valores antigos (inclusive negativos).
2. `PlaceAttackerCommand.Execute()` e `RemoveUnitsCommand.Execute()` percorrem `GetComponents(0)` e removem de **qualquer** `UnitStorageComponent`, inclusive o **castelo** (1000014). Ao remover tropa doada do castelo, `RemoveUnitsImpl` decrementa o contador do avatar (que nunca foi incrementado por `AddDonatedUnit`), gerando negativo.
3. `UnitProductionComponent.ProductionCompleted()` usa `GetClosestComponent` com filtro tipo 0 e **não exclui a fábrica de feitiços** (`IsSpellForge`). No DB do player 1 há tropas (`4000000`, `4000001`) dentro do storage de feitiços (`1000020`), prova de que tropa foi parar no lugar errado.
4. Não há clamp em 0; `SetUnitCount` aceita negativo e o `Encode()` manda negativo para o cliente.
5. O ataque usa o avatar (`GetUnits()`), não os camps. Então o jogo mostra uma coisa na vila e outra no ataque.

**Correção:** uma única fonte de verdade. No load e antes de salvar/encodar, reconstruir `m_vUnits` do avatar a partir dos camps (1000000), **não** do castelo (1000014) nem da fábrica de feitiços (1000020); clampar em 0. Em `PlaceAttacker`/`RemoveUnits`, remover só de camps para o exército próprio; para o castelo, mexer em `alliance_units`/castelo separadamente. Em `ProductionCompleted`, filtrar `IsSpellForge == m_vIsSpellForge` (ou o prédio correto). Adicionar log de `Unit` e `count` em `TrainUnitCommand`/`PlaceAttacker` para a auditoria exata.

**Auditoria do log (janela 05:16:42–05:18:42):**
- 25 `TrainUnitCommand`, 14 `CancelUnitProductionCommand`, 7 `SpeedUpTrainingCommand`, 39 `PlaceAttackerCommand` (19 no multiplayer + 20 no NPC), 1 `RemoveUnitsCommand`, 2 `SearchOpponent`, 2 `EnemyHomeData`, 1 `AttackNpc`, 1 `NpcData`.
- O log **não** registra o tipo/quantidade de cada `TrainUnit`; por isso o cálculo exato “comprei X, apareceu Y” ainda não é possível. A próxima mudança deve adicionar esse log e pedir um teste controlado (ex.: comprar exatamente 12 dragões, atacar, contar os `PlaceAttacker` de dragão).
- Curiosidade do DB atual: 4 dragões nos camps + 7 na fila + 1 no castelo = 12 dragões “contabilizados”, mas o avatar diz 5. Isso mostra exatamente o desalinhamento.

---

### R-C5 — “Volta ao padrão” / persistência [V]

**O que existe:**
- Save periódico a cada 60s (`ObjectManager.Save`) de todos os levels em memória + alianças.
- O e4e adicionou save imediato no `ResourcesManager.DropClient`:
  ```csharp
  m_vDatabase.Save(new List<Level> { c.GetLevel() });
  m_vDatabase.Save(ObjectManager.GetInMemoryAlliances());
  ```
- O source atual **não tem** isso: `DropClient` só chama `LogPlayerOut`, que remove o level da memória. Se o cliente cair/desconectar antes do tick de 60s, as mudanças são perdidas.
- O binário em execução provavelmente tem o save (e4e); o em disco não.

**Outros fatores:**
- O log mostra várias mortes do servidor antes de 05:14 (código 1 e 137/OOM). Cada morte sem save perde a janela.
- Os contadores negativos são salvos e recarregados; não é “reset”, é estado corrompido persistido.
- O `ClientAvatar.Encode()` manda os contadores do avatar, não os camps; então o ataque pode parecer “zerado” mesmo com a vila cheia.

**Correção:** restaurar o save no `DropClient` (e salvar aliança após promote/kick), além de sincronizar os contadores antes de salvar. Teste: comprar 1 tropa, fechar/abrir o jogo em <60s, ver se persiste.

---

### R-C5b — Campanha goblin só tutorial / tropas erradas [V]

**Golden de PvE:** `NpcDataMessage.Encode()` lê `gamefiles/pve/golden-level-0x<LevelId>.bin` e manda os bytes **verbatim**. Só existem dois:
- `golden-level-0x01036640.bin` (2299 B)
- `golden-level-0x01036641.bin` (2703 B)

Dentro deles:
- `i32 0`, `i32 jsonLen`, JSON do mapa, **avatar da captura** (id **3752**, sem clã, `F10=3`), e **nada depois**.
- O avatar do golden **não é o do jogador**. Por isso a tela de ataque da campanha mostra as tropas/TH da captura, não as suas.

**Fallback para outros níveis:** `NpcDataMessage` monta `i32 0, i32 jsonLen, JSON, avatar do jogador, i32 0, i32 LevelId`. O golden **não tem** os 8 bytes finais (`i32 0, i32 LevelId`). Esse formato inventado pode ser o motivo de os níveis além dos 2 tutoriais não abrirem.

**Progresso:** o `ClientAvatar.Encode()` sempre manda 3 listas NPC zeradas + 1 lista final zerada. O `NpcStars`/`NpcLootedGold`/`NpcLootedElixir` são salvos no JSON do avatar, mas **nunca encodados**. O DB está vazio. Sem isso o cliente não marca estrelas nem libera as próximas vilas. `MissionProgressCommand`/`Unknown539Command` só logam; `GoHome` só mexe no tutorial para os IDs 0x01036640/41.

**Correção:** gerar o 24133 com o JSON do nível + **avatar do jogador**, sem os 8 bytes extras; registrar o resultado da batalha NPC (estrelas/loot) e encodar as listas no avatar; testar um nível além do tutorial.

---

### R-C6 — Popup “Centro de Vila nível 3” [P]

**O que o e4e fez:** trocou `data.AddInt32(3)` por `data.AddInt32(GetTownHallLevel() + 1)` no `ClientAvatar.Encode()` (F10). O binário rodando gera `F10=10` (TH 9 + 1) no `generated-24101.bin` de 05:58. O `24262` usa o mesmo `Encode()`.

**Se o popup ainda mostra 3 com esse binário, F10 não é o campo.** O avatar do golden de PvE tem `F10=3`, então o “CV3” pode vir da campanha, não da troca de vila. Não sabemos ainda qual campo o popup lê. Candidatos: `B` (hoje 2), o campo de liga (10), ou algum dos 9 zeros do bloco de clã. Fazer **um teste por vez**, trocando um campo e pedindo o número exato do popup.

**Teste zero-código (fazer primeiro):** com o binário atual (F10=10), trocar de vila e reportar o número. Se aparecer 3, F10 está descartado. Se aparecer 10, F10 é o campo e o problema é o valor/TH.

---

### R-C7 — Mais contas (2 → 5/10) [P]

`GoogleBindService.GetOtherPlayerId()` percorre o DB por ID e retorna o próximo jogador. Com 2 contas funciona; com N, ele cicla, mas o popup nativo só oferece **um** alvo por vez. Não há lista/escollha de conta. O `google_bind.json` nem existe porque o fluxo sempre mostra “já vinculado” (`bound=0`). Deixar para depois de R-C5; não misturar.

---

## 5. Evidências salvas

Pasta: `/home/eg/Downloads/CoC_RC_Handoff_2026-10-19/evidence/`
- `ucsdb-snapshot-2026-10-19.db` — DB no momento do diagnóstico.
- `generated-24301-live-0515.bin` — 24301 gerado pelo binário em execução (league 16, ordem 1-based).
- `generated-24101-live-0558.bin` — 24101 gerado pelo binário em execução (F10=10).
- `ucs-zero.log` — log completo.
- `ucs-on-disk-0522.exe` — binário em disco (revertido, 05:22).
- `e4e-RC-promote-persist-clan-league-TH.patch` — patch completo do commit R-C perdido.

Capturas reais úteis:
- `/home/eg/Downloads/CoC_2015_Workspace_v2/archive/evidencias/artifacts-2026-10-18-X0-clan/decode/conn27-s2c-121-24301.bin` (clã 2 membros; ordem 0-based; league 16).
- `.../conn27-s2c-133-24301.bin` (3 membros).
- `.../conn27-s2c-25-24301.bin` (50 membros; soma 13.645).
- Goldens: `.../bin/Release/gamefiles/pve/golden-level-0x01036640.bin` e `...41.bin`.

---

## 6. Plano de correção — uma mudança por vez

> **Não comece pelo R-C6 nem pelo R-C7.** A ordem abaixo é para evitar regressão e dar vitórias rápidas ao usuário.

### Passo 0 — Antes de buildar (sem código)
1. Confirmar `git rev-parse --short HEAD` = `cb88f4c` e que `e4e3648` está no reflog.
2. Perguntar ao usuário (via `question`), **sem mexer em nada**:
   - “Troca de vila agora e me diz o número exato que aparece no popup (3? 10?).”
   - “O ícone Ouro 1 aparece no seu perfil ou na lista de membros do clã?”
   - “A promoção para Ancião ainda não aparece, certo?”
   Isso decide R-C6 e R-C3 sem build.

### Passo 1 — R-C1: promoção (primeira correção de código)
- Restaurar do e4e os 4 arquivos que faltam (`ResourcesManager`, `Alliance`, `ClientAvatar`, `MessageFactory`) **para não regredir** o que o binário em execução já tem.
- Corrigir o guard de cargo para usar a tabela de autoridade (`1→1, 2→4, 3→2, 4→3`).
- Registrar 14306 no `MessageFactory`.
- Build + restart.
- **Teste do usuário:** promover `vizinho` para Ancião; abrir “Meu Clã”; ver se aparece “Ancião”. Conferir DB `members` com role 3.
- Só passar ao próximo depois do “funcionou”.

### Passo 2 — R-C2/R-C3: clã
- Recalcular `score` do clã pela soma dos membros (8000 para 2×4000) e nível/exp reais (1/0 no clã atual); persistir.
- Corrigir `memberOrder + 1` para `memberOrder` (0-based).
- Testar: busca de clã (deve mostrar 8000 e nível 1) + “Meu Clã” (total e liga) + perfil (Ouro 1 vs Champion).
- Se “Meu Clã” continuar 0, investigar qual campo o cliente soma; não chutar.

### Passo 3 — R-C4: tropas
- Reconstruir contadores do avatar a partir dos camps no load/save; clampar em 0.
- `PlaceAttacker`/`RemoveUnits`: só camps para o exército próprio; castelo separado.
- `ProductionCompleted`: filtrar spell forge vs camp.
- Adicionar log de tipo/quantidade em `TrainUnit` e `PlaceAttacker`.
- Teste controlado: comprar exatamente 12 dragões (sem doar), atacar, contar `PlaceAttacker` de dragão no log e comparar com o DB e com o que o usuário viu. Só então dizer se “bate”.

### Passo 4 — R-C5: persistência
- Restaurar save no `DropClient` (e salvar aliança após promote/kick).
- Teste: comprar 1 tropa, fechar/abrir o jogo em <60s, ver se persiste; repetir com troca de conta.

### Passo 5 — R-C5b: campanha
- Gerar 24133 com JSON + avatar do jogador, sem os 8 bytes extras.
- Encodar NpcStars/loot no avatar e registrar o resultado da batalha NPC.
- Teste: atacar um nível além dos 2 tutoriais; ver se a vila abre, se as tropas são as suas e se a próxima desbloqueia.

### Passo 6 — R-C6: popup CV
- Com o resultado do Passo 0, bissectar o campo (B, liga, etc.) **um por vez**, pedindo o número do popup a cada troca.
- Não mexer no TH real (DB th=9) sem prova.

### Passo 7 — R-C7: N contas
- Só depois de tudo. Definir como o usuário escolhe a conta (o popup nativo só oferece uma). Provavelmente precisará de um comando/chat ou uma lista.

---

## 7. Protocolo de teste (copiar para o novo chat)

Para **cada** mudança:
1. Implementar **só** aquela mudança.
2. `bash scripts/build-server.sh`.
3. `pkill -xf "mono ucs.exe"` (watcher reergue) e confirmar o novo pid.
4. Conferir no log que a mensagem/feature foi carregada.
5. Abrir uma `question` com:
   - o que mudou (1 frase);
   - o passo a passo in-game;
   - o que observar (texto exato, número, ícone);
   - “me diga o que apareceu, mesmo que não tenha mudado”.
6. Esperar a resposta. **Não** fazer outra mudança enquanto espera.
7. Anotar o resultado (funcionou / não / piorou) no handoff/SPEC.
8. Se não funcionou: comparar binário em execução vs source, log e DB **antes** de propor a próxima.

---

## 8. O que NÃO confiar

- `HANDOFF.md`/`SPEC.md` antigos dizem “R-C1..R-C7 pronto para teste”. **Falso no source atual**: o commit foi revertido.
- “O servidor foi reiniciado” — confirmar `ps` e mtime do `ucs.exe`; o processo pode estar com o binário antigo.
- “A liga está corrigida” — o `24301` pode mostrar 16, mas o perfil (`24334`) usa o avatar com campo fixo 10. São lugares diferentes.
- “O clã está 10.000” — é o DB, não o encoder. Recalcular, não só mudar default.
- “As tropas estão certas” — o DB tem negativos; o ataque usa o avatar, não os camps.
- “A campanha está pronta” — os goldens usam o avatar da captura e as listas NPC vão zeradas.

---

## 9. Comandos úteis de diagnóstico

```bash
# Processo e binário
ps -eo pid,lstart,cmd | grep 'mono ucs.exe' | grep -v grep
stat -c '%y %s %n' '/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/ucs.exe'

# Git: provar o revert
cd /home/eg/Downloads/coctestezero/server
git rev-parse --short HEAD
git reflog -5
git show --stat e4e3648
git diff --stat e4e3648

# Log: promoção
grep -n '14306\|Promote' /home/eg/Downloads/coctestezero/server/ucs-zero.log | tail -20

# DB: tropas vs camps
python3 - <<'PY'
import sqlite3, json
con = sqlite3.connect('/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/ucsdb')
for pid, av, go in con.execute('select PlayerId, Avatar, GameObjects from Player'):
    a = json.loads(av); d = json.loads(go)
    camps = {}; castle = {}
    for o in d.get('buildings', []):
        if o.get('data') == 1000000:
            for u in o.get('units') or []: camps[u[0]] = camps.get(u[0], 0) + u[1]
        if o.get('data') == 1000014:
            for u in o.get('units') or []: castle[u[0]] = castle.get(u[0], 0) + u[1]
    avu = {u.get('global_id', u.get('data')): u.get('value') for u in a.get('units', [])}
    print(pid, a.get('avatar_name'), 'camps', camps, 'avatar', avu, 'castle', castle)
PY
```

---

## 10. Contato com o usuário

- Idioma: **português**.
- Ele está cansado de “tá tudo pronto” sem teste. Ser direto, admitir o que não funciona e mostrar a prova.
- Ele quer **uma coisa por vez**; não despejar 7 correções.
- O celular é o juiz final. Log/DB são a prova técnica.
- O vídeo antigo não será enviado; não pedir de novo.

---

**Fim do handoff.** O próximo chat deve começar lendo este arquivo, o `RC_INVESTIGACAO.md` e o `PROMPT_NOVO_CHAT.md`, e então fazer o Passo 0/1.
