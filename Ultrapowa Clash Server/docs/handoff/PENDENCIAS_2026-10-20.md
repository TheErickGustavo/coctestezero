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
- **Qualquer `.cs` novo precisa ser adicionado ao `scripts/sources.rsp`** (casing EXATO). O script e o
  manifesto usados no ambiente Linux não estão versionados na árvore GitHub desta branch; portanto essa inclusão
  precisa ser conferida no workspace local antes do build. Houve anteriormente um caso de `EndOfBattleCommand.cs`
  ausente do manifesto.

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
| **R-C4** | **Troféus PvP — validação parcial** | sem 603 = sem combate (não altera nada) validado; **a liquidação quando há 603 não é confiável ainda**, porque o campo parece ser `tick`, não destruição. Atualização/persistência do score do clã é um resultado separado já validado |
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
- `Unknown1` observado: `1138`, `637`, `689` — varia entre coletas e não coincide com a
  quantidade que o cliente mostrou. **Nova hipótese forte: é o `tick` do comando**, não tempo,
  quantidade nem autoridade de produção. O esquema público de protocolo lista o comando 506
  (`0x1FA`) como `buildingId + tick`; referência cruzada, ainda não específica da versão 7.65:
  [CommandComponent.json](https://github.com/clugh/coc-messages/blob/master/component/CommandComponent.json).
  Não creditar `Unknown1` diretamente. Confirmar comparando-o com ticks dos comandos vizinhos
  numa captura ordenada do mesmo `ExecuteCommandsMessage`.
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

### P-7 — Semântica do comando 603 (CRÍTICO: resultado PvP não confiável)

**Correção da hipótese anterior, revisão de 20/10/2026:** não há evidência suficiente para chamar o
valor do comando 603 de `damage points`. Um esquema público de protocolo identifica o comando 603
(`End combat`) com um único campo `tick: INT`; o comando 506 (`Collect resources`) também termina
em `tick: INT`. Fonte de referência cruzada (não é uma captura específica do 7.65):
[CommandComponent.json](https://github.com/clugh/coc-messages/blob/master/component/CommandComponent.json).
O payload local de 603 tem exatamente 4 bytes, compatível com esse campo. **Tratar o inteiro como dano
ou percentual está sem sustentação e deve ser considerado um bug de lógica, não uma calibração incompleta.**

Dados observados (preservados; o campo `value` pode ser o tick):

| horário | value (hex) | value (dec) | % que o cliente mostrou | scores atacante/defensor | delta do cliente |
|---|---|---|---|---|---|
| 07:45:26 | `00000236` | 566 | – | – | – |
| 07:54:39 | `00000115` | 277 | 2% | 4000/4000 | – |
| 13:39:05 | – | 3824 | 2% | 4000/4000 | -20 |
| 13:42:57 | – | 7968 | 16% | 4000/4000 | -17 |
| 14:21:06 | – | 1004 | 2% | 4000/4000 | -20 |
| 14:33:35 | – | 693 | 2% | 3980/4020 | -17 |
| 14:50:30 | – | 5188 | 2% | 3960/4040 | -15 |
| 15:07:49 | – | 712 | 2% | 3960/4040 | -12 |

**O que o código faz de arriscado hoje** (`EndOfBattleCommand.Execute` → `GoHomeMessage.ProcessPendingPvpBattle`):

- Registra o único inteiro como `ResultValue` e interpreta a presença do comando 603 como prova de resultado.
- Calcula `destructionPct = 100 * ResultValue / HP_total`, escolhe vitória em 50% e usa essa %
  para o bônus/delta de troféus. Isso pode transformar um contador/tick em destruição fictícia.
- A própria documentação/comentários do código divergem: o início de `GoHomeMessage` fala em
  percentual em décimos (`500 == 50%`), enquanto a implementação fala em `damage points`/HP total.
- Mesmo com percentual real, o código não observa estrelas nem trata explicitamente destruição do
  Centro da Vila abaixo de 50%; portanto `destructionPct >= 50` não reproduz todos os resultados do CoC.
- Como o inteiro vem do cliente, não pode ser usado como evidência autoritativa de dano/troféus sem validar
  primeiro o formato e a origem no protocolo.

**Próximo passo seguro**

1. Em uma captura de 7.65.5, guarde a mensagem `14102` completa, contagem de comandos, IDs e payloads em
   ordem — não apenas a linha isolada de `unknown-commands.log`.
2. Compare o inteiro 603 com os ticks dos comandos `PlaceAttacker`/`CastSpell` e com a progressão do cliente
   durante um combate sem implantação, uma implantação com 0% e um ataque que mostre percentual conhecido.
3. Localize no binário `LogicEndCombatCommand` os campos lidos/escritos e procure outra mensagem que transporte
   estrelas/percentual. O servidor atual não simula dano nem recebe eventos de edifícios destruídos.
4. Até resolver isso, classifique a parte de liquidação PvP como **experimental/não validada**. Preserve a
   proteção comprovada “sem 603 = não alterar troféus”; não use a fórmula atual para afirmar vitória/derrota real.

**Onde mexer depois da captura:** `Commands/EndOfBattleCommand.cs`, `GoHomeMessage.cs`, `Level.cs` e, se
o resultado não vier em pacote separado, o desenho do fluxo de combate. Não ajuste expoente/fórmula de troféus
antes de obter uma fonte correta para percentual/estrelas.

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

### P-10 — Delta de troféus (BLOQUEADO POR P-7)

Os valores `-20`, `-17`, `-15` e `-12` continuam sendo evidência útil, mas **não calibrar uma fórmula de
delta em função da coluna `value` de 603 enquanto ela provavelmente é `tick`**. O `ratio^13` é uma
hipótese empírica e não prova a fórmula histórica do CoC 7.65. Primeiro descobrir a origem de stars/%;
depois comparar atacante/defensor antes e depois, pontuação de ambos, Centro da Vila destruído, tempo e
todas as amostras de 7.65. Anotar separadamente: (a) presença do comando 603, (b) resultado visual do cliente,
(c) delta mostrado, (d) scores persistidos no DB. Não considerar R-C4 plenamente validado até esses pontos
concordarem.

### P-11 — `unit_prod` sempre precisa de `"t"` e `"slots"`

**Não é pendência, é ARMADILHA**: o cliente 7.65 **crasha** (StackOverflow nativo) se o
`unit_prod` do JSON da vila não tiver os dois campos. Qualquer coisa que grave o JSON da vila
tem que manter esse formato — ver `UnitProductionComponent.Save()` e o golden
`evidence/generated-24101-live-0558.bin`.

---

## 2.1 ACHADOS ADICIONAIS DA REVISÃO DE CÓDIGO (20/10/2026)

Estes itens vieram da inspeção dos fontes desta branch. São achados de código/protocolo, não testes práticos no Android.

### A-1 — Estado de recursos pode se perder quando o coletor está cheio (P-1 / persistência)

`ResourceStorageComponent.Load()` chama `SetupProduction()`, que cria um timer novo, lê `res_prod.avail` para
`m_vCurrentResources` e só substitui o timer se `res_prod.t > 0`. `GetCollectable()` não consulta
`m_vCurrentResources`; ele retorna a capacidade quando o timer é nulo, ou calcula tudo a partir do timer.
Logo, quando um coletor cheio é salvo com `t=0` e `avail=cap`, `Load()` mantém o timer cheio recém-criado
e o próximo `GetCollectable()` pode retornar quase zero: o `avail` persistido é ignorado. Além disso, produção
durante o tempo em que servidor/conta ficam descarregados não é reconstruída pelo código atual.
**Reprodução proposta:** comparar `res_prod` no DB antes/depois de reiniciar com coletor vazio, parcial e cheio;
logar `t`, `avail`, capacidade, `perHour`, `m_vTotalSeconds`, restante do timer e valor creditado. Corrigir em
um patch isolado e preservar o formato `unit_prod` (`t` + `slots`) em todas as respostas 24101.

### A-2 — Pedido de tropas já tem modelo de servidor; falta o tipo de stream-entry do cliente (P-2)

`TroopRequest`, `Alliance.AddOrUpdateTroopRequest` e `AllianceTroopService.Donate()` já mantêm pedidos e
validam tropas/capacidade. O problema visual está no envio: `RequestAllianceUnitsCommand.Execute()` converte
o pedido em `ChatBroadcast.MakeEntry(...)`, ou seja, texto `ChatStreamEntry` tipo 2. Um esquema público de
protocolo descreve `AllianceStreamEntry` extensão tipo 1 como “Troop request”, com dados de capacidade,
doações, texto opcional e total de tropas; tipo 2 é chat normal. Referência cruzada, a confirmar contra 7.65:
[AllianceStreamEntry.json](https://github.com/clugh/coc-messages/blob/master/component/AllianceStreamEntry.json).
**Não começar do zero nem copiar o wire format cegamente:** primeiro capturar/decodificar um pedido real 7.65,
comparar bytes com `StreamEntry.Encode()` e só então criar uma entrada dedicada, preservando 24312.

### A-3 — Comando 553 (`0x0229`) pode ser conhecido e hoje não está mapeado

Os logs guardam várias mensagens `cmd=553 len=8` com payloads de dois inteiros. A `CommandFactory` atual
não mapeia `0x0229`; quando encontra um comando desconhecido, `CommandFactory.Read()` consome todos os bytes
restantes do envelope e `ExecuteCommandsMessage.Process()` dá `break` ao receber `null`. Se 553 aparecer antes
de outros comandos no mesmo envelope, pode ocultá-los. O esquema externo acima descreve id 553 como dois
inteiros, o último `tick`, mas não dá nome semântico — é evidência cruzada, não confirmação do 7.65.
**Próximo passo:** correlacionar `NumberOfCommands` e os limites de cada envelope capturado; só depois adicionar
um parser no-op que leia exatamente os dois inteiros. Não trocar `break` por `continue` sem resolver o consumo
dos bytes, pois não há delimitador de tamanho individual para saltar um comando desconhecido com segurança.

### A-4 — O fallback de matchmaking favorece sempre o menor ID (risco de seleção/estado)

`ObjectManager.GetRandomPlayerExcluding()` primeiro usa níveis em memória; se a lista estiver vazia, itera
de ID 1 até `GetMaxPlayerId()` e para no primeiro jogador carregável diferente do atacante. Não é uma busca
aleatória entre todos os jogadores offline nem filtra vilas inválidas, score, Centro da Vila ou elegibilidade.
Isso pode repetir sempre o mesmo adversário. Antes de mudar, confirmar a escala com duas contas; depois adicionar
filtros explícitos e escolher aleatoriamente dentro da lista elegível.

### A-5 — `Level.Tick()` pode ser executado concorrente por duas rotas (produção/race)

`ObjectManager.TickOnlineLevels()` chama `level.Tick()` num `System.Threading.Timer` a cada segundo, enquanto
`ExecuteCommandsMessage.Process()` também chama `level.Tick()` ao processar pacotes. A execução não está
protegida por lock por `Level`, e `UnitProductionComponent.Tick()` pode completar produção e modificar a fila.
Os timers são baseados em tempo absoluto, então isto não prova que a produção acelere; prova que duas threads
podem manipular/serializar o mesmo estado sem sincronização. **Não reduza o intervalo para 200–500 ms ainda.**
Primeiro tornar o tick serial por `Level`, depois validar R-C5 e R-T7 no telefone.

### A-6 — Campanha: template existe, progresso individual não está ligado (P-3)

`AttackNpcMessage.Process()` só grava `Level.LastNpcAttackLevelId` (estado transitório) e manda `NpcDataMessage`.
`NpcLevel` tem propriedades `Stars`/`LootedGold`/`LootedElixir`, mas não há fluxo observado que atualize e persista
esses dados por jogador. `GoHomeMessage` só avança `TutorialStepsCount` para os dois primeiros NPCs; isso não é
progresso da campanha. Há também um limite de segurança: `NpcDataMessage` indexa `ObjectManager.NpcLevels`
diretamente com `LevelId - 0x01036640`, sem validar intervalo antes do acesso. Validar IDs sem alterar o pacote
24133 e definir onde salvar estrelas/saque antes de implementar.

### A-7 — Replay/histórico não tem fluxo de mensagens registrado no servidor (P-6)

A árvore desta branch não contém `BattleReportStreamEntry`; `MessageFactory` não registra o pedido 14114
`HomeBattleReplay`, e não há classe de resposta 24114 `HomeBattleReplayData`. Um esquema de protocolo público
lista esses IDs como pistas de pesquisa, mas sua compatibilidade com 7.65 precisa ser confirmada. O servidor
também não guarda eventos de colocação/dano suficientes para reconstruir sozinho uma replay fiel. Primeiro
capturar 14114/24114 e descobrir se o cliente envia replay serializado; depois implementar histórico e persistência.

### A-8 — Promoção muda o cargo em memória, mas não cria evento nem confirma persistência/broadcast (P-4)

`PromoteAllianceMemberMessage.Process()` termina em `targetMember.SetRole(m_vRole)`: não gera
`AllianceEventStreamEntry`, não emite `AllianceStreamEntryMessage` e não força gravação imediata do clã.
O autosave/logout pode salvar mais tarde, mas a ausência de evento explica por que a mensagem verde não aparece
e deixa uma janela em que a alteração pode não estar no DB. Preservar a autorização atual e, num patch isolado,
adicionar persistência/broadcast depois de confirmar os `eventType`/wire fields da versão 7.65.

### A-9 — Implantação de tropa não identifica de qual storage ela veio (P-5 / regressão de contagem)

`PlaceAttackerCommand` recebe coordenadas, tipo de tropa e um inteiro desconhecido; depois remove 1 unidade do
primeiro `UnitStorageComponent` que contém aquele tipo. Se o jogador tiver o mesmo tipo no acampamento e no
castelo do clã, o payload parseado não distingue a origem e o servidor pode subtrair do slot errado. Testar
caso duplicado (mesmo tipo no acampamento e no castelo) com log de cada storage antes/depois.

### A-10 — 24101 pode estar substituído por um golden estático (dependente de arquivo de runtime)

`OwnHomeDataMessage.Encode()` primeiro serializa e grava o estado vivo em `generated-24101.bin`, mas, se existir
`golden-24101.bin` no diretório de execução, envia o golden estático em vez do pacote recém-gerado. Isso mascara
qualquer alteração de vila/recursos/tropas. A evidência versionada `generated-24101-live-0558.bin` não é o mesmo
nome/caminho que a injeção usa; portanto não presumir que ela esteja ativa. Checar o log `GOLDEN INJECTION` e
verificar se `golden-24101.bin` existe antes de interpretar resultados.

### A-11 — A branch remota não inclui o script/manifesto real de build

A árvore do GitHub desta branch contém o projeto sob `Ultrapowa Clash Server/`, mas não contém
`scripts/build-server.sh` nem `scripts/sources.rsp`; esses caminhos descritos no ambiente são externos a este
snapshot do repositório. Portanto, não dá para verificar pelo GitHub se `EndOfBattleCommand.cs` ou futuros `.cs`
estão no manifesto usado pela máquina Linux. Antes do próximo build, conferir o `sources.rsp` local e adicionar
o arquivo com casing exato. Usar exclusivamente o script Mono especificado pelo usuário; nenhum build foi executado
nesta revisão.

### A-12 — Três pendências não devem ser “resolvidas” só trocando um campo

- **P-9 liga vazia:** `ClientAvatar.Encode()` manda `10` fixo embora `m_vLeagueId` e `GetLeagueId()` existam.
  O comentário diz que 10 vem de capturas, então não substituir pelo valor calculado sem comparar o pacote de
  perfil e os bytes golden — o vazio pode ser outro campo/mensagem.
- **Doações:** o caminho server-side já bloqueia heróis e valida espaço/lista de pedido; o visual do botão
  pertence ao formato da stream-entry. Não refatorar doação de tropa antes de validar a nova entrada.
- **R-C4:** manter separadas três provas: “sem 603 não muda score” (validado), “score de clã reflete score novo”
  (validado) e “vitória/estrelas/delta PvP corretos” (não validado por causa da semântica de 603).
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
