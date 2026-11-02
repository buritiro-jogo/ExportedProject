# BURITIRO: guia para o Claude Code

> **Tarefas pendentes para o terminal:** veja `PENDENTE.md` (correção pronta do travamento na ponta de muro, NavMesh passando por dentro de objetos, plano de CPU).
>
> **Resumo completo do projeto:** veja `RESUMO_PROJETO_BURITIRO.md` (decisões do Ruan, o que cada agente já fez, estado atual, bugs e o que falta). Leia antes de começar, para não refazer trabalho.

Jogo de tiro em terceira pessoa que se passa em Boa Vista (RR). Os donos são o Ruan e um amigo.
Este projeto Unity foi reconstruído a partir da build do jogo com o AssetRipper, mais o código C# recuperado com o ILSpy.

- Unity **6000.6.7f1**, URP, Windows 64 bits. Build de teste em Mono, build de entrega em IL2CPP.
- Responda sempre em português simples. O Ruan está em Boa Vista (UTC-4); o relógio do PC está em UTC-3, uma hora na frente.

## Regras do GitHub (obrigatórias)

- O repositório é privado: `buritiro-jogo/ExportedProject`, branch `main`. Os arquivos grandes ficam no Git LFS.
- **Antes de editar, faça `git pull`.** O amigo do Ruan também manda mudanças.
- **Depois de cada passo testado**, faça um commit com mensagem clara em português e depois o `git push`.
- **Nunca use force push** e nunca reescreva o histórico (`rebase`, `reset --hard` em commit já enviado, `commit --amend` em commit já enviado).
- Se der conflito com uma mudança do amigo, pare e pergunte ao Ruan antes de resolver.
- Adicione arquivos por caminho (`git add caminho/arquivo`), nunca `git add -A` ou `git add .`.
- Não faça commit de `Assets/_Recovery/` nem de `Assets/_Recovery.meta`. Essa é a cena de recuperação que o Unity cria depois de travar.
- **Conta:** o repositório usa a conta GitHub **Buritiro**. A configuração local deste repo já aponta para ela (`credential.https://github.com.helper` = `manager`, `credential.https://github.com.username` = `Buritiro`).
  - O `gh` do PC está logado em outra conta (ecoart-rr), que não enxerga este repositório.
  - Se o Git pedir login, quem faz é o Ruan: ele digita o código em github.com/login/device com a conta Buritiro.
  - Nunca digite senha nem mostre token.

## Trabalho em equipe

- Além de você, agentes do Claude na nuvem também editam este projeto pelo PC. A thread "Ajustar física do jogo BURITIRO" aplica pacotes que ficam em `..\..\_patches`.
- Antes de começar:
  - rode `git status` e `git log -5` para ver se há trabalho não commitado ou recente;
  - combine com o Ruan para só um editar o Unity por vez.
- Antes de trocar um arquivo, guarde uma cópia em `..\..\_backup\<nome>_<data>\`.

## Pastas

Dentro do repositório (`C:\Users\User\Documents\Buritiro_Unity\Export\ExportedProject`):

- `Assets/Scripts/FreeBala.Runtime/FreeBala/`: código do jogo. Os principais arquivos são:
  - jogador: `FBPlayer`, `FBCharacter`, `FBRig`, `FBWeaponUser`;
  - bots: `FBBot`, `FBBotSense`, `FBBotBrain`, `FBNav`, `FBWorld`;
  - modo 10x10 e interface: `FBModo10x10`, `FBShop`, `FBEconomy`, `FBHUD`;
  - veículos e dados: `FBDrive`, `FBMxBike`, `FBCarPhys`, `GameData`.
- `Assets/Scripts/FBTeste/`: testes automáticos.
  - `FBTesteExe`: o teste do .exe.
  - `FBPiloto`: o bot que joga no lugar do jogador.
  - `FBVitrine`: fotos 4K.
  - `FBLabFisica` e `FBVigiaSom`.
- `Assets/Scripts/com.cqf.urpvolumetricfog.runtime/`: névoa volumétrica.
- `Assets/Editor/`: ferramentas do Editor.
  - `FBLabRunner`: o laboratório que atende os pedidos do terminal.
  - `BuritiroMontaMapa`: monta os mapas PT, Orla e Centro Cívico.
  - `BuritiroPlantarArvores`, `BuritiroEsculturasPT`, `BuritiroVilaJardim`, `BuritiroResidentDrawer`, `BuritiroMascaraCorpo` e outras.
- `Assets/FreeBala/Scenes/`: cenas do jogo.
- `Assets/BURITIRO_Mapas/` (`praca_pt`, `orla`, `centro_civico`), `Assets/BuritiroVJ/` e `Assets/BURITIRO/`: mapas e recursos de Boa Vista feitos pelo time.
- `Assets/BURITIRO_Animacoes/` e `Assets/BuritiroMascaras/`: animações novas e máscaras de corpo.
- `Assets/StreamingAssets/fb_data.json`: dados do jogo, que o jogo lê toda vez que abre.
  - Contém armas, movimento, veículos, mapas, pontos de nascer e bases do 10x10.
  - Se ele faltar, o jogo usa a cópia de `Assets/Resources/fb_data.json`.
  - Faça backup antes de editar.
- `Assets/Resources/`: prefabs, materiais, áudio e registros (`FBRegistry.asset`), carregados pelo nome.
- `Assets/AnimationClip`, `Material`, `Mesh`, `Texture2D`, `Shader`, `NavMeshData` e afins: o que o AssetRipper extraiu da build original.
- `autoteste/`: prints antigos de teste de moto e carro.
- `Packages/manifest.json`: pacotes (URP, Input System, Animation Rigging, Newtonsoft Json etc.).
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln*`: gerados pelo Unity, não vão para o Git.

Fora do repositório, em `C:\Users\User\Documents\Buritiro_Unity\` (não vão para o GitHub):

- `_diag\`: scripts de teste e de laboratório, listas de teste e relatórios (`_diag\<nome do teste>\relatorio.txt`).
- `_patches\`: pacotes que os agentes da nuvem entregam. Cada pacote tem `aplicar.ps1` (confere o MD5 e faz backup), `desfazer.ps1` e `manifesto.txt`.
- `_backup\`: cópias dos arquivos antes de cada pacote.
- `Builds\`: os .exe de teste (`teste_<data>_mono\BURITIRO.exe`). Mantenha só as 2 builds mais novas.

A pasta `C:\Users\User\Documents\Buritiro_Dev` é a build antiga original. Não mexa nela.

## Compilar, gerar build e testar

**No Unity:** abra o projeto pelo Unity Hub com a versão 6000.6.7f1. Os erros de compilação aparecem no Console.

**Pelo terminal, com o Unity aberto**, um script do Editor lê pedidos de `..\..\_diag\lab_pedido.txt`:

```powershell
powershell -ExecutionPolicy Bypass -File ..\..\_diag\lab_run.ps1 -Pedido refresh -MaxSeg 240   # recompila
powershell -ExecutionPolicy Bypass -File ..\..\_diag\lab_run.ps1 -Pedido salvar -MaxSeg 60     # salva cenas alteradas
powershell -ExecutionPolicy Bypass -File ..\..\_diag\lab_run.ps1 -Pedido build -MaxSeg 1200    # gera Builds\teste_<data>_mono
powershell -ExecutionPolicy Bypass -File ..\..\_diag\lab_run.ps1 -Pedido "metodo Classe.Metodo" -MaxSeg 120
```

- O resultado da compilação fica em `_diag\lab_compilacao.txt` (`erros N`) e o log em `_diag\lab_log.txt`.
- O build é recusado se houver cena com mudança não salva: peça `salvar` antes.

**Teste automático no .exe:**

```
BURITIRO.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile log.txt -fbtesteexe 10x10 <mapa> <rodadas> [time1] [9x9] [camalta] [semculling] [piloto] [mirabot]
BURITIRO.exe ... -fbtesteexe paredes <mapa>
```

- Mapas: `praca_pt`, `orla`, `centro_civico`, `praca_aguas`, `vila_jardim`.
- Opções do teste:
  - `9x9`: o jogador fica parado, para medir o equilíbrio entre os times.
  - `time1`: troca o time do jogador.
  - `piloto`: um bot joga no lugar do jogador, comprando, andando e atirando.
  - `mirabot`: o piloto erra a mira como os bots.
  - `paredes`: procura pontos onde o NavMesh atravessa muro ou passa embaixo de teto baixo.
- O relatório e as fotos ficam em `teste_exe\` ao lado do exe.
- **Vários testes em fila:** `powershell -File ..\..\_diag\lanca_noite.ps1 -Build <pasta da build> -Lista <arquivo em _diag>`.
  - Cada linha da lista tem o formato `nome|argumentos|minutos máximos`.
  - O andamento fica em `_diag\noite_status.txt` e os resultados em `_diag\<nome>\`.

**Cuidados:**
- O jogo pausa quando a janela perde o foco. Com um exe de teste rodando, não foque o Unity, não gere build e não rode outro exe.
- Nunca troque arquivos .cs com o Unity em Play ou no meio de um build.
- A linha de comando do processo do Unity tem um `accessToken`. Nunca mostre nem copie essa linha; filtre por `accessToken|token`.
- No PowerShell 5.1, scripts com acento precisam ser salvos em UTF-8 com BOM.

## Armadilhas conhecidas

- **Água pelo nome do material:** o `FBWater` transforma em água todo material cujo nome pareça água (por exemplo, "agua", "igarape"). Não use essas palavras em material que não é água, como areia ou parede verde.
- **Teste que engasga sozinho:** as fotos e o censo do próprio teste podem aparecer como engasgos de 130 a 170 ms. Antes de culpar o jogo, confira se o engasgo não veio do teste.
- **Física do personagem:** a camada do jogador é a 10. A visão dos bots (`FBWorld.ClearLine`) só enxerga as camadas 0, 8 e 11 (máscara 2305). Uma parede em outra camada bloqueia o jogador, mas os bots enxergam através dela.
- **NavMesh:** o NavMesh é dado pré-calculado (`FBNavData`) mais o recorte, em tempo de jogo, dos muros da arena 10x10 (`FBModo10x10`).

## Estado atual (02/11/2026)

Modo principal: **10x10 por rodadas** com compra, primeiro a 7 vitórias, nos 5 mapas competitivos: Praça do PT, Orla do Rio Branco, Centro Cívico com o Garimpeiro, Praça das Águas e Vila Jardim. Os mapas são arenas de 600 x 600 m recortadas da cidade.

**Último pacote aplicado:** `fisica_02nov_a`, com backup em `..\..\_backup\fisica_02nov_a_20261102_164122`. Ele traz:
- o dinheiro mudou para logo abaixo do minimapa (`FBHUD`, `cashT`);
- a versão v14 do 10x10 (`FBModo10x10`);
- o teste `paredes`;
- a opção `mirabot` do piloto.

**Bugs conhecidos e situação:**

1. **Dinheiro escondido atrás do minimapa:** corrigido em 02/11 e confirmado no print do teste `piloto_hud`.
2. **Jogador (e o piloto) preso no muro da Praça do PT**, em (-240,7, 0,17, 45,8), na ponta do `COL_G_PT_Muro_Grafitado_R_Carmelo`.
   - O muro e o NavMesh estão certos.
   - O problema é como os bots e o piloto seguem o caminho: cortam a ponta do muro com raio fino e o corpo bate.
   - A correção está pronta, mas não foi aplicada: tarefa 1 do `PENDENTE.md`.
3. **O NavMesh passa por dentro de objetos que não abriram buraco nele**, e os bots podem prender ali:
   - pontos de ônibus, orelhões e lixeiras da `FB_Rua171`;
   - o food truck e as mesas da Praça das Águas;
   - o parquinho `PA_parquinho_166`.
   - Lista completa: `..\..\_diag\paredes_<mapa>\relatorio.txt`.
   - Tarefa 2 do `PENDENTE.md`.
4. **Rodada de pistola e efeito bola de neve (v14):**
   - O perdedor da R1 compra arma na R2 e ganhou 2 de 6 partidas (antes, 0 de 11).
   - A R1 ainda acaba no tempo em 5 de 6, porque a pistola mata pouco (10% de acerto).
   - A thread do 10x10 decidiu manter a v14 como está. A letalidade da pistola fica para depois.
   - Resumo em `Docs/pendente_02nov/v14_RESUMO.md`.
5. **Não existe vantagem do time do jogador.** O time dele ganhou 42 de 86 rodadas nos testes. O piloto vence por mirar melhor (acerta 40% contra 8% dos bots).

**Pendente:** veja `PENDENTE.md`.
- Economia de CPU para chegar a 120 FPS: só o plano (`Docs/pendente_02nov/cpu_plan.md`).
- Depois dela: teste A/B do GPU Resident Drawer (`metodo BuritiroResidentDrawer.Liga`), sombras, Graphics Jobs e GPU skinning.
