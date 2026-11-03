# Unificação dos projetos (02/11/2026)

Este repositório (`buritiro-jogo/ExportedProject`) passou a guardar o **projeto-fonte original** do jogo (`unity/FreeBala`,
rodada r53 do amigo do Ruan), com todo o trabalho que o time do Ruan fez no projeto reconstruído pelo AssetRipper/ILSpy
(commits até `052f886`, 31/10–02/11) portado para ele. O histórico antigo do repositório foi mantido: nada foi reescrito
nem enviado com force push. O commit da unificação fica em cima do `052f886`.

## Por que não foi só copiar

- O projeto do parceiro nasceu da build **Release r45 (29/10)**. Isso foi provado decompilando a
  `FreeBala.Runtime.dll` da r45 com o ILSpy (`-lv CSharp9_0`): saem os mesmos 278 arquivos do `Assets/Scripts` do parceiro.
- O fonte original já estava na r53: as rodadas r46–r53 (retarget nativo dos veículos, dano por arma, elenco,
  Vila Jardim por evidência etc.) não existem no projeto do parceiro.
- No projeto do AssetRipper, cada asset ganhou um GUID novo. Por isso cenas, prefabs e materiais dele não abrem no fonte
  (as referências apontam para GUIDs que não existem aqui).

## O que foi feito

1. **Código (145 arquivos .cs):** diff r45 decompilado → parceiro, arquivo por arquivo, separando o ruído do
   decompilador (variáveis renomeadas, `&`/`&&` em bool etc.) das mudanças reais. Só as mudanças reais foram aplicadas
   no nosso fonte, com o comentário `[parceiro <data>]` para achar depois. Arquivos novos do parceiro:
   - `Runtime/Modes/FBModo10x10.cs` (modo 10x10 por rodadas, v14);
   - `Runtime/Core/FBTempo.cs` (medidor de CPU) e `Runtime/Core/FBUnityNullExt.cs`;
   - `Runtime/Anim/FBAnimBur.cs`;
   - `Runtime/Teste/` (`FBTesteExe`, `FBPiloto`, `FBVitrine`, `FBLabFisica`, `FBVigiaSom`);
   - `Editor/Parceiro/` (as ferramentas de Editor dele, com caminhos adaptados).
2. **Pastas de conteúdo do time** copiadas com os `.meta` dele (são autocontidas):
   `Assets/BURITIRO`, `Assets/BURITIRO_Mapas`, `Assets/BURITIRO_Animacoes`, `Assets/BuritiroMascaras`,
   `Assets/BuritiroVJ`. As 23 texturas da Vila Jardim que os materiais polidos usam vieram em
   `Assets/BuritiroVJ/Texturas_r45/`, com os GUIDs dele.
3. **fb_data.json:** merge de 3 vias (base r45 × nosso r53 × parceiro) com `merge3.py`. 0 conflitos: desde a r45 nós
   só mexemos em `skins`, e o parceiro em `maps` (PT, Orla, Centro Cívico, pontos da Vila Jardim, bases do 10x10).
4. **Mapas:** os ids oficiais `praca_pt` e `orla` passam a ser as **arenas novas** do parceiro. Os mapas antigos do amigo
   foram preservados como `Map_praca_pt_antiga` / `Map_orla_antiga` (cenas, NavMesh, reflexo) e
   `praca_pt_antiga` / `orla_antiga` no fb_data (desligados, fora do build). Nada foi apagado.
5. **Cenas novas:** como as cenas dele não abrem aqui, elas foram **remontadas** dentro do fonte com as próprias
   ferramentas dele, na ordem do `RESUMO_PROJETO_BURITIRO.md`, pelo `Editor/Parceiro/FBUnificacao.cs`.
6. **Git:** `.gitignore` de Unity completo e `.gitattributes` com Git LFS para todo binário pesado.
7. **Árvore extraída removida do versionamento:** os ~48 mil arquivos do AssetRipper (`Assets/Scripts`, `Assets/Mesh`,
   `Assets/Resources/fbimported`, `Assets/AnimationClip` etc.) saem do `main` porque o fonte tem tudo isso com os GUIDs
   originais. Eles continuam no histórico (commit `052f886` e anteriores) e no LFS do GitHub; nada foi apagado do disco
   de ninguém.

## Detalhes do que entrou (02/11)

- **Cenas remontadas** (`FBUnificacao.Rodar`, depois `Segunda`/`SoSol`): `Map_praca_pt` (com árvores, esculturas Tripo e
  pisos PBR), `Map_orla`, `Map_centro_civico`. NavMesh de cada uma em `Assets/BURITIRO_Mapas/<id>/`.
- **Sol:** o `BuritiroMontaMapa.CopiaDoModelo` não registrava a luz "Sol" em `RenderSettings.sun` (m_Sun vazio na cena).
  Corrigido na ferramenta e gravado nas 3 cenas.
- **Build Settings:** Menu, Praça das Águas, Vila Jardim, Praça do PT, Orla, Centro Cívico (ligadas) e Estande (desligada),
  igual à lista do parceiro.
- **Ids antigos:** `Map_praca_pt_antiga`/`Map_orla_antiga` gravam `mapId` `praca_pt_antiga`/`orla_antiga` (FBMatch e
  FBWaterRuntime), para não se confundirem com as arenas novas.
- **Miniaturas do menu:** `Resources/MapShots/praca_pt|orla|centro_civico.jpg` são as do parceiro. O `FBImport` passou a
  copiar as miniaturas antigas do jogo web como `<id>_antiga.jpg`, para não sobrescrever as novas numa reimportação.
- **Locomoção armada (CombatLocomotion):** o blend tree do `EnemyAnimator_AAA_relaxed_...controller` ganhou os 5 filhos
  do parceiro (RunRtLoop, RunLtLoop, RunStrafeRight/Left135Loop, Rifle_SprintLoop) e as corridas diagonais foram para
  (±1,7465; 1,7465), total 18 filhos como no dele. O `FBAnimImport.AdaptMain` refaz isso se o controlador for
  regenerado.
- **Camada "BUR Tronco"** (peso 0, máscara `BUR_Tronco`) entrou no índice 2 do controlador. As camadas de emote andaram
  uma posição; o `FBJogoTest` agora procura a camada "Emote Full Body" pelo nome em vez do índice fixo.
- **asmdef do Editor:** referência a `Unity.RenderPipelines.GPUDriven.Runtime` (precisa para o `BuritiroResidentDrawer`).
- **Vila Jardim:** recebeu só as 40 árvores do parceiro (`BuritiroPlantarArvores.PlantarVila`). O polimento v2.2b
  (`BuritiroVilaJardim.Tudo`) **não** foi aplicado: o "Corrigir" foi feito para a VJ do AssetRipper e move objetos (os
  blocos da VJ do fonte já foram validados pela vista aérea e não podem sair do lugar), e o "Polir" deixou a vista 4
  ~23 % mais escura e tirou saturação. A cena polida ficou guardada em `<game>\_backup\unificacao_vj_polida\` para
  comparar antes × depois. Os materiais/malhas do parceiro em `Assets/BuritiroVJ` foram mantidos, mesmo sem uso nas cenas
  do fonte. A VJ polida do próprio parceiro (sobre a VJ da r45) continua no histórico: `git show
  052f886:Assets/FreeBala/Scenes/Map_vila_jardim.unity`. As 1.231 malhas de `BuritiroVJ/Malhas` são da VJ da r45 e não
  encaixam na VJ do fonte; para aproveitar o polimento, rodar só os passos que não movem objetos (Otimizar/LOD, Polir sem
  escurecer) e nunca o "Corrigir".
- **`.gitattributes`:** os `NavMesh_*.asset` de `BURITIRO_Mapas` são binários; ficam marcados `binary` porque a regra
  `*.asset text eol=lf` trocava um `CRLF` interno do NavMesh da PT e corrompia o arquivo.

## Pendências (não feitas de propósito, precisam de decisão ou de teste)

- **Postes e luminárias das arenas novas:** o `FBPolePrep`/`FBLampPrep` procuram nomes do fonte; os postes do parceiro
  (`I_PT_Poste_*`, `I_OR_*`, `I_CC_*`) estão dentro das células de HLOD e não são achados. À noite as arenas novas ficam
  sem postes acesos.
- **Tonemapping Neutral no Mobile/Web** (mudança do parceiro): não aplicado, contraria o `FBProjectSetup`. Decidir com
  fotos antes × depois.
- **`Docs/pendente_02nov/correcao_muro.patch`** (tarefa 1 do `PENDENTE.md`): não aplicado, nem pelo parceiro.
- **NavMesh por dentro dos objetos da `FB_Rua171`** (tarefa 2): esses objetos são criados pelo `FBVestirRua` sem
  `NavMeshObstacle`.
- **Polimento da Vila Jardim:** só com aprovação de fotos antes × depois (ver acima).
- **Reflection probe** das arenas assa sem textura (problema antigo do projeto) e `SoftShadowQuality` segue o perfil de
  qualidade (0).
- A Vila Jardim e a Praça das Águas não têm `arena`/`basePairs` próprios no fb_data além do que o parceiro gravou.
- Android/iOS: o código novo só usa `#if` de plataforma já existentes, mas não houve troca de plataforma nem build
  nesta unificação (a troca reimporta o projeto inteiro).

## Cuidados daqui para frente

- **`node tools/unity/export_data.mjs`** (gerador do fb_data a partir do jogo web) regrava o fb_data **sem** os mapas
  do parceiro. Depois de rodar, refaça o merge:
  `python merge3.py <fb_data da r45> <fb_data novo> <fb_data deste repo> saida.json relatorio.json`.
- **Remontar a PT apaga as árvores e as esculturas:** sempre montar → `BuritiroPlantarArvores.PlantarPT` →
  `BuritiroEsculturasPT.PorSemTirar`. O `FBUnificacao.Rodar` já faz nessa ordem.
- `remap_guids.py`: troca, em assets copiados do projeto do AssetRipper, os GUIDs extraídos pelos GUIDs equivalentes do
  fonte (`Assets/Resources/fbimported/...` → `Assets/FreeBala/Resources/FBImported/...`). Use se precisar trazer mais
  algum asset do projeto antigo.
- Versão do Unity: o fonte está em **6000.6.6f1**; o 6000.6.7f1 do Ruan abre o projeto normalmente (atualização de
  correção).
