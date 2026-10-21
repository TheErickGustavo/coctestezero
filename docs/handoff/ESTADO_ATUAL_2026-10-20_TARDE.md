# ESTADO ATUAL — 20/10/2026 (tarde)

Servidor UCS Zero (fork do Ultrapowa) para o Clash of Clans 2015 (client **7.65.5**,
`com.supercell.coctestezero`).
Branch: `fix/rc4-multiplayer-battle-context` · Build oficial: `bash scripts/build-server.sh`
· Servidor: `cd server && setsid nohup bash scripts/watch-server.sh &` · `adb reverse tcp:9341 tcp:9340`

---

## 1. CAUSA-RAIZ GERAL (descoberta desta sessão)

O usuário percebeu que **todos** os bugs (coletor, herói, gemas, tropas) tinham a
mesma origem. A causa é dupla:

1. **O servidor não persistia / não expunha o estado que o cliente 7.65 assume.**
   O client 7.65 é *client-authoritative* em vários pontos e recalcula o que
   precisa a partir de campos específicos do JSON. Se o servidor não manda o
   campo certo (ou manda um inventado), o client:
   - aplica a mudança **só na tela** (HUD local) e
   - ao recarregar a vila recebe o valor do servidor → **"a coisa volta"**.

2. **O servidor creditava/decidava com o valor errado** (o `Unknown1` do comando),
   enquanto o cliente exibia o valor certo.

### Campos do protocolo 7.65 que descobrimos (comparados com o jogo real)

| Campo | Onde | O que é (confirmado) |
|---|---|---|
| `res_time` | prédio (na vila) | **Segundos que faltam para o coletor ENCHER.** É com ele que o client calcula o acumulado. Sem esse campo o coletor nasce zerado a cada entrada na vila. Ex. real: gold mine lvl1 com `res_time: 8626` (cap 500, 200/h → 9000 s). |
| `id` | prédio | **Id do objeto** (500000171, 500000348…). O jogo real sempre manda. Sem ele o client não amarra herói↔altar, castelo↔tropas etc. |
| `hero_health` | avatar | **Tempo RESTANTE (em segundos) para o herói ficar disponível** — não é o HP! (1630 → 27 min; 4453 → 74 min = "1h13", exatamente o que o usuário via). `0` = herói disponível na hora. |
| `hero_state` | avatar | `0` = na vila (mas **não atacável**), `1` = em upgrade, `2` = deitado, **`3` = pronto/disponível para atacar** (o valor que o próprio UCS usa no `FinishUpgrading`). |
| `res_prod` | — | **INVENTADO pelo UCS.** Não existe em nenhum JSON do jogo 7.65. Mantido por compatibilidade, mas o `res_time` é o campo oficial. |

### Comandos que o client 7.65 manda e o servidor ignorava

| Cmd | Nome | Situação anterior |
|---|---|---|
| `0x212` (530) | SpeedUpHeroHealth | **Não tinha `Execute()`** → o revive do herói nunca era salvo (gemas voltavam) e o client reenviava o comando a cada tela de ataque → o servidor cobrava 21× (10.000+ gemas). |
| `0x1FA` (506) | CollectResources | Creditava o valor do `Unknown1` (= **tick do relógio do jogo**, não o acumulado) → 27 gemas onde o cliente tinha coletado ~11.000. |
| `0x211` (529) | ToggleHeroSleep | Sem `Execute()` (pendência). |

### Ticks (relógio do jogo, 1/60 s)
`0x258 PlaceAttacker.Unknown1` = tick do relógio do jogo no momento da colocação.
`0x25B EndOfBattle` (603) = **tick final** (o cliente NÃO manda dano, % nem loot:
o protocolo é client-authoritative e o servidor precisa simular).

---

## 2. RESOLVIDO E VALIDADO PELO USUÁRIO (20/10)

| Item | Como foi resolvido | Validação |
|---|---|---|
| **Coletor não enche fora da vila** | `ResourceStorageComponent.Save()` grava `res_time`; `Load()` restaura o timer (o tempo do level = `LastUpdateTime` do banco, então o tempo fora da vila já conta). Estado legado (sem `res_time`) volta a encher. | ✅ "os coletores realmente enchem conforme o jogador não fica com o jogo aberto" |
| **Coleta: o recurso sumia** | O servidor credita `GetCollectable()` (valor real) em vez do `Unknown1`. O valor do servidor bate com o que o cliente exibe (11.053 vs 11.048). | ✅ "1.815.498 → 1.815.500 ao voltar" |
| **Herói "dormindo" / não atacava** | `hero_health = 0` (é o *tempo restante*, não HP) + `hero_state = 3` (pronto). `ActivateHeroes()` no login, com guard do altar (sem altar o client crasha com SIGSEGV). | ✅ "apareceram de pé na vila **e na tela de ataque**, e deu pra atacar" |
| **Gemas caindo sozinhas** | Consequência do item acima: o client parou de pedir revive. `SpeedUpHeroHealthCommand` agora tem `Execute()` + dedup (não cobra se o herói já está disponível) + `SavePlayerNow`. | ✅ "as gemas param de cair sozinhas" |
| **Looping da fila de treino** | `UnitProductionComponent.RemoveUnit` reinicia o timer **sempre** que houver fila (antes só quando o slot zerava → a 2ª tropa ficava presa). | ✅ "Testei - funcionou!" |
| **24101 malformado (crash ao abrir a vila)** | `GameObjectManager.Save()` tinha `time_in_special_period` **duplicado** no `respawnVars` → o JObject do Newtonsoft lançava exceção e o 24101 ia quebrado (SIGSEGV no GLThread). | ✅ (vila inicial carrega até o fim) |
| **Conta nova / login** | `LoginMessage` cria a conta com a vila padrão se não existir; `ObjectManager.DefaultAccountId` (default 1) mapeia o `accountId=0` (app com dados limpos) para a conta principal; `/account <id>` troca. | ✅ jogo abre normal |
| **Comando de gemas** | `/give <id> <gemas> [ouro] [elixir] [elixirEscuro]` (admin, privilégio ≥1) + mensagem de feedback no chat. O `olamundo` tem `AccountPrivileges=1`. | ✅ funciona (no chat **global**) |
| **Reparar todas as contas** | Comando `/fixaccounts` (repara contador de tropas, coletores, score do clã, castelo e salva) — **PERDIDO no revert de 20/10**, precisa ser reaplicado. | ⚠️ |

---

## 3. ABERTO / PRIORIDADES

| # | Item | Estado |
|---|---|---|
| P-1 | **Castelo do clã não aparece na tela de ataque** (P-5 antigo). Diagnóstico: o `alliance_units` do avatar está **vazio** e o castelo (1000014) tem `"units": []`. Provavelmente é o `AllianceTroopService.SyncAllianceUnits` (que copia castelo→avatar) + o castelo ficar vazio quando as tropas doadas são usadas. | ❌ |
| P-2 | **PvP às vezes registra `tropas=0`** (a lista de tropas colocadas se perde entre o `PlaceAttacker` e o `GoHome`). Diagnóstico instalado: log em `ClearPvpBattleContext` (com o chamador) e em `ProcessPendingPvpBattle` (endTick + lista). Também: o client manda o id **4000520** (x=0) para colocar o **HERÓI** e o servidor mapeia para "Dragon" — o herói precisa ser reconhecido. | 🔍 log instalado |
| P-3 | **Custo de 75 de ouro por ataque** está errado para a liga do usuário (4.000 troféus). O valor vem do client; no CoC real depende da liga. | ❌ |
| P-4 | **% de destruição / loot / vitória** ainda estimados por simulação (`BattleSimulation`). Calibração anterior: 5 magos → 2,37% (real 2%), 9 magos → 4,25% (real 3%). O loot real do cliente (1000+1000) nunca foi comparado com o simulado. | ⚠️ |
| P-5 | **Vila inicial (tutorial) ainda crasha** (SIGSEGV no GLThread) — é o crash documentado no handoff desde 18/10 (o client pula para o passo da batalha). `TutorialStepsCount` é o suspeito. | ❌ |
| P-6 | `ToggleHeroSleepCommand` (0x211) sem `Execute()`. | ❌ |
| P-7 | `/fixaccounts` (repara todas as contas de uma vez) — perdido no revert. | ❌ |
| P-8 | Tarefas do castelo do clã (torre inferno, x-besta) "recarregam" só visualmente — provavelmente a mesma classe do bug do coletor (`res_time`/timer). | ❌ |
| P-9 | P-2/P-3/P-4 antigos (barra + botão Donate no chat, campanha goblin, mensagem de promoção). | ❌ |

---

## 4. ARMADILHAS CONHECIDAS (não repetir)

- **Nunca** usar `xbuild`/`msbuild` — o build é `bash scripts/build-server.sh` (mcs + `scripts/sources.rsp`).
- **Todo `.cs` novo** precisa entrar em `scripts/sources.rsp` (casing exato).
- **JObject com chave duplicada** = exceção silenciosa no `Save()` → 24101 malformado → crash nativo no client. Use o indexador `json["k"] = v` para campos que podem repetir.
- O client 7.65 **não** conhece `res_prod` (não está em nenhum JSON do jogo) — o campo oficial é `res_time`.
- `hero_health` = **tempo restante em segundos**, não HP.
- `hero_state` = **3** para o herói atacável.
- O `id` dos prédios (`500000000+`) precisa ir no JSON e ser restaurado no `Load()`.
- `adb reverse tcp:9341 tcp:9340` — o reverse se perde quando o USB cai.
- O watcher do servidor precisa de `setsid` (senão morre junto com o shell).
- O `uczero.log` chegou a 22 MB com os logs de diagnóstico — rotacionar (`mv ucs-zero.log ucs-zero.log.old`).

---

## 5. ATUALIZAÇÃO 20/10 (final do dia)

### Causa-raiz mais grave encontrada: `ResourcesManager.LoadLevel` usava `TryAdd`
Se já existisse uma entrada do id no dicionário `m_vInMemoryLevels` (de um login
anterior, sem logout), a entrada antiga continuava lá. Consequências:
- o **save periódico gravava o estado de um level OBSOLETO** no banco (por cima do
  estado real do jogador, que vivia no level do login) → o estado do jogador
  (coletor, fila de treino, castelo, gemas) se perdia;
- os **comandos admin** (`/give`, `/castelo`) pegavam essa cópia e mexiam nela →
  as gemas e as tropas doadas só "apareciam" quando o jogador relogava.
**Correção:** `LoadLevel` agora **substitui** a entrada (`m_vInMemoryLevels[id] = level`)
e `LogPlayerIn` sempre registra o level atual. ✅ validado (castelo e gemas
persistem agora).

### Other correções validadas
- `res_prod` (campo inventado pelo UCS) removido dos prédios que não são coletores.
- `PlaceAttackerCommand` não mexe mais no castelo do clã (usava o primeiro storage
  com aquele tipo → esvaziava o castelo).
- `SyncAvatarUnitCounts` exclui o castelo **1000014** (antes excluía 1000143, que não
  existe) → a tropa doada não aparece mais no acampamento.
- Chat do **clã** passou a delegar ao `GameOpCommandFactory` (antes só `/doar` e
  `/ajuda` funcionavam lá; `/give` e `/account` só no chat global).
- Novo comando admin **`/castelo <avatarId> <tropa> [qtd]`** (o client 7.65 bloqueia
  doar para si mesmo).
- 24 contas de teste ("Chief") apagadas (entravam na sorteio do PvP).
- O servidor crashava em loop com "Address already in use" quando dois watchers
  subiam juntos — **sempre subir UM watcher só** (`setsid nohup bash scripts/watch-server.sh &`).

### Heróis no CoC 7.65
`AllianceTroopService.HeroUnitIds = {4000049 (Dragon), 4000073 (Golem), 4000078}` —
no CoC 2015 os heróis são **Dragão e Golem** (o "Rei"/"Rainha" só existem no CoC
moderno). O client coloca o herói com **x = 0** e o id global **4000520**.

### Ainda aberto
- **P-1** o castelo do ATACANTE não aparece na tela de ataque (o do DEFENSOR já
  aparece ✅, com as tropas doadas).
- **P-2** PvP às vezes com `tropas=0` (log de diagnóstico instalado).
- **P-3** custo de 75 de ouro por ataque (por liga).
- **P-4** % de destruição / loot / vitória (simulação, precisa de calibração).
- **P-5** vila inicial (tutorial) crasha.

###(ids de tropa: o "count" parcial do UCS)
`Data.GetGlobalID()` usa `CreateGlobalID(tableIndex+1, dt.GetItemCount())` e o
`GetItemCount()` do DataTable é um **contador parcial**: os ids saíam **deslocados**
(Dragon = 4000006, Archer = 4000008...). O cliente 7.65 **ignora ids que não
conhece**, então:
- o castelo do clã gravava 4000008 → o `alliance_units` não era reconhecido;
- o alias "dragão" do `TroopNameParser` apontava para 4000006 → `GetDataById`
  devolvia a **linha 6** (bárbaro nível 7) → a doação entregava a tropa errada;
- o contador de tropas do avatar ficava com ids inválidos.
**Correção:** `DataSlotUtil.GetClientGlobalId(data)` devolve o id **real**
(`4000000 + índice da linha do CSV` = o id do jogo: Dragon 4000049, Archer 4000007,
Goblin 4000014). Aplicado no `DataSlot.Save/Encode`, no `UnitStorageComponent.Save`,
no `SyncAvatarUnitCounts`, no `SyncAllianceUnits` e no `TroopNameParser`.

### Coisas que NÃO são bug (expectativa do CoC real)
- No CoC 2015 o **castelo do clã do DEFENSOR** aparece na tela de ataque (✅ já
  funciona, com as tropas doadas). O **castelo do ATACANTE** (o escudo do clã na
  barra) é o que ainda não aparece — o servidor envia `alliance_units`,
  `alliance_castle_level/total_capacity/used_capacity` corretos.
- No CoC 2015 os heróis são **Dragão (4000049) e Golem (4000073)**, não Rei/Rainha
  (esses são do CoC moderno). O client coloca o herói com **x = 0** e id **4000520**.

---

## 6. P-1 — CASTELO DO CLÃ (21/10) — RESOLVIDO PARCIALMENTE

### Resolvido e validado pelo usuário ✅
1. **Castelo do DEFENSOR com as tropas doadas aparece na tela de ataque** ✓
2. **As tropas doadas NÃO aparecem no acampamento** (elas ficam "dentro" do castelo,
   como no CoC real) — o `SyncAvatarUnitCounts` exclui o castelo **1000014**
   (antes excluía 1000143, que não existe nesta versão).
3. **As tropas TREINADAS não vão mais para o castelo** — o `ProductionCompleted`
   entregava no storage de tropas mais **próximo** e o castelo (mais perto do quartel
   que a casa) recebia as tropas do jogador (elas sumiam do acampamento e o castelo
   enchia). Agora o castelo é ignorado nessa busca.
4. **Apagar tropa do castelo** — o `RemoveUnitsCommand` (0x226) tem o campo
   `Unknown1`: **0 = do acampamento, 1 = do castelo do clã** (confirmado no
   `commands.log`: `00000000...` vs `00000001...`). O servidor agora respeita isso e
   não mexe no contador do avatar quando apaga do castelo.
5. **IDs de tropa reais** — o client 7.65 referencia as tropas pelo **índice do TIPO**
   (gigante = 4000003, dragão = 4000008) e o servidor indexava o `characters.csv` por
   **LINHA** (7 linhas por tipo). Criado o mapa linha↔tipo (`DataTable`), o
   `GetDataById` e o `DataSlotUtil.GetClientGlobalId` usam o índice do tipo. Antes o
   "dragão" virava "archer nível 2" e as comparações por instância falhavam (as
   tropas atacadas **não eram consumidas**).
6. Comando admin **`/castelo <id> <tropa> [qtd]`** (o client bloqueia doar para si
   mesmo) e o chat do clã passou a delegar ao `GameOpCommandFactory` (`/give`,
   `/account`).

### Pendente: o ESCUDO/BRASÃO do CLÃ do ATACANTE na barra de ataque
O servidor envia **tudo** que o protocolo tem (log do 24107):
```
claUnits=Archer:7(id=4000001) Dragon:1(id=4000008) | casteloLvl=6 cap=35/27
```
- `alliance_units` com os ids REAIS ✓, `alliance_castle_level/total_capacity/
  used_capacity` ✓, o badge e o role do clã no avatar ✓ (formato do bloco do clã
  validado byte-a-byte contra o capture `generated-24101-live-0558.bin`).
- O sprite **`alliance_marker` existe no client 7.65** (`base-libg-lan.so` do APK) —
  então a feature é desta versão.
- **Hipóteses para investigar:** (a) o `storage_type` do castelo (no nosso código
  `storage_type=1` significa "feitiaria" e o `Load` deriva o `IsSpellForge` dele —
  no jogo real o feitiário tem `storage_type=1` e o castelo do clã talvez use outro
  valor/flag); (b) algum campo do avatar que o client 7.65 exige para o slot do
  clã; (c) o client precisa do PRÉDIO do castelo na sua vila com as tropas (o client
  tem a vila em memória, mas o storage_type/ids podem estar errados).
