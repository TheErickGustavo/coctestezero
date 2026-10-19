# Auditoria técnica e plano de correção — CoC 2015 / UCS Zero

**Data da revisão:** 19/10/2026  
**Repositório:** `TheErickGustavo/coctestezero`  
**Branch de auditoria:** `audit/estado-e-plano-2026-10-19`  
**Base analisada:** `master` em `f8e26490d70f22cdc9ca5f37258e0a4eb3f44f92`  
**Escopo desta entrega:** leitura do código e da documentação versionada; plano e critérios de validação. Nenhuma lógica do servidor foi alterada neste documento.

---

## 1. Como ler esta auditoria

O relato original mistura quatro tipos diferentes de afirmação: sintomas observados no celular, hipóteses sobre causa, mudanças que uma IA diz ter feito e correções que o usuário diz ter validado. Para evitar regressão, este documento separa essas categorias.

- **Validado no jogo (documentado):** a documentação do repositório relata teste bem-sucedido pelo usuário. Isso não significa que eu tenha repetido o teste neste ambiente.
- **Confirmado no source:** a implementação atual contém (ou não contém) o comportamento citado.
- **Parcial:** existe uma parte do recurso, mas faltam outros caminhos, persistência ou garantias.
- **Pendente / não comprovado:** o código atual ou as evidências disponíveis não demonstram que esteja resolvido.
- **Hipótese:** explicação plausível que requer captura, log, estado de banco ou teste controlado para ser confirmada.

**Limite da auditoria:** tenho acesso ao repositório público e aos arquivos versionados, não ao processo Mono, ao binário que está executando no computador, ao banco ativo, ao telefone ou ao ambiente local citado nos handoffs. Portanto, não afirmo que o binário em execução corresponde ao `master`, que o servidor foi reiniciado, que o build passa ou que o cliente móvel confirma o estado atual. Para isso, é necessário comparar source, build e processo e testar no jogo.

O histórico e os arquivos de handoff mencionam `e4e3648`, um reset e o HEAD local `cb88f4c`. A API usada nesta auditoria não conseguiu resolver `e4e3648` como commit no estado público atual, mas a árvore atual contém `docs/handoff/evidence/e4e-RC-promote-persist-clan-league-TH.patch` e descreve os arquivos restaurados. A conclusão prudente é: **há evidência versionada da recuperação; não foi possível verificar o reflog nem o incidente local independentemente**.

## 2. Diagnóstico executivo

O problema não é apenas uma coleção de telas erradas. Há vários lugares em que o mesmo dado é mantido ou serializado de forma independente:

1. **Estado autoritativo duplicado:** as tropas existem nos componentes de armazenamento físicos e também na lista `units` do avatar. O comando de batalha consome armazenamentos, enquanto o cliente também recebe contadores do avatar. Sem reconciliação, essas representações podem divergir ou ficar negativas.
2. **Fluxo de batalha incompleto:** a busca envia a vila de um jogador aleatório, mas não registra quem é o oponente na sessão. O comando 603 continua não registrado no `CommandFactory`; o retorno para casa não aplica resultado PvP, troféus, saque ou histórico.
3. **Persistência desigual:** há salvamento ao desconectar no source, mas não foi encontrada rotina periódica de salvamento no `Program`/timer atual nem tratamento explícito de desligamento. O código de desconexão também ignora silenciosamente exceções.
4. **Pacotes com campos hardcoded:** a lista do clã tem correções reportadas e codificadas, mas o serializador geral do avatar ainda envia liga fixa 10. Isso ajuda a explicar por que liga pode aparecer corretamente numa tela e vazia/incorreta em outra.
5. **Recursos de clã incompletos:** promoção funciona segundo as notas; cargo em mensagens continua fixo em membro; evento de expulsão já tem implementação, mas promoção/entrada/saída não estão integradas; eventos não são mantidos quando a aliança é serializada.
6. **Falta de proteção contra regressão:** não encontrei projeto de testes automatizados ou workflow de CI na árvore consultada. As mudanças têm de ser pequenas e provadas com logs, dados persistidos, pacote capturado e teste no telefone.

**Prioridade recomendada:** proteger os dados e estabelecer a versão real executada; reparar a integridade das tropas e impedir o autoalvo; medir corretamente o protocolo de fim de batalha antes de conceder troféus; depois implementar settlement idempotente; por fim atacar perfil, eventos de clã, campanha, munição, relatórios e UX de múltiplas contas. Não misturar essas frentes num commit grande.

---

## 3. Estado atual por problema do relato

Os números abaixo seguem os itens descritos no relato original (que começa no “segundo problema”).

| Item | Estado nesta revisão | Evidência principal | Próxima ação |
|---|---|---|---|
| **2 — código fantasma/reset Git** | Recuperação documentada; incidente local não verificável só pelo source público | `docs/handoff/evidence/e4e-RC-promote-persist-clan-league-TH.patch`; handoffs divergem do HEAD público atual | Nunca resetar sem snapshot; conferir SHA, diff, binário, processo e backup antes de reiniciar |
| **3 — promover a Ancião/Co-líder** | **Codificado como corrigido; teste no celular relatado como validado** | `PacketProcessing/Messages/Client/PromoteAllianceMemberMessage.cs`; mapeamento 14306 em `PacketProcessing/MessageFactory.cs`; notas RC1 | Não reimplementar. Revalidar após prova de build/binário e verificar persistência/evento em mudança separada |
| **4 — pontuação/nível do clã** | Migração e pacote codificados; teste no celular relatado como validado; atualização dinâmica pós-batalha ainda pendente | `Logic/Alliance.cs` (`RecalculateScore`, migração 10000/10/8599); `Messages/Server/AllianceDataMessage.cs` | Conectar recálculo ao serviço transacional de alteração de troféus, não só à migração de dados antigos |
| **5 — ordem de membros 0-based** | Codificado e relatado como validado | `Messages/Server/AllianceDataMessage.cs`, `memberOrder = 0` e incremento após cada membro | Manter teste de regressão de pacote; não alterar ordem por hipótese |
| **6 — liga na lista/chat do clã** | Melhoria relatada como efeito colateral validado; perfil é caminho separado | League dinâmica nos campos de membro de `AllianceDataMessage.cs`; notas RC1 | Preservar o comportamento e testar separadamente lista, chat e perfil |
| **7 — emblema de liga vazio no perfil** | **Pendente / divergência no serializador** | `Logic/ClientAvatar.cs`, `Encode()` escreve `data.AddInt32(10)`; `AvatarProfileMessage.cs` usa esse encoder para 24334 | Capturar 24334 válido e localizar o campo exato antes de alterar o valor |
| **8 — botão editar clã para membro comum** | Permissão/UX pendente; cliente não está neste repositório | `PacketProcessing/MessageFactory.cs` tem 14136 comentado como “Edit Clan Messages”; não há fluxo servidor correspondente | Separar visibilidade no cliente de autorização server-side; qualquer handler futuro deve validar líder/co-líder |
| **9 — cargo de mensagens do chat sempre “membro”** | **Confirmado no source como pendente** | `Logic/StreamEntry/StreamEntry.cs`, `SetAvatar()` define `m_vSenderRole = 1` | Resolver o cargo atual do remetente na aliança e gravar um snapshot consistente na entrada |
| **10 — avisos de sistema no mural do clã** | **Parcial**; não é verdade que todas as classes estejam ausentes | `AllianceEventStreamEntry.cs` existe; `KickAllianceMemberCommand.cs` emite evento; promover/entrar/sair não têm integração equivalente | Unificar criação/broadcast/persistência dos eventos; cobrir promoção, entrada, saída e expulsão |
| **11 — troféus de batalhas PvP** | **Pendente; comando 603 ainda ignorado** | `PacketProcessing/CommandFactory.cs` não registra 0x025B/603; `GoHomeMessage.cs` só cuida de tutorial e volta para casa | Registrar e medir o comando sem conceder troféus até validar payload e condições de vitória |
| **12 — sortear a própria vila** | **Confirmado no source como pendente** | `Commands/SearchOpponentCommand.cs` chama `ObjectManager.GetRandomPlayer()` sem excluir o atacante nem gravar o alvo | Filtrar ID próprio e candidatos inválidos; guardar oponente selecionado na sessão |
| **13 — histórico de ataque/defesa/replay** | Pendente; não há pipeline completo de relatório PvP | Não foram encontrados modelos/serviços de relatório e replay; `GoHomeMessage` não cria registro PvP | Implementar somente depois de settlement estável, com persistência separada e política de retenção |
| **14 — contadores de tropas e tropas na Fábrica de Feitiços** | **Risco alto; confirmado por caminhos de código** | `PlaceAttackerCommand.cs`, `RemoveUnitsCommand.cs`, `UnitStorageComponent.cs`, `UnitProductionComponent.cs`, `DatabaseManager.GetAccount()` | Fazer backup; definir armazenamento físico como fonte de verdade; reparar dados com dry-run; filtrar Fábrica de Feitiços |
| **15 — vila parece voltar ao padrão / persistência** | Parcial; salvamento ao desconectar existe, salvamento periódico/encerramento não comprovado no source | `Core/ResourcesManager.cs` → `DropClient()` salva jogador/alianças; timer `ReleaseOrphans` não salva; `Program.cs` dorme indefinidamente | Testar logout/crash/restart separadamente; instrumentar falhas; criar autosave/shutdown controlados em mudança isolada |
| **16 — munição de defesa não persiste** | Pendente | `Commands/LoadTurretCommand.cs` apenas decodifica três inteiros e não tem `Execute`; não foi achado estado de munição no objeto de defesa | Capturar o comando e formato de estado; adicionar campo persistido e round-trip load/save |
| **17 — campanha goblin não avança** | Pendente / parcialmente funcional só para tutorial | `Messages/Server/NpcDataMessage.cs` procura arquivos `gamefiles/pve/golden-level-0x*.bin`; não aparecem na árvore pública consultada; `ClientAvatar.Encode()` envia grupos NPC zerados; `GoHomeMessage` só incrementa dois passos do tutorial | Incluir assets reproduzíveis/licenciáveis; serializar estrelas/saque reais; implementar resultado/progressão NPC |
| **18 — aviso de Centro de Vila nível 3** | Não comprovado; campo no avatar está marcado como placeholder | `ClientAvatar.Encode()` envia `GetTownHallLevel() + 1` no campo comentado como usado no popup | Comparar payloads reais e fazer bisseção de um campo por vez; não modificar o nível real no banco |
| **19 — alternar entre várias contas** | Parcial e com cautela de segurança | `Logic/GoogleBindService.cs` escolhe automaticamente o próximo ID existente; tokens `local-pass-*` são falsos; `BindGoogleServiceAccountMessage.cs` lê access token sem validá-lo | Tratar como implementação de desenvolvimento/local; não expor publicamente sem autenticação real e fluxo explícito de seleção |

---

## 4. Achados técnicos que mudam o plano

### 4.1 Git, binário e processo são três estados diferentes

O handoff local registra o processo ainda executando um binário que tinha correções em memória enquanto o arquivo em disco já havia sido recompilado a partir de source revertido. Esse tipo de divergência é plausível e não é resolvido olhando somente para o GitHub.

Antes de cada restart, guardar e conferir:

1. SHA do commit e `git status --short`;
2. `git diff` e arquivos não rastreados;
3. backup de banco ativo, configuração, binário e logs;
4. data/tamanho/hash do executável em disco;
5. PID, hora de início e caminho de execução do processo;
6. hash do executável novo depois do build e log de inicialização.

Recomendação: incluir SHA de source/build e versão do protocolo no log de startup. **Não usar `git reset --hard`, checkout destrutivo ou limpeza automática como ferramenta de “sincronização”** sem backup e confirmação do conteúdo local. O binário antigo em memória não é uma fonte permanente de recuperação.

### 4.2 Promoção: a regra de autoridade é a correção certa, mas os eventos e o save são trilhas separadas

No protocolo, o número do cargo não é a autoridade ordenada. O source atual mapeia autoridade assim: membro (role 1) = 1; Ancião (role 3) = 2; Co-líder (role 4) = 3; Líder (role 2) = 4. Isso evita a comparação bruta que rejeitava o role 3 quando o solicitante tinha role 2.

O handler atual também limita a operação a líder/co-líder, verifica aliança e alvo e não permite que o solicitante conceda ao alvo autoridade igual/superior. Não alterar essa lógica sem caso reproduzível.

Entretanto, a mensagem de promoção apenas faz `targetMember.SetRole(m_vRole)` e escreve log. Ela não cria evento de mural nem chama diretamente o banco. Deve ser verificado se a persistência vem do fluxo de desconexão/salvamento periódico do ambiente local; para resultado previsível, o serviço de mudança de cargo deverá atualizar o estado e agendar/salvar a aliança de forma explícita, sem misturar isso à correção visual.

### 4.3 O score correto do clã é uma regra de domínio, não um valor fixo

A migração atual somente dispara quando a aliança combina exatamente score 10000, nível 10, experiência 8599 e nenhuma guerra. Isso é conservador para não sobrescrever progresso legítimo, mas significa que não corrige automaticamente toda variação possível de dados históricos.

Mais importante, `Alliance.RecalculateScore()` existe, porém não foi encontrado um gancho geral em `ClientAvatar.SetScore()`: este atualiza o score pessoal e chama `UpdateLeague()`, mas não recalcula e persiste a aliança. Portanto, a migração inicial pode deixar o clã em 8000, mas o total ficará desatualizado após uma batalha se o serviço de resultado não recalcular ambos os clãs envolvidos.

Regra pretendida: score do clã = soma dos troféus dos membros atuais. A implementação deve calcular a partir do estado autoritativo e salvar jogador(es) + aliança(s) juntos quando possível. Não chamar `GetPlayer(id, true)` recursivamente dentro de um recálculo em massa sem analisar carga, locking e consistência; prefira receber os estados já carregados ou usar um serviço de domínio com limites claros.

### 4.4 Perfil e lista do clã usam caminhos de serialização diferentes em intenção, mas hoje compartilham o encoder

Na lista de membros 24301, `AllianceDataMessage` lê `GetLeagueId()` e `GetScore()` do avatar; a função `UpdateLeague()` calcula liga 0–16 com base no score, incluindo Champion a partir de 3200. As notas relatam que o emblema Champion passou a aparecer corretamente na lista do clã/chat após a mudança da ordem 0-based.

No entanto, o encoder geral em `ClientAvatar.Encode()` escreve o inteiro 10 no campo comentado “league” e documenta que capturas reais daquela família de pacote mostravam 10. `AvatarProfileMessage` (24334) prefixa o perfil com esse mesmo `Encode()`. Isso confirma a divergência do source, mas **não autoriza substituir 10 por `GetLeagueId()` às cegas**: o campo pode ter semântica diferente conforme contexto/versão do cliente. A próxima alteração exige captura de 24334 esperado e atual, identificação de offset/campo e comparação com tela de perfil.

### 4.5 Eventos de clã: a implementação está incompleta e a persistência descarta eventos

O repositório já tem `AllianceEventStreamEntry` (tipo 4) e `AllianceKickOutStreamEntry` (tipo 5). `KickAllianceMemberCommand` cria um evento do clã quando expulsa alguém e o transmite aos membros online. Isso contradiz a hipótese de que “nenhuma classe de evento existe”.

As lacunas reais no source:

- `PromoteAllianceMemberMessage` muda o cargo, mas não emite evento de promoção/rebaixamento;
- `JoinAllianceMessage` adiciona o membro e responde ao cliente que entrou, mas não transmite anúncio de entrada para o restante do clã;
- `LeaveAllianceMessage` remove o membro e manda confirmação ao solicitante, sem anúncio de saída;
- `Alliance.SaveToJSON()` só serializa mensagens que podem ser convertidas para `ChatStreamEntry`, pulando `AllianceEventStreamEntry` e outros tipos de stream.

Plano: centralizar a criação de eventos e decidir explicitamente quais eventos podem ser serializados. Testar mensagem enviada ao vivo, histórico ao pedir o mural, e sobrevivência após restart. Não adianta criar a entrada se o broadcast ou a persistência ainda estiver ausente.

### 4.6 603 é um marcador do fim da batalha, mas o significado exato precisa ser medido

O factory de comandos contém 0x0258 (600) para colocação de tropa, 0x0226 (550) para remoção, 0x025C (604) para feitiço e 0x02BC (700) para busca de adversário; 0x025B (603) não está registrado e não foi encontrada uma classe `EndOfBattleCommand`. O factory tenta registrar o comando desconhecido no arquivo `unknown-commands.log`, mas devolve `null`; o parser de `ExecuteCommandsMessage` interrompe o processamento ao receber null. Isso pode descartar qualquer comando subsequente dentro do mesmo payload, então a telemetria precisa ser cuidada para não esconder a sequência dos comandos.

O handoff descreve payload de quatro bytes e valores decimais 566/277, interpretados como 5,66%/2,77% de destruição. É hipótese forte, ainda não prova. Teste obrigatório antes de calcular troféus:

1. Registrar score inicial de atacante e defensor, sem ataques concorrentes;
2. Fazer ataque controlado e anotar porcentagem/estrelas/resultados visíveis;
3. Guardar payload completo e ordem dos comandos 603 e 14101;
4. Repetir com um resultado claramente diferente (por exemplo, vitória de 1 estrela e derrota abaixo de 50%);
5. Correlacionar bytes com tela e estado salvo.

Primeira alteração recomendada para este ponto: **decodificar e logar o 603 de forma não destrutiva**, sem mudar score. A lógica de premiação só entra quando formato e gatilhos forem confirmados.

### 4.7 Busca de oponente precisa guardar um contexto de batalha

`SearchOpponentCommand.Execute()` escolhe `ObjectManager.GetRandomPlayer()`, dá Tick na vila escolhida e envia `EnemyHomeDataMessage`; não exclui o próprio jogador e não registra a identidade daquele adversário. O resultado da batalha posterior, portanto, não pode ter segurança de que sabe quem foi atacado.

O conserto deve:

- excluir o ID da conta atacante;
- excluir null, conta inválida e candidatos sem vila carregável;
- definir um limite de tentativas e uma resposta segura se não houver alvo;
- registrar ID do defensor e um identificador de batalha/contexto no atacante;
- invalidar esse contexto quando batalha termina, abandona ou expira;
- impedir que um 603 duplicado ou um segundo GoHome aplique resultado duas vezes.

Não assumir que qualquer jogador aleatório disponível é um adversário válido. Mesmo em ambiente de teste com duas contas, o atacante não pode ser escolhido como alvo.

### 4.8 Tropas: fonte de verdade e roteamento estão inconsistentes

A lógica atual revela vários pontos concretos:

- `PlaceAttackerCommand` busca unidade nos componentes de armazenamento de tipo 0 e remove do primeiro armazém que encontrar, sem excluir Fábrica de Feitiços. Não valida ID de building como acampamento.
- `RemoveUnitsCommand` também percorre os mesmos componentes de armazenamento, que podem incluir spell forge.
- `UnitStorageComponent.AddUnitImpl` acrescenta uma unidade a um armazenamento e em geral incrementa também o contador do avatar. `RemoveUnitsImpl` reduz o armazenamento e subtrai do avatar sem clamp para zero. Essa atualização acontece inclusive sem garantir que o armazenamento seja exército próprio.
- `UnitProductionComponent.ProductionCompleted()` procura o componente de armazenamento mais próximo, verifica se há espaço e chama `AddUnit(cd)`; a seleção não parece excluir armazenamento de feitiços nem validar a categoria da unidade. A propriedade `m_vIsSpellForge` existe, mas esse caminho não a usa para rotear o destino.
- `CanAddUnitToQueue()` avalia capacidade de produção e espaço, não valida que o item seja da categoria correta para aquele edifício.
- Ao carregar a conta, `DatabaseManager.GetAccount()` chama `AllianceTroopService.SyncAllianceUnits(account)` para sincronizar tropas de castelo, mas não foi encontrada rotina equivalente que reconstrua os contadores normais do avatar a partir dos acampamentos.

Isso é suficiente para justificar os relatos de unidade na Fábrica de Feitiços e números divergentes, ainda que não prove sozinho cada valor negativo observado no banco.

**Modelo de consistência recomendado:**

- Acampamentos físicos = fonte autoritativa de tropas comuns prontas para batalha.
- Fábrica de Feitiços = fonte autoritativa apenas de feitiços; nunca armazena tropas comuns.
- Castelo do clã = armazenamento separado de tropas doadas; nunca entra nos contadores do exército próprio.
- `avatar.units` / pacote = projeção calculada a partir dos armazenamentos próprios, não uma segunda verdade independente.
- Toda remoção valida quantidade disponível e nunca produz valor negativo. Uma solicitação acima do saldo é rejeitada/clampada com log explícito, não deixa saldo negativo.
- Produção, cancelamento, speed-up e persistência devem respeitar o tipo de armazenamento.

Antes de reparar o banco, exportar snapshot do arquivo SQLite e produzir relatório dry-run por jogador: tipo de tropa, quantidade de cada acampamento, quantidade na spell forge, contadores do avatar antes/depois, excesso/inconsistência. Validar a regra contra as capturas reais da versão 7.65 antes de aplicar migração. Não usar o avatar atual (potencialmente corrupto) para “corrigir” o armazém.

### 4.9 Persistência: “ao desconectar” não equivale a “salva a cada minuto”

O método `ResourcesManager.DropClient()` tenta salvar o Level e as alianças ao remover um cliente. Isso é positivo. Mas a exceção está num `catch { }`, de forma que um erro de banco pode passar sem mensagem. O timer criado pelo `ResourcesManager` chama `ReleaseOrphans` a cada 60 segundos; no source atual, esse método somente dá `Dispose()` no timer se `m_vTimerCanceled` estiver true e não salva contas. `Program.Main()` inicia os componentes e depois faz `Thread.Sleep(Timeout.Infinite)`; não foi encontrado handler de shutdown ou rotina periódica de persistência nessa classe.

Por isso, a descrição do handoff de que o servidor “salva a cada minuto” não é sustentada por esses arquivos específicos do source público atual. Pode existir comportamento adicional no binário local ou num working tree que não foi publicado, mas isso precisa ser provado.

Teste mínimo de persistência:
1. alterar uma coisa pequena e reversível (por exemplo, treinar uma única tropa);
2. desconectar normalmente e reconectar;
3. fazer o mesmo teste sem esperar pelo tick de 60 s;
4. repetir em reinício controlado do servidor;
5. analisar DB antes/depois e logar sucesso/falha de cada Save;
6. simular encerramento/crash apenas com backup e conta de teste.

A implementação futura deve ter autosave de intervalo definido, salvamento ao desconectar e salvamento no encerramento com timeout, logs estruturados e tratamento de exceções. Evitar operações de IO concorrentes que sobrescrevam snapshot mais recente com dados antigos.

### 4.10 Campanha, defesas e conta vinculada são funcionalidades com dependências próprias

**Campanha:** `NpcDataMessage` usa arquivo de replay binário, se disponível, com caminho relativo `gamefiles/pve/golden-level-0x...`; esses arquivos não apareceram na árvore pública consultada. O fallback envia JSON, avatar atual, zero e LevelId. O avatar persiste listas `npc_stars`, `npc_looted_gold`, `npc_looted_elixir` no JSON, mas o `Encode()` atual ainda escreve grupos NPC zerados. `GoHomeMessage` só altera o contador de tutorial para dois níveis conhecidos. Corrigir mapa, estado de progresso e resultado NPC num fluxo consistente; não tratar o valor de nível do Centro de Vila como causa sem evidência.

**Munição:** `LoadTurretCommand` só decodifica dados e não implementa `Execute`; não há campo de munição visível sendo salvo no building. Capturar o payload real, identificar o componente/arma envolvido e definir o round-trip serializado antes de adicionar um campo.

**Múltiplas contas:** `GoogleBindService` escolhe automaticamente o próximo ID numérico e usa tokens `local-pass-*`; `BindGoogleServiceAccountMessage` lê access token, mas não o valida. Isso é adequado apenas como simulação local de fluxo. Não deve ser exposto publicamente nem tratado como autenticação segura. Para o requisito de 2/5/10 vilas, é necessário um seletor explícito ou fluxo claro de conta vinculada, mais autenticação que não aceite identidade fornecida livremente pelo cliente.

---

## 5. Plano de execução recomendado

**Regra de trabalho:** uma alteração de código por vez. Não acumular fases antes de build/teste da anterior. Os nomes R-0..R-8 abaixo são rótulos de plano, não alegações de implementação.

### R-0 — Congelar e identificar a linha de base (nenhuma alteração de gameplay)

**Ações**
1. Conferir branch/SHA/status e salvar `git diff` e arquivos não rastreados.
2. Fazer cópias do DB ativo, configuração, logs e executável atual; calcular hashes.
3. Identificar PID/hora de início, caminho do executável e comparar mtime/hash contra o último build.
4. Registrar uma assinatura do build (commit SHA + timestamp) no startup log.
5. Rodar smoke tests de features que já funcionam: login/troca de conta local, mural, pedido/doação de tropas, promoção, lista do clã.

**Aceite:** há backup recuperável, source e binário estão identificados sem ambiguidade, e existe uma lista explícita das funcionalidades que não podem regredir.

### R-1 — Persistência observável e controlada

**Alteração isolada:** primeiro substituir `catch { }` crítico por logs com contexto e provar Save no disconnect; não introduzir autosave e shutdown ao mesmo tempo. Depois, em mudança separada, implementar autosave e shutdown controlado.

**Testes:** uma mudança mínima deve sobreviver a logout/relogin e restart controlado. Confirmar JSON/DB, logs e ausência de exceção. Forçar um erro em ambiente de teste deve produzir log e não uma alegação falsa de sucesso.

**Aceite:** a conta e a aliança gravadas correspondem ao estado esperado; qualquer falha é observável; nenhuma rotina de save concorre silenciosamente com outra.

### R-2 — Corrigir a seleção de adversário (baixo escopo)

**Alteração isolada:** SearchOpponent deve excluir o ID atacante, validar candidatos e armazenar o oponente selecionado no contexto de batalha.

**Testes:** com duas contas, pesquisar várias vezes e nunca receber a própria vila; verificar também ausência de candidato válido, alvo desconectado e chamadas repetidas.

**Aceite:** o alvo não pode ser o próprio ID; a batalha tem um defensor bem definido guardado no servidor; o resultado não pode se associar a alvo diferente daquele enviado.

### R-3 — Decodificar o comando 603, sem atribuir recompensa

**Alteração isolada:** registrar 0x025B, decodificar o payload conhecido de 4 bytes e salvar o valor bruto + ID de batalha/contexto + timestamp. Inicialmente, não mexer em score nem no resultado da batalha.

**Teste do usuário:** ataque controlado, comparar payload 603 com porcentagem/estrelas e delta que o cliente mostra; repetir pelo menos em duas condições distintas. Guardar pacote completo e ordem de comandos.

**Aceite:** o valor decodificado tem correspondência observada e repetível com a tela; parser não perde comandos subsequentes; nenhuma batalha altera troféus nessa fase.

### R-4 — Reconciliar e proteger contadores de tropas

**Preparação obrigatória:** snapshot e dry-run do banco; registrar por building o conteúdo exato antes/depois proposto. Não fazer migração destrutiva em todo o DB como primeira tentativa.

**Alterações em passos separados:**
1. excluir Fábrica de Feitiços e Castelo do clã de operações de exército próprio;
2. validar categoria da tropa no prédio de produção e armazenamento;
3. remover apenas quantidades disponíveis e impedir negativos;
4. reconstruir contadores do avatar a partir dos acampamentos no load/projeção;
5. implementar migração do banco somente após relatório e validação de uma conta de teste.

**Teste:** capacidade 240 com exército conhecido, por exemplo 12 dragões conforme a tabela da versão; atacar e soltar quantidade conhecida; comparar UI antes/depois, comandos 600/550, cada building no JSON e campos de avatar. Repetir após relogin/restart.

**Aceite:** contagem de avatar = soma de acampamentos para tropas próprias; feitiços só na spell forge; doadas só no castelo; zero negativo; nenhuma unidade fantasma após reabrir sessão.

### R-5 — Resolver fim da batalha uma única vez

Só iniciar depois de R-2, R-3 e dos invariantes de tropa de R-4 terem evidência suficiente.

**Desenho recomendado:** um serviço único `BattleResultService` recebe contexto de batalha guardado no servidor + resultado validado. Não deixar cálculo espalhado entre 603, GoHome, avatar e aliança.

Responsabilidades:
- validar batalha ativa e seu oponente;
- validar o resultado recebido (destruição, estrelas, condição de vitória e outros dados quando confirmados);
- encerrar/inativar a batalha e rejeitar processamento duplicado;
- calcular troféus pela fórmula confirmada para essa versão do jogo, não por tentativa;
- atualizar scores dos dois jogadores de acordo com regras comprovadas;
- atualizar liga de ambos;
- recalcular os scores dos clãs de ambos (se houver clã);
- persistir os jogadores e alianças afetados;
- emitir log estruturado com score antes/depois, tipo de resultado e battle ID.

**Testes:** derrota com destruição abaixo de 50%, vitória por 1 estrela, vitória por 2/3 estrelas, alvo com score diferente, alvo que sai de clã, GoHome duplicado e 603 repetido. Não creditar troféu duas vezes se o cliente reenvia ou reconecta.

**Aceite:** resultado exibido pelo cliente e estado persistido concordam; o score do clã corresponde à soma atual; uma batalha gera no máximo um settlement.

### R-6 — Perfil e eventos/permissões de clã

Dividir esta fase em commits pequenos:

- **R-6a, perfil:** capturar/decode de 24334 e identificar a posição real do campo de liga. Alterar campo específico/encoder específico, evitando alterar o valor hardcoded comum até conhecer todas as suas semânticas. Confirmar que a mudança não quebra login, visita de vila e lista do clã.
- **R-6b, cargo do chat:** antes de criar `ChatStreamEntry`, consultar cargo na aliança do remetente e guardar o valor no snapshot. Definir fallback seguro quando o jogador já saiu do clã.
- **R-6c, eventos de clã:** centralizar eventos de promoção/rebaixamento, entrada, saída e expulsão; persistir eventos do tipo apropriado; testar broadcast online e histórico após restart.
- **R-6d, editar clã:** como o código do cliente não está neste repo, pode não ser possível esconder o botão a partir daqui. Ainda assim, qualquer comando de editar precisa rejeitar no servidor cargos abaixo do permitido. Não considerar “abre o botão” prova de permissão concedida; validar o estado efetivamente salvo.
- **Save de cargo/evento:** alteração de role e evento devem sobreviver ao restart. A lista e o perfil têm de refletir o mesmo cargo.

**Aceite:** cada tela mostra o cargo/liga correto; eventos aparecem para membros online e no histórico após reconexão; cargo comum não consegue persistir mudança administrativa.

### R-7 — Funcionalidades de jogo: campanhas, munição e relatórios

Executar nesta ordem, com cada feature isolada:

1. **Defesas:** capturar `LoadTurret` (0x020D), confirmar building ID e formato; criar estado de munição/fuel no componente correto; serializar e restaurar; testar reload e reinício.
2. **Campanha NPC:** garantir arquivos de mapa/golden versionados e caminho estável; enviar listas de estrelas e saques do jogador; processar resultado NPC uma vez; desbloquear progressão; validar recompensas e tropas usadas.
3. **Battle log:** criar registro persistente de ataque/defesa com atacante, defensor, timestamp, destruição, estrelas, delta de troféus, saque, tropas e resultado.
4. **Replay:** só se houver dados de comandos/eventos suficientes para reproduzir a batalha e se o cliente 7.65 aceitar o formato exato. Não mostrar botão funcional sem payload válido.

**Aceite:** campanhas desbloqueiam com dados persistentes; munição sobrevive a sessão; relatórios sobrevivem a restart e não são criados duas vezes; replay abre um conteúdo verdadeiro, não um placeholder.

### R-8 — Seletor de contas e hardening do vínculo

- Especificar UX real para escolher entre contas já existentes (2, 5, 10 ou N), em vez de apenas “próximo ID”.
- Separar ID local de conta, identidade externa e token de autenticação.
- Nunca aceitar `accessToken` sem validação; remover tokens previsíveis `local-pass-*` de qualquer uso fora de ambiente de teste.
- Exigir confirmação/flow explícito para substituir vínculo e impedir que um cliente vincule uma identidade arbitrária a outra vila.
- Testar troca de conta com estado salvo e sem sobrescrever dados da vila anterior.

**Aceite:** a conta escolhida é explícita, carregada corretamente e não permite apropriação de conta ao fornecer um ID externo.

---

## 6. Matriz mínima de testes de regressão

Executar e registrar os testes aplicáveis após cada mudança; não esperar até o final para descobrir regressão:

| Área | Cenário mínimo | Evidência exigida |
|---|---|---|
| Build/runtime | Build limpo + restart | saída do build, SHA do source/build, PID novo, log de startup |
| Login/persistência | login → mudar dado → logout → relogin | DB antes/depois + log Save |
| Clan roles | promover Ancião/Co-líder; tentar operação sem autoridade | role persistido, pacote 24301, resultado visual |
| Clan score | alterar troféus de membro do clã | score dos membros e soma do clã no DB/UI e busca |
| Chat | mandar mensagem como cada cargo | sender role no payload/entrada e rótulo no cliente |
| Eventos | entrar/sair/expulsar/promover | entrada transmitida + sobrevivência no JSON após restart |
| Busca PvP | procurar oponente repetidamente | ID atacante e ID alvo; nunca iguais |
| Fim de batalha | cenários abaixo/acima de 50% e repetições | payload 603, tela de resultado, estado final e battle ID |
| Tropas | treinar, atacar, gastar, relogar | lista por building, contadores do avatar, log de comandos |
| Spell forge | produzir feitiço e tropas | categoria correta em cada armazenamento após reload |
| Campanha | concluir nível e abrir próximo | payload NPC, listas de progresso, JSON persistido |
| Defesa | carregar torre e reiniciar | comando de carga, ammo state no save/load |
| Perfil | ver próprio perfil e de outra conta | pacote 24334 comparado com payload decodificado e UI |
| Contas | alternar entre contas selecionadas | ID carregado, vila, troops/resources/clan persistidos |

### Formato de anotação recomendado

Para cada teste, guardar:
- ID curto do teste e data/hora;
- commit SHA + hash/timestamp do binário;
- conta atacante/alvo (IDs de teste, não tokens);
- ação exata e resultado esperado;
- resultado observado no celular;
- excerto relevante do log;
- snapshot do DB antes/depois ou diff JSON;
- pacote bruto e decoder utilizado;
- decisão: **PASS**, **FAIL** ou **BLOCKED**;
- próximo passo, sem iniciar segunda correção se o teste falhar.

---

## 7. Critérios para declarar um bug resolvido

Uma alteração só pode ser marcada como concluída quando todas as condições aplicáveis forem atendidas:

1. a mudança está commitada e o SHA é conhecido;
2. o build que contém o SHA terminou com sucesso;
3. o processo executa esse build (não basta compilar ou reiniciar separadamente);
4. log confirma o handler/caminho esperado;
5. estado persistido após logout/restart corresponde ao esperado;
6. payload enviado pelo servidor bate com o contrato analisado;
7. usuário confirmou o resultado no celular para bugs visuais/jogabilidade;
8. smoke test das funções já validadas não regrediu.

Comentários no código, log de “executou” e ausência de exceção isoladamente não contam como teste de comportamento. Uma correção de protocolo deve sempre ser verificada nos bytes recebidos pelo cliente.

## 8. Não fazer

- Não fazer reset/checkout destrutivo para “arrumar” o working tree.
- Não trocar campos do protocolo por palpite, principalmente campos comuns no encoder do avatar.
- Não calcular troféus antes de confirmar o significado do 603 e a fórmula da versão.
- Não corrigir contadores corrompidos sem snapshot e relatório de impacto por conta.
- Não inferir que o score dinâmico está correto só porque a migração inicial funcionou.
- Não afirmar que o salvamento ocorre a cada minuto sem apontar rotina de código/log que o comprove.
- Não fechar relatório/replay com dados artificiais ou vazios só para preencher a tela.
- Não tratar `GoogleBindService` atual como autenticação segura para ambiente público.
- Não marcar “resolvido” sem a validação exigida e não juntar diversas correções para um único teste.

## 9. Arquivos-chave para a próxima sessão

- `Ultrapowa Clash Server/PacketProcessing/CommandFactory.cs` — registro/dispatch de comandos, incluindo lacuna 603.
- `Ultrapowa Clash Server/PacketProcessing/Commands/SearchOpponentCommand.cs` — escolha de alvo.
- `Ultrapowa Clash Server/PacketProcessing/Commands/PlaceAttackerCommand.cs` — remoção de tropas ao colocar atacante.
- `Ultrapowa Clash Server/PacketProcessing/Commands/RemoveUnitsCommand.cs` — remoção de unidades.
- `Ultrapowa Clash Server/Logic/Component/UnitStorageComponent.cs` — conteúdo/capacidade e contadores.
- `Ultrapowa Clash Server/Logic/Component/UnitProductionComponent.cs` — fila e destino da produção.
- `Ultrapowa Clash Server/Core/DatabaseManager.cs` — load/save de jogador e aliança.
- `Ultrapowa Clash Server/Core/ResourcesManager.cs` — desconexão e ciclo de vida.
- `Ultrapowa Clash Server/Logic/ClientAvatar.cs` — score, liga, serialização do avatar, listas NPC.
- `Ultrapowa Clash Server/PacketProcessing/Messages/Server/AvatarProfileMessage.cs` — pacote 24334.
- `Ultrapowa Clash Server/Logic/Alliance.cs` — score do clã e serialização do mural.
- `Ultrapowa Clash Server/PacketProcessing/Messages/Server/AllianceDataMessage.cs` — pacote 24301.
- `Ultrapowa Clash Server/Logic/StreamEntry/StreamEntry.cs` — cargo fixo nas mensagens.
- `Ultrapowa Clash Server/Logic/StreamEntry/AllianceEventStreamEntry.cs` — evento tipo 4.
- `Ultrapowa Clash Server/PacketProcessing/Commands/KickAllianceMemberCommand.cs` — evento de expulsão já implementado.
- `Ultrapowa Clash Server/PacketProcessing/Messages/Client/GoHomeMessage.cs` — retorno de batalha e tutorial.
- `Ultrapowa Clash Server/PacketProcessing/Messages/Server/NpcDataMessage.cs` — dados de campanha NPC.
- `Ultrapowa Clash Server/PacketProcessing/Commands/LoadTurretCommand.cs` — comando de munição ainda sem execução.
- `Ultrapowa Clash Server/Logic/GoogleBindService.cs` e `BindGoogleServiceAccountMessage.cs` — troca/vínculo de contas.

## 10. Resumo final

As correções de promoção, ordem 0-based e clã 8000/nível 1 estão presentes no source e documentadas como validadas pelo usuário em 19/10. **Não devem ser refeitas sem regressão reproduzida.** A liga correta na lista do clã é um caminho separado do campo hardcoded usado pelo serializador do avatar, então a liga vazia no perfil continua aberta.

Os riscos mais altos neste momento são a integridade/persistência do exército e o fluxo PvP incompleto. O 603 está instrumentado, não implementado; a busca pode sortear o próprio atacante e não guarda o oponente; o resultado não atualiza troféus nem score dinâmico do clã. Depois, tratar eventos de clã (com implementação parcial já existente), mapa/progresso NPC, munição, reports/replay, popup e contas múltiplas.

**Ordem sugerida para o próximo ciclo:** estabelecer a linha de base e backup → provar persistência → filtro/registro do oponente → decodificar 603 sem recompensa → reconciliar tropas com migração segura → implementar resultado PvP idempotente → perfil/chat/eventos → campanha/munição/reports → multi-conta com autenticação adequada.

A fonte da verdade para cada correção é o combo de **source + build + processo real + log + DB/pacote + teste no celular**, não o texto de status isolado.
