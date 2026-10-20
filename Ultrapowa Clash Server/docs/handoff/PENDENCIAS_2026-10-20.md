# PENDÊNCIAS — CoC 2015 / UCS Zero — 20/10/2026

> Documento de trabalho para **revisão profunda de código** (a ser feita por outra IA lendo
> apenas este repositório). Tudo aqui é verificável no repositório e nos logs.
>
> **Repositório:** https://github.com/TheErickGustavo/coctestezero (privado)
> **Branch de trabalho:** `fix/rc4-multiplayer-battle-context`
> **Servidor:**ucs 0.6.1 (fork do Ultrapowa Clash Server), C# / Mono 6.8, **rodando em Linux (Ubuntu 24.04 / Linux Mint)**

---

## 0. COMO O PROJETO É CONSTRUÍDO E TESTADO (leia antes de tudo)

### 0.1 Estrutura

```
/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/   <- fonte C# (projeto MSBuild: .csproj + .sln)
/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/  <- binário + gamefiles + DB
/home/eg/Downloads/coctestezero/scripts/     <- build-server.sh, sources.rsp, watch-server.sh, build-apk.py
/home/eg/Downloads/coctestezero/server/ucs-zero.log             <- log do servidor
/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/ucsdb   <- SQLite (players + clans)
/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/r-c4-603.log  <- log de calibração do comando 603
```

### 0.2 Build (OBRIGATÓRIO usar o script, NUNCA xbuild/msbuild)

```bash
bash /home/eg/Downloads/coctestezero/scripts/build-server.sh
```

Por que o script e não o `.csproj`:

- `scripts/build-server.sh` compila com `mcs` do **mono-root customizado**
  (`/home/eg/Downloads/coc-2015-blue-walls-lab/tools/mono-root`) e usa a lista de fontes
  `scripts/sources.rsp`.
- O `EntityDeploy` (tarefa do MSBuild que embute o metadata do Entity Framework) **não
  funciona no Linux**. O script embute os recursos na mão:
  `-resource:Database/ucsdb.sqlite.csdl,Database.ucsdb.sqlite.csdl` (idem ssdl/msl).
- `MySql.Data.Entity.EF6.dll` **não existe** no ambiente → o `App.config` foi ajustado para
  usar só o provider SQLite (o servidor usa `databaseConnectionName=sqliteEntities`).
- **Qualquer `.cs` novo precisa ser adicionado ao `scripts/sources.rsp`** (casing EXATO do
  arquivo) — se não, ele simplesmente não é compilado. Foi exatamente o que aconteceu com
  `PacketProcessing/Commands/EndOfBattleCommand.cs`.

### 0.3 Reiniciar o servidor

```bash
pkill -xf "mono ucs.exe"     # o watcher (scripts/watch-server.sh) reergue sozinho
tail -n 20 /home/eg/Downloads/coctestezero/server/ucs-zero.log
```

### 0.4 Conectar o celular (SEM ISSO O APP NÃO CONECTA)

O app Android (`com.supercell.coctestezero`, build em `apk-work/coctestezero-aligned.apk`)
aponta para **`127.0.0.1:9341`** (ver `scripts/build-apk.py`, que troca a porta `9340`→`9341`
no `libg.so`). Então é preciso:

```bash
export PATH="/home/eg/Downloads/coc-2015-lab/tools/platform-tools:$PATH"
adb devices                 # o celular precisa estar conectado via USB
adb reverse tcp:9341 tcp:9340
adb reverse --list          # tem que mostrar "UsbFfs tcp:9341 tcp:9340"
```

O reverse **se perde quando o USB cai** → o app dá "erro de conexão".

### 0.5 other settings que quebram o login

- `ucsconf.config` → `clientVersion` tem que ser **7.65** (o app 7.65.5 manda `major=7 build=65`).
  Com 7.156 o servidor responde `LoginFailedMessage` (errorCode 8) e o app não entra.
- `ucsconf.config` não pode ter BOM (UTF-8 BOM) — o parser de config do Mono falha.

### 0.6 Teste prático

O usuário **sempre tem o jogo aberto no celular** e valida cada mudança na prática.
Dumpbox/logcat do crash nativo do cliente:

```bash
adb logcat -d | grep -iE "coctestezero|libg|FATAL|StackOverflow"
adb shell dumpsys dropbox --print data_app_crash
```

---

## 1. ESTADO ATUAL (validado pelo usuário — NÃO QUEBRAR)

| # | Item | Como foi validado |
|---|---|---|
| R-C1 | Promoção de membro (Ancião/Co-líder) | cargo aparece no perfil e no "Meu Clã"; DB `role: 3` |
| R-C2a | Ordem 0-based no 24301 (membros do clã) | líder em 1º; símbolo de liga "Campeão" certo |
| R-C3 | Score/nível do clã no 24301 | "Meu Clã" e busca mostram 8000 / nível 1 |
| R-T7 | Login, vínculo de conta, mural | não regredir |
| **R-C4** | **Troféus PvP** | sem 603 = sem combate (não altera nada); resultado aplicado no `GoHomeMessage`; score do clã recalcula na hora (kick/leave/join) e aparece sem relogar |
| **R-C5** | **Tropas** | acampamento e tela de ataque mostram os mesmos números; golem aparece no multiplayer; produção de tropas funciona de verdade |
| **R-C6** | **Persistência de recursos** | coletar elixir/ouro credita no servidor e **persiste no DB** (valor permaneceu após fechar/reabrir o jogo) |
| **R-T7** | **Doações** | "você não tem X no acampamento" sumiu; herói não é doável |

### 1.1 Causas raiz já encontradas (NÃO REDISCOBRIR)

| Sintoma | Causa raiz | Arquivo:linha |
|---|---|---|
| Crash nativo do cliente (StackOverflow em `GameApp.update`) | `UnitProductionComponent.Save()` só gravava `"t"`/`"slots"` do `unit_prod` quando havia fila/timer. O cliente 7.65 **sempre** espera os dois campos → parser em loop → app morre logo após o 24101 | `Logic/Component/UnitProductionComponent.cs` (Save) |
| Produção de tropas nunca acontecia | `ComponentManager.Tick()` e `GameObject.Tick()` estavam **VAZIOS** → `UnitProductionComponent.Tick()` nunca rodava → `ProductionCompleted()` só via `SpeedUp` (gemas) | `Logic/Manager/ComponentManager.cs` |
| Coletor de recurso não tinha storage | `Building` só criava `ResourceStorageComponent` se `MaxStored* > 0`, e essas colunas vêm **VAZIAS** no CSV do 7.65 | `Logic/Building.cs` |
| Capacidade do coletor errada | a capacidade real é a coluna **`ResourceMax`** por nível (500, 1000, …), não `MaxStored*` | `Logic/Component/ResourceStorageComponent.cs` |
| "você não tem X no acampamento" | `AllianceTroopService.ArmyCampBuildingId = 1000000` é a **CASA**; o quartel é `1000006` (barracks) / `1000026` (dark elixir barracks) | `Logic/AllianceTroopService.cs` |
| Contador de tropas avatar ≠ storage | avatar conta por tipo, storage por tipo+nível; `RemoveUnitsImpl` descontaba do avatar o valor pedido (não o removido) e só achava o slot com o nível exato | `Logic/Component/UnitStorageComponent.cs` |
| "Procurar vila" ficava na nuvem infinita | `GetRandomPlayerExcluding` só olhava jogadores **em memória**; sem adversário online não mandava o 24107 | `Core/ObjectManager.cs` |
| Score do clã não atualizava | `RecalculateScore()` só era chamado no fim de batalha, nunca em kick/leave/join; e o 24301 não era reenviado | `Core/ObjectManager.cs`, `Alliance.cs` |

---

## 2. PENDÊNCIAS (a fila de trabalho)

### P-1 ⭐ (PRIORIDADE ALTA) — Valor da coleta de recurso diverge do cliente

**Sintoma**

- O cliente 7.65 mostra e credita localmente uma quantidade pequena (ex.: `+9`, depois `+81`).
- O servidor credita muito mais (ex.: `18177`) com o mesmo comando.
- Consequência: o total de elixir do jogador fica divergente do que ele vê (cliente × servidor).

**Log do servidor (evidência)**

```
[UCS] Coleta: predio 500000011 (data 1000002 TID_BUILDING_ELIXIR_PUMP lvl10) disponivel=18177 cap=150000 unknown1=689
[UCS] Coleta: 18177x Elixir (total 5725977)
[UCS] Coleta: predio 500000006 (data 1000002 TID_BUILDING_ELIXIR_PUMP lvl10) disponivel=0 cap=0 unknown1=1138
[UCS] Coleta: predio 500000007 (data 1000002 TID_BUILDING_ELIXIR_PUMP lvl10) disponivel=0 cap=150000 unknown1=637
```

**O que já se sabe**

- `CollectResourcesCommand` (0x1FA) tem 2 campos: `BuildingId` (uint) e `Unknown1` (uint).
  `BuildingId` = **GlobalId do GameObject** (`5000000xx`, gerado por
  `GameObjectManager.GenerateGameObjectGlobalId` = `index + 1000000*(classId+500)`), **não** o
  `data` do prédio.
- `Unknown1` observed: `1138`, `637`, `689` — **varia entre coletas** e **não é a quantidade
  coletada** (9 / 81 / 34). Hipótese atual: é o **tempo restante** até o coletor encher, ou
  alguma unidade interna do recurso.
- Modelo atual do servidor (`ResourceStorageComponent.GetCollectable`): acúmulo contínuo
  `perHour * elapsed / 3600`, com `elapsed = totalSegundos - restante` e
  `totalSegundos = capacidade * 3600 / perHour` (Elixir Pump lvl10: cap 100000, perHour 2500 → 40h).
- O cliente 7.65 tem o **próprio** modelo de produção (ele simula localmente) e mostra
  valores muito menores.

**Perguntas para a IA**

1. Qual é o formato real do payload do comando **0x1FA (CollectResourcesCommand)** no CoC
   7.65? (o `Unknown1` é tempo restante? timestamp? id de "respawn"?)
2. Como o CoC 7.65 calcula o "tempo até encher" e o "disponível" de um coletor? Existe
   conceito de *respawn* de recursos (`respawnVars` no JSON da vila tem
   `secondsFromLastRespawn` / `respawnSeed` / `time_to_gembox_drop` / `time_to_special_drop`)?
3. Qual o valor correto a creditar: o que o cliente mandou, ou o cálculo do servidor?
   (precisa ser **o mesmo número** que o cliente mostra, senão o inventário diverge)
4. O `unknown1` muda quando o tempo passa (1138 → 637 → 689)? Dá para modelar como
   "segundos restantes" e usar isso como **autoridade do cliente**?

**Onde mexer**: `PacketProcessing/Commands/CollectResourcesCommand.cs`,
`Logic/Component/ResourceStorageComponent.cs`, `Logic/Timer.cs`.

---

### P-2 — Pedido de tropas no chat sem barra de capacidade e sem botão "Donate"

**Referência visual (CoC 2015 real)**: a mensagem do jogador no chat do clã, e **abaixo**:
a barra de espaço do castelo (`0/10`) e um **botão verde "Donate"** (mesmo estilo dos outros
botões verdes do jogo — "Resgatar", "Editar", "Enviar mensagem").

Print de referência do usuário:
`/home/eg/Área de trabalho/Donate_ClashOfClans/doação de tropas chat.jpeg`
(foto do CoC real: `/home/eg/Área de trabalho/Donate_ClashOfClans/doaçao de tropas.jpeg`)

**Situação atual**: o servidor anuncia o pedido como **texto puro** no chat:

```
olamundo pediu tropas (17/35): 1x Bárbaro
```

criado em `RequestAllianceUnitsCommand.Execute` (que chama `ChatBroadcast.ToAlliance`).

**Hipótese**: no CoC 2015 o pedido é uma **stream entry de tipo específico** (o cliente
desenha a barra + o botão quando a entrada é do tipo "troop request"), e não um
`ChatStreamEntry` (tipo 2) com texto.

**Perguntas**

1. Qual é o **tipo de stream entry** que o CoC 7.65 usa para o pedido de tropas no chat do
   clã? (`Logic/StreamEntry/*.cs` tem: `ChatStreamEntry` = tipo 2, `AllianceEventStreamEntry` = tipo 4,
   `AllianceKickOutStreamEntry`, `AllianceMailStreamEntry`, `AvatarStreamEntry`).
2. Que campos o cliente precisa nesse pacote para desenhar a barra (x/y, used/total) e o botão
   "Donate"?
3. Como o cliente sabe que pode doar (habilita o botão) e qual o comando que ele manda ao
   clicar (hoje usamos `0x04 DonateAllianceUnitCommand`)?

**Onde mexer**: `PacketProcessing/Commands/RequestAllianceUnitsCommand.cs`,
`Logic/StreamEntry/`, `PacketProcessing/Messages/Server/AllianceStreamEntryMessage.cs` (24312).

---

### P-3 — Campanha (goblin) não avança

**Sintoma**: o usuário venceu a base goblin (campanha) e **a história não avançou**; o nível
da campanha não subiu. Já foi registrado como pendência desde o handoff anterior
(`NOTAS_2026-10-19_RC1.md`).

**Contexto**: o servidor tem `gamefiles/pve/level1..50.json` (`ObjectManager.LoadNpcLevels`),
`Logic/NpcLevel.cs`, e o cliente 7.65 tem o tutorial ligado ao goblin (passos
`TutorialStepsCount` 3 e 10 em `GoHomeMessage`).

**Perguntas**

1. Onde o CoC 7.65 guarda o **progresso da campanha** (level do NPC, número de estrelas por
   nível)? No avatar (`ClientAvatar`) ou no level (vila)?
2. Qual comando/mensagem o cliente manda ao vencer a campanha?
3. O `NpcDataMessage` (que o servidor envia) precisa devolver o progresso atualizado?

**Onde mexer**: `PacketProcessing/Messages/Server/NpcDataMessage.cs`, `Logic/NpcLevel.cs`,
`Logic/ClientAvatar.cs`, `PacketProcessing/Messages/Client/AttackNpcMessage.cs`.

---

### P-4 — Chat do clã: mensagem de promoção / expulsão

**Sintoma**: a **promoção** funciona (cargo aparece no perfil e no "Meu Clã") mas **não aparece
no chat**. No CoC real aparece uma mensagem de sistema (verde) "X foi promovido a Ancião".
A **expulsão** também não gera evento no chat.

**Situação atual**: existe `Logic/StreamEntry/AllianceEventStreamEntry.cs` (tipo 4, com
`m_vEventType`) usado no **kick** (`KickAllianceMemberCommand` → `SetEventType(1)`), mas o
usuário não vê o evento. E a promoção (`PromoteAllianceMemberMessage`) não cria nenhuma
stream entry.

**Perguntas**

1. Quais `eventType` o CoC 7.65 usa no `AllianceEventStreamEntry` (join/leave/promote/kick)?
2. O chat do clã (`AllianceStreamMessage` 24311) e as entradas de evento (`AllianceStreamEntryMessage`
   24312) usam o **mesmo** formato? Hoje o kick envia a entry para o alvo, e o
   `AllianceEventStreamEntry` para todos — o cliente mostra a "faixa" de evento ou só no chat?
3. A mensagem do chat do evento precisa de texto pronto ou o cliente monta a partir do eventType?

**Onde mexer**: `Logic/StreamEntry/AllianceEventStreamEntry.cs`,
`PacketProcessing/Commands/KickAllianceMemberCommand.cs`,
`PacketProcessing/Messages/Client/PromoteAllianceMemberMessage.cs`.

---

### P-5 — Tropas do castelo do clã não aparecem no multiplayer

**Sintoma**: quando o jogador tem tropas doadas no **castelo do clã** (data `1000014`,
`TID_BUILDING_ALLIANCE_CASTLE`), elas **não aparecem** na tela de ataque multiplayer.

**Situação atual**: o avatar tem o campo `AllianceUnits` (enviado em `ClientAvatar.Encode()`,
`data.AddRange(BitConverter.GetBytes(AllianceUnits.Count)...)`) e o
`AllianceTroopService.SyncAllianceUnits(level)` copia as tropas do castelo para ele — hoje é
chamado no login (`DatabaseManager.GetAccount`), no `GoHomeMessage` e no `SearchOpponentCommand`.

**Perguntas**

1. No CoC 7.65, as tropas do castelo do clã são **usáveis no ataque multiplayer** ou só na
   Guerra de Clã? (o usuário afirma que no CoC real elas aparecem no multiplayer)
2. Se forem, como o cliente as recebe: pelo `AllianceUnits` do avatar ou pelo `ClientHome`
   (JSON da vila do atacante)?
3. O `AllianceTroopService` está contando/limitando errado? (ver
   `AllianceTroopService.GetCastleStorage`, `AddDonatedUnit`,
   `UnitStorageComponent.AddDonatedUnit`)

**Onde mexer**: `Logic/AllianceTroopService.cs`, `Logic/ClientAvatar.cs`,
`Logic/Component/UnitStorageComponent.cs`.

---

### P-6 — Histórico de batalha / replay (R-C9)

**Sintoma**: no CoC real existe a aba com **registro de defesa / registro de ataque / caixa de
entrada** (ícone de mensagem). Aqui não aparece nada — nem os ataques que o jogador fez.

**Contexto**: já existe no upstream a `Logic/StreamEntry/Avatar/BattleReportStreamEntry.cs`
(usada pelo RetroClash: `PvbBattle.EndBattle()` salva o replay e cria a entry com
`BattleLogStats` — `DestructionPercentage`, `AttackerScore`, `DefenderScore`,
`TownHallDestroyed`, `BattleTime`…). No fork atual **não existe** nenhum registro.

**Perguntas**

1. Qual o formato do `BattleReportStreamEntry` no CoC 7.65 e qual mensagem o servidor deve
   enviar (id do pacote) para o cliente mostrar a aba?
2. O replay é gerado no cliente (o servidor só manda o resultado) ou o servidor precisa
   reconstruir a batalha?

**Onde mexer**: `Logic/StreamEntry/Avatar/` (novo), `Logic/Manager/GameObjectManager.cs`.

---

### P-7 — Escala do comando 603 (percentual de destruição real)

**Sintoma**: o payload de 4 bytes do comando **603 (`EndOfBattleCommand`, 0x025B)** **não é o
percentual de destruição**. Dados reais coletados (cliente 7.65.5):

| horário | value (hex) | value (dec) | % que o cliente mostrou | scores atacante/defensor | delta que o cliente mostrou |
|---|---|---|---|---|---|
| 07:45:26 | `00000236` | 566 | – | – | – |
| 07:54:39 | `00000115` | 277 | 2% | 4000/4000 | – |
| 13:39:05 | – | 3824 | 2% | 4000/4000 | -20 |
| 13:42:57 | – | 7968 | 16% | 4000/4000 | -17 |
| 14:21:06 | – | 1004 | 2% | 4000/4000 | -20 |
| 14:33:35 | – | 693 | 2% | 3980/4020 | -17 |
| 14:50:30 | – | 5188 | 2% | 3960/4040 | -15 |
| 15:07:49 | – | 712 | 2% | 3960/4040 | -12 |

**Conclusão atual**: o value é "damage points" (HP danificado) — **não** dá para derivar a %
de destruição a partir dele (valores 693 e 5188 ambos deram 2%).

**O que o servidor faz hoje** (`GoHomeMessage.ProcessPendingPvpBattle`):

```
hpTotalDefensor = ObjectManager.GetAttachableHpTotal(defensor)   // Σ HP dos prédios, sem muros
destruicao%     = 100.0 * value / hpTotalDefensor                 // ≈5,66% no caso 5188
vitória         = destrucao% >= 50                               // 1 estrela = 50%
bonus           = round(0.2 * destrucao%)
delta           = vitória ? round((20 + bonus) * ratio^13)
                           : -round((20 - bonus) / ratio^13)
ratio           = troféus do defensor / troféus do atacante
```

O `ratio^13` foi **calibrado empiricamente** (3 dos 5 casos bateram exatos; os outros erram ±1-2).

**Perguntas**

1. Existe alguma forma de o servidor saber a **% de destruição real** no CoC 7.65? (o cliente
   calculou 2% e 16% — o servidor precisa concordar)
2. A fórmula real de troféus do CoC 2015 (antes do patch de março/2016) é conhecida? Ela
   depende só dos troféus + % de destruição, ou de mais dados?
3. O `value` do 603 pode ser comparado com o **"battle score total"** do defensor (que o
   servidor poderia computar com a tabela `StrengthWeight` do CSV do jogo
   `v7.65.5/logic/buildings.csv`, coluna 105)? As amostras de `StrengthWeight` do CSV são
   100, 99, 98… por nível.

**Onde mexer**: `PacketProcessing/Messages/Client/GoHomeMessage.cs`,
`Core/ObjectManager.cs` (`GetAttachableHpTotal`), `Logic/Level.cs` (contexto PvP).

---

### P-8 — Client entrega a tropa ~1s antes do servidor

**Sintoma**: o cliente anima a entrega da tropa treinada e a mostra no acampamento ~1 segundo
antes de o servidor entregá-la (tick de 1s em `ObjectManager.TickOnlineLevels`). Resultado: o
acampamento (cliente) mostra 44 e a tela de ataque (servidor) 43 durante esse intervalo.

**Onde mexer**: `Core/ObjectManager.cs` (timer de 1s) — talvez 200-500 ms.

---

### P-9 — Perfil mostra a liga vazia

**Sintoma**: no perfil do jogador a **liga aparece vazia**; no clã/chat aparece "Campeão"
(correto). O `ClientAvatar.Encode()` manda `data.AddInt32(10)` fixo como league
(comentário: "10 em TODAS as capturas reais"). `UpdateLeague()`/`GetLeagueId()` existem no
avatar, mas o valor não é usado no encode.

**Onde mexer**: `Logic/ClientAvatar.cs` (Encode), `Logic/Avatar.cs` (GetLeagueId).

---

### P-10 — Delta de troféus erra ±1-2 em relação ao cliente

**Sintoma**: com scores iguais (4000/4000) o cliente mostra **-20** com 2% de destruição e
**-17** com 16%; com scores 3960/4040 mostra **-12** e o servidor aplica **-14**.

**Hipótese atual (parcial)**: `loss(d) = -(20 - 0.2*d) / ratio^13`.
Falta o termo do **ratio** (o cliente muda o valor conforme a diferença de troféus dos dois
jogadores) e talvez um termo de **estrelas** (1 estrela = 50% de destruição / TH destruído).

**Dados para calibrar** (tabela em P-7).

---

### P-11 — `unit_prod` sempre precisa de `"t"` e `"slots"`

**Não é pendência, é ARMADILHA**: o cliente 7.65 **crasha** (StackOverflow nativo) se o
`unit_prod` do JSON da vila não tiver os dois campos. Qualquer coisa que grave o JSON da vila
tem que manter esse formato — ver `UnitProductionComponent.Save()` e o golden
`evidence/generated-24101-live-0558.bin`.

---

## 3. INFORMAÇÕES ÚTEIS

### 3.1 Tabela de ids do jogo (confirmada por dump no servidor)

O `data id` do prédio = `1000000 + índice da PRIMEIRA LINHA do grupo do prédio no CSV`
(agrupado por nível). Confirmado pelo log `[UCS] R-C5 buildings:`:

```
1000000 => TID_BUILDING_HOUSING        1000009 => TID_BUILDING_ARCHER_TOWER
1000001 => TID_BUILDING_TOWN_HALL       1000011 => TID_BUILDING_WIZARD_TOWER
1000002 => TID_BUILDING_ELIXIR_PUMP    1000012 => TID_BUILDING_AIR_DEFENSE
1000003 => TID_BUILDING_ELIXIR_STORAGE  1000013 => TID_BUILDING_MORTAR
1000004 => TID_BUILDING_GOLD_MINE       1000014 => TID_BUILDING_ALLIANCE_CASTLE
1000005 => TID_BUILDING_GOLD_STORAGE    1000019 => TID_BUILDING_TESLA_TOWER
1000006 => TID_BUILDING_BARRACK         1000020 => TID_BUILDING_SPELL_FORGE
1000007 => TID_BUILDING_LABORATORY      1000021 => TID_BUILDING_BOW
1000008 => TID_BUILDING_CANNON          1000022 => TID_BARBARIAN_KING
1000026 => TID_DARK_ELIXIR_BARRACK      1000025 => TID_ARCHER_QUEEN
1000010 => (decos, 250x na vila)
```

### 3.2 CSV do jogo (7.65.5) está em

`/home/eg/Downloads/coc-2015-blue-walls-lab/source/coc-assets-historical/v7.65.5/logic/*.csv`

- `buildings.csv`: 199 linhas (1 linha por nível, agrupadas por prédio).
  Colunas úteis: `Hitpoints` (37), `ResourcePerHour` (129), `ResourceMax` (130),
  `MaxStored*` (120-125, **vazias**), `StrengthWeight` (105, 100→… por nível),
  `ProducesResource` (128), `UnitProduction` (132), `HousingSpace` (127).
- `characters.csv`: ids `4000000 + índice da linha`:
  `4000000` Barbarian, `4000007` Archer, `4000014` Goblin, `4000020` Giant,
  `4000027` Wall Breaker, `4000033` Balloon, `4000039` Wizard, `4000045` Healer,
  `4000049` **Dragon (herói)**, `4000053` PEKKA, `4000058` Gargoyle, `4000073` **Golem (herói)**,
  `4000078` Golem Secondary, `4000083` Warlock.

### 3.3 O APK do cliente (para RE)

- `/home/eg/Downloads/coctestezero/apk-work/base-libg-lan.so` — `libg.so` **stripped** (sem
  símbolos de função; só os typeinfo do C++ em `.rodata`, ex.: `21LogicEndCombatCommand`).
- `/home/eg/Downloads/coctestezero/apk-work/aptools/smali-out/` — smali do `classes.dex`.
- O `assets/logic/*.csv` do APK está **criptografado** (cifra customizada do Titan, não é
  zlib/xor simples) — o CSV descriptografado está no workspace (item 3.2).
- `scripts/build-apk.py` mostra o que é patcheado: porta `9340`→`9341`, host `127.0.0.1`,
  package `com.supercell.clashofclant`→`com.supercell.coctestezero`.

### 3.4 Comandos úteis

```bash
# log do servidor em tempo real
tail -f /home/eg/Downloads/coctestezero/server/ucs-zero.log

# DB (SQLite)
python3 - << 'EOF'
import sqlite3, json
con = sqlite3.connect('/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/ucsdb')
for pid, av, go in con.execute('SELECT PlayerId, Avatar, GameObjects FROM Player'):
    a = json.loads(av); g = json.loads(go)
    res = {r['global_id']: r['value'] for r in a.get('resources', [])}
    print(pid, a.get('avatar_name'), 'score=', a.get('score'), 'elixir=', res.get(3000002),
          'alliance=', a.get('alliance_id'))
EOF

# calibração do 603
cat "/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/r-c4-603.log"

# comandos desconhecidos do cliente
cat "/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/unknown-commands.log"

# pacote 24101 gerado (comparar com o golden)
xxd "/home/eg/Downloads/coctestezero/server/Ultrapowa Clash Server/bin/Release/generated-24101.bin" | head -40
```

### 3.5 Evidências neste repositório

- `docs/handoff/ESTADO_ATUAL_2026-10-19_1600.md` — estado validado + armadilhas
- `docs/handoff/HANDOFF.md` — diagnóstico antigo (tropas/campanha/popup)
- `docs/handoff/NOTAS_2026-10-19_RC1.md` — histórico dos testes
- `docs/handoff/evidencia/r-c4-trofeus.log` — trechos de log do R-C4 (calibração do 603)
- `docs/handoff/evidence/` — binário antigo, DB snapshot, patch do R-C1, 24101 gerado

---

## 4. REGRAS DO PROJETO (invioláveis)

1. **Uma mudança por vez.** Build → restart → avisar → o usuário testa no celular → anotar.
2. **Nunca** declarar "pronto" sem validação do usuário no jogo.
3. Prova sempre com **log + DB + pacote gerado/captura**.
4. **Não regredir** R-T7 (doações, login, vínculo de conta, mural) nem R-C1/R-C2a/R-C3.
5. Qualquer `.cs` novo → adicionar em `scripts/sources.rsp`.
6. Qualquer alteração no JSON da vila → respeitar o formato do golden 24101 (o cliente 7.65
   crasha com formato diferente).
7. Responder em **português**, direto, sem enrolação.
