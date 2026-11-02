# BURITIRO: resumo completo do projeto (até 02/11/2026)

Este arquivo junta tudo o que foi conversado e feito no projeto BURITIRO desde 30/10/2026: as decisões do Ruan, as regras do time, o que cada agente fez e entregou, o estado atual do jogo, os bugs e o que falta.

Serve para o Ruan, para o amigo dele e para qualquer Claude (inclusive o Claude Code do terminal, na outra conta). A ideia é que ninguém refaça trabalho que já existe.

- **Leia junto com:** `CLAUDE.md` (regras de trabalho no repositório) e `PENDENTE.md` (as próximas tarefas técnicas, com passo a passo). Os dois estão na raiz do repositório.
- **Horários:** quando aparecer "hora do PC", é o relógio do PC do Ruan (UTC-3). O Ruan está em Boa Vista (UTC-4), uma hora atrás do PC.
- **Fora do arquivo de propósito:** chaves, senhas, tokens e dados de cartão.

---

## 1. O que é o BURITIRO

- É um jogo de tiro em terceira pessoa que se passa em Boa Vista (RR), com carro e moto. Os donos são o Ruan e um amigo dele. O nome do estúdio no código é "FreeBala".
- O amigo mandou só a build compilada, que fica em `Documents\Buritiro_Dev`: Unity 6000.6.6f1, IL2CPP, sem código-fonte.
- O time reconstruiu o projeto Unity com AssetRipper e recuperou o C# com ILSpy (278 arquivos).
  - Projeto atual: `C:\Users\User\Documents\Buritiro_Unity\Export\ExportedProject`.
  - Abre no Unity **6000.6.7f1** (URP 17.6), com 0 erros.
  - A build de teste sai em Mono; a de entrega deve sair em IL2CPP.
- O objetivo atual é o modo competitivo **10x10 por rodadas**, com compra de armas, em 5 mapas reais de Boa Vista. Depois vem um modo livre para andar de carro pela cidade, "tipo GTA online".

---

## 2. Decisões e preferências do Ruan (não perguntar de novo)

### Jeito de trabalhar
- Responder sempre em português simples. O Ruan muitas vezes escreve em maiúsculas.
- Ele quer que os agentes ajam sozinhos nas escolhas técnicas de rotina, sem pedir aprovação.
- **Aprovação permanente (31/10):** jogabilidade, física do jogo e conserto de bug podem ser feitos sem perguntar ("você tá aprovado pra fazer").
- Ele pede para não parar de trabalhar enquanto dorme. Ele deixa o PC para os agentes e comanda pelo celular.
- Cada mudança tem que ser provada com um .exe de teste e print. Só peça teste ao Ruan depois de mostrar o print funcionando.
- O jogo vem antes de marketing. Se gravar vídeo de antes/depois atrasar o trabalho, não grave (31/10).
- **Acesso ao PC:** os agentes têm acesso total ao PC dele. Prefira as pastas já aprovadas: `Documents\MAPA NOVO`, `Buriti Dev`, `Buritiro_Dev`, `Buritiro` e `Buritiro_Unity`. O app ainda pede a aprovação dele para cada pasta nova, e ele tem dificuldade de achar esses cartões.
- **Dinheiro:**
  - Nada de API paga ou pré-paga. Procure sempre o caminho grátis.
  - Comprar asset precisa do OK dele.
  - Preço da Asset Store no Brasil sai uns 30% mais caro, mais IOF.
- **Chaves:** ele já colou chave de API no chat. Nunca repita, guarde nem use. Se acontecer de novo, peça para ele trocar a chave.
- **Mapa terminado:** nenhum mapa pode ser declarado pronto se faltar estrutura importante ou se houver erro de exportação conhecido.
- **Pedido de LAI das plantas:** em 31/10 ele disse "deixa de lado". Não insistir.

### Decisões de jogo
- **Os 5 mapas competitivos do 10x10 (31/10, 08:15):**
  1. Praça do PT
  2. Orla do Rio Branco
  3. Centro Cívico com o Garimpeiro
  4. Praça das Águas
  5. Vila Jardim

  São arenas de uns 600 × 600 m recortadas da cidade. O resto da cidade aparece só como horizonte leve.
- **Regras do 10x10:**
  - Todo mundo começa junto de um lado, compra arma no começo e ganha dinheiro com abates e rodadas.
  - As bases mudam de lugar a cada rodada, para os times não se encontrarem sempre no meio do mapa.
- **Boa Vista inteira:** o mapa vai ser feito bairro por bairro, todos na mesma grade, e depois juntado.
  - O primeiro bairro é o Centro.
  - Regra do Ruan: focar no Centro (Centro Cívico, Monumento ao Garimpeiro) e só sair dele quando estiver 100%.
  - O Centro completo serve para o modo livre de carro.
- **Praça das Águas:** é do amigo e já está no jogo (mapa `praca_aguas`). O Ruan mandou aproveitar e juntar com o Centro.
- **Tripo 3D (31/10, 20:05):**
  - Não usar para personagens, NPCs, carros, motos ou armas. Esses aproveitam o que o jogo já tem.
  - Usar só em detalhes reais de Boa Vista nos mapas.
  - Nada se gera sem o OK dele sobre os créditos.
- **Unity 7.0 Alpha:** o Ruan quer usar. A recomendação foi continuar no 6000.6 e testar uma **cópia** no 7.0. O amigo continua no 6000.6. Ainda não foi feito.
- **Animações pagas:** a compra do Human Soldier Animations foi **estornada** e o pack não está na conta. O jogo segue com as 82 animações grátis.
- **Limpezas:** o Ruan aprovou apagar arquivos antigos da pasta do projeto (31/10 e 01/11) e as builds de teste antigas, deixando as 2 mais novas.
- **Pausa (02/11):** às 19:51 UTC ele pediu para terminar o que estava em andamento e pausar. Os agentes pararam às 17:46 (hora do PC). Ele vai continuar pelo Claude Code do terminal, em outra conta.

---

## 3. Regras do time

1. **Só a thread "Ajustar física do jogo BURITIRO" edita o Unity no PC e faz commit.**
   - As outras threads entregam em `/mnt/project-files`, e ela aplica.
   - Com a física em pausa, o Claude Code do terminal pode editar. Mas só um por vez, e sempre começando com `git pull`.
2. Gerar um .exe de teste a cada etapa. O módulo IL2CPP é para a build final.
3. Nunca trocar arquivo .cs com o Unity em Play ou durante um build.
4. Com um .exe de teste rodando, não mexer no PC: o jogo pausa quando perde o foco, e chamadas de ferramenta no PC trazem o app do Claude para a frente.
5. Antes de trocar arquivo, guardar backup em `Buritiro_Unity\_backup\<nome>_<data>\`.
6. Os pacotes vêm em `_patches\`, com `aplicar.ps1` (confere o MD5 e faz backup), `desfazer.ps1` e `manifesto.txt`.
7. Manter só as 2 builds de teste mais novas em `Buritiro_Unity\Builds` (cada uma tem uns 12 GB).

---

## 4. GitHub

- **Organização:** `buritiro-jogo`, criada e paga pelo Ruan em 02/11. É do plano GitHub Team, com 2 vagas, US$ 8/mês. Fica na conta GitHub **Buritiro**, não na ecoart-rr.
- **Repositório privado:** `github.com/buritiro-jogo/ExportedProject`, branch `main`.
  - Os arquivos grandes vão pelo Git LFS (uns 13 GB).
  - `Library/`, `Temp/`, `Logs/`, `Builds/`, `*.csproj` e `*.sln` ficam fora.
  - `_diag`, `_patches`, `_backup` e `Builds` ficam em `Buritiro_Unity\`, fora do repositório.
- **Cópia local:** `C:\Users\User\Documents\Buritiro_Unity\Export\ExportedProject`, aberta no GitHub Desktop.
- **Regras:**
  - dar `git pull` antes de mexer, porque o amigo também envia mudanças;
  - depois de cada etapa testada, commit com mensagem clara em português (exemplo: "física: conserta moto presa") e push;
  - nunca usar force push, nunca reescrever o histórico;
  - adicionar arquivo por caminho, nunca `git add -A`;
  - não fazer commit de `Assets/_Recovery*`;
  - se houver conflito com mudança do amigo, parar e perguntar ao Ruan.
- **Login:**
  - O `gh` do PC está logado em **ecoart-rr**, outra conta do Ruan que não enxerga o repositório.
  - A configuração **local** do repositório usa o Git Credential Manager com o usuário `Buritiro`.
  - Quando o Git pede login, quem resolve é o Ruan: ele digita o código em `github.com/login/device` com a conta Buritiro.
  - Agentes nunca digitam senha.
- **Commits marcantes:** eec86291 (inicial), ee90aa77 (pacote fisica_01nov_f), 43d6d9c3 (fisica_02nov_a), 797d8897 (CLAUDE.md) e **c70391ef** (o último, com PENDENTE.md e o CLAUDE.md atualizado). O GitHub está igual ao PC.
- **Convidar o amigo:** ele precisa de conta própria. O Ruan entra como Buritiro em `github.com/orgs/buritiro-jogo/people`, clica em **Invite member** e o amigo usa **Clone repository** no GitHub Desktop dele. Isso ainda não foi confirmado.
- **Scripts úteis:** `_diag\git_push_tenta.ps1` faz fetch e push sem pedir login e se recusa a enviar se o GitHub tiver commits novos.

---

## 5. Onde ficam as coisas

### No PC do Ruan (`C:\Users\User\Documents\`)
| Pasta | O que tem |
|---|---|
| `Buritiro_Dev` | A build original do amigo, que não deve ser mexida. Inclui `CODIGO_CSHARP_BURITIRO.zip`. |
| `Buritiro_Unity\Export\ExportedProject` | O projeto Unity, que é o repositório Git. |
| `Buritiro_Unity\_diag` | Laboratório (`lab_pedido.txt`, `lab_run.ps1`), listas de teste, relatórios e scripts PowerShell. |
| `Buritiro_Unity\_patches`, `_backup`, `Builds` | Pacotes, backups e builds de teste. |
| `Buritiro_Unity\_backup_projeto\ExportedProject_2026-10-30` | Backup completo do projeto (12,4 GB), feito com robocopy. |
| `Buritiro_Unity\LEIA-ME_PROJETO_UNITY.md` | Notas da reconstrução do projeto. |
| `BURITIRO_REFERENCIAS\` | Fotos de referência, logs de captura do Street View, esculturas do Tripo (`PRACA_DO_PT\tripo_modelos` e `esculturas_jogo`) e scripts de render 4K. |
| `MAPA NOVO\_plantas_web\` | Scripts para ler sites da Prefeitura pelo PC. |

### Na pasta compartilhada do projeto (`/mnt/project-files/`, uns 7,1 GB de 10 GB)
| Pasta | O que tem |
|---|---|
| `BURITIRO_JOGO/` | Código decompilado, notas de física, `patch/` (todos os pacotes, de etapa1 a fisica_02nov_a, arenas v4 a v7 e `pendente_02nov`), `modo_10x10/`, `testes/` e provas. |
| `BURITIRO_JOGO/patch/pendente_02nov/` | O que ficou parado na pausa: LEIA-ME, os diffs do muro, `cpu_plan.md` e a cópia do que foi para o repositório. |
| `BURITIRO_MAPAS/` | `PRACA_DO_PT` (v1, v3_detalhada, v4_fotos_visitantes), `MIRANTE_ORLA` (v3 completo, leve, v3_detalhe_ruas), `BOA_VISTA` (base_C, bairros/Centro, _fontes), `props/` (69 itens), `_TRABALHO/` (áreas 1 a 5 do Mirante) e `_fontes_*`. |
| `BURITIRO_UNITY_MAPAS/` | Preparo GLB para Unity: `prepara_unity.py`, `BuritiroMontaMapa.cs` e as partes do Centro Cívico. |
| `BURITIRO_ARENAS_10x10/` | Especificação das 5 arenas (`arenas_10x10.json` é a fonte da verdade), plantas e prévias. |
| `BURITIRO_PLANO_BOA_VISTA/` | Plano da cidade por bairros. |
| `BURITIRO_PLANO_DESEMPENHO/` | Plano de 120 FPS (`PLANO_DESEMPENHO.md`). |
| `BURITIRO_SHADERS/` | Pacote final dos 15 shaders refeitos, já aplicado. |
| `BURITIRO_RECURSOS_GRAFICOS/` e `_2/` | HDRIs, texturas PBR, decals, árvores leves, animações grátis e pisos. |
| `BURITIRO_ANIMACOES_PRONTAS/` | 82 animações grátis `BUR_*`, com scripts de importação. |
| `BURITIRO_VILA_JARDIM/` | Ferramenta de polimento da Vila Jardim. |
| `BURITIRO_PRACA_AGUAS/` | Zip do mapa do amigo exportado. |
| `BURITIRO_TRIPO/` | `GUIA_TRIPO.md`, `ESTUDO_MAPAS_TRIPO.md` e `PLANO_USOS_TRIPO.md`. |
| `BURITIRO_INSTAGRAM/` | Carrosséis, stories, reels, `LEGENDAS.md` e `INVESTIDOR/` (pacote de fotos 4K). |
| `BURITIRO_PLANTAS/` | O que se achou de plantas e editais, mais o rascunho de LAI. |
| `PLANO_MAPEAMENTO.md`, `MAPA_PONTOS_CAPTURA.html` | Plano das capturas do Street View. |
| `visitantes_*.zip` | Fotos de visitantes, usadas como referência. Não apagar. |

---

## 6. Estado atual do jogo (02/11/2026)

### Mapas
| Mapa | Situação no jogo |
|---|---|
| **Praça do PT** (`praca_pt`) | Arena v7 montada. Tem 40 árvores plantadas, as 6 esculturas novas do Tripo (onça, 2 filhotes, jacaré, papagaio e lagarto) e o piso do parquinho em PBR. Roda a 95–165 FPS no 10x10. Bug conhecido: a ponta do muro grafitado da R. Carmelo (ver bugs). |
| **Orla do Rio Branco** (`orla`) | Arena v7 montada, com Mirante, Selvinha, praia e passarela sobre o canal. É a mais pesada: no LOD0 são 2.969 instâncias nas células, 2.405 delas árvores. Espera o teste A/B do GPU Resident Drawer; se não bastar, o plano B é uma v8 que junta árvores por célula. |
| **Centro Cívico** (`centro_civico`) | Arena v7 montada, com Garimpeiro, Palácio Hélio Campos, Catedral, Assembleia, TJ e Palácio da Cultura feitos a partir de fotos. Tem horizonte de 3 km. |
| **Praça das Águas** (`praca_aguas`) | No 10x10 usa o mapa original do amigo. A versão convertida já está nos blocos do Centro (-3_3, -3_4, -2_3, -2_4), mas a arena de 600 m ainda **não** foi recortada desses blocos. |
| **Vila Jardim** (`vila_jardim`) | Mapa do amigo, polido (v2.2b): PBR, luz, névoa e otimização dos móveis internos. Roda a uns 83 FPS no 10x10, mas ainda tem uns 25 mil draw calls (a PT tem 3,3 mil). |
| **Boa Vista inteira** | Feita no Blender, mas **não** está no Unity. A base C da cidade toda está pronta, com 195 superblocos e relevo. O Centro tem 25 blocos v2, com ruas de mão única, 3,7 mil construções e marcos nível A. É a base do modo livre de carro, que ainda não foi montado no jogo. |

Os mapas ficam em `Assets/BURITIRO_Mapas/` e são montados pelo menu `BuritiroEditor.BuritiroMontaMapa.Monta<PT|Orla|CentroCivico>Auto`.

**Depois de montar a PT de novo, sempre:**
1. `BuritiroPlantarArvores.PlantarPT`
2. `BuritiroEsculturasPT.PorSemTirar`, que também refaz o NavMesh
3. `salvar`

Montar de novo apaga as árvores e as esculturas. Depois de montar qualquer mapa, junte os `*_fb_data_trecho.json` no `StreamingAssets/fb_data.json`.

### Modo 10x10 (versão v14)
- Arquivo: `FBModo10x10.cs`. A cópia de referência está em `BURITIRO_JOGO/modo_10x10/`, mas a versão que vale é a do repositório.
- Regras: 9 bots + o jogador contra 10 bots. Fase de compra de 20 s, rodada de 135 s e ganha quem fizer 7 primeiro.
- Economia: vitória dá +2500; derrota dá 1400 +500 por derrota seguida, até 3400; o teto é 8000.
- Quando o tempo acaba, ganha o time com mais gente viva. Se empatar, ganha quem tiver mais vida somada.
- As bases são sorteadas a cada rodada (`basePairs` validados no NavMesh).
- O time compra junto pela mediana do dinheiro: COMPLETA, FORCADA ou ECO.
- Na v14, os bots começam a caçar aos 35 s da R1, e o time que perde a R1 faz compra forçada na R2.
- **Resultado dos testes:**
  - Não existe vantagem para o time do jogador: ele ganhou 42 de 86 rodadas.
  - O perdedor da R1 agora ganha a R2 em 2 de 6 partidas; antes, 0 de 11.
  - A R1 ainda acaba no tempo em 5 de 6, porque a pistola mata pouco: 10% de acerto, e a primeira morte acontece aos 62 s.
  - **Decisão:** a v14 fica como está, e a letalidade da pistola fica para depois.

### Física, bots e jogador
- Tudo isso foi corrigido e aplicado entre 31/10 e 02/11:
  - **Arma:** a arma dentro do peito foi corrigida (FBRig v17: cabeça e fuzil baixo ao correr).
  - **Bots:** os bots deslizando foram corrigidos (`BuritiroMascaraCorpo`). Os bots presos em quadras e em ilhas do NavMesh foram corrigidos com a âncora do NavMesh, o vigia de progresso, o `DestinoSeguro` e a realocação escondida.
  - **Bugs gerais:** 21 bugs no jogador, nos bots, no carro, na moto, nas rodadas e no tiro.
  - **Água:** a água certa na Orla e no Centro (filtro de nomes no `FBWater`).
  - **Pássaros:** os pássaros com corte por campo de visão.
  - **Loja:** a tecla B volta a fechar a loja.
  - **HUD:** o dinheiro agora aparece abaixo do minimapa, confirmado em print.
- **Bot piloto** (`FBPiloto`): é um bot que joga no lugar do jogador. Ele compra, anda e atira, e registra onde ficou preso, sem arma ou com a câmera na parede.
  - Opções do teste: `piloto` e `mirabot` (com `mirabot`, o piloto erra a mira como os bots).
  - Ele achou os 3 bugs de 02/11.
- **Teste automático do .exe** (`FBTesteExe` v18):
  - Uso: `-fbtesteexe 10x10 <mapa> <rodadas> [time1] [9x9] [camalta] [semculling] [piloto] [mirabot]`.
  - Também tem os modos `arma`, `perf`, `medir`, `paredes <mapa>` e `videoarma`.
  - Para rodar vários testes em fila, use `_diag\lanca_noite.ps1` ou `noite_testes.ps1`.
- **Último pacote aplicado:** `fisica_02nov_a`, em 02/11 às 16:41 (hora do PC). Build: `Builds\teste_2026-11-02_1641_mono`.

### Gráfico
- **Céu branco e brilho estourado:** a causa era que o AssetRipper deixou 15 shaders vazios. Todos foram refeitos e aplicados (`BURITIRO_SHADERS`), e o céu v2b corrige o roxo no URP 17.6.
  - Ainda não foi conferido de perto: a água do rio, a aura de spawn e a fonte.
- **Tela preta na partida:** era a névoa volumétrica com shader vazio. Foi resolvido no `FBGfx.cs`.
- **Árvores:** 3 árvores leves (do pack de recursos 2) foram importadas e plantadas: 40 na Vila Jardim e 40 na PT.
- **Modo foto 4K** (`FBVitrine` v3b): rendeu 91 fotos de dentro do jogo para o investidor. A foto de vitória é **encenada**, e isso precisa ser dito quando ela for mostrada.

### Animações
- As 82 animações grátis `BUR_*` (Kevin Iglesias e Quaternius, CC0) estão no projeto. A camada "BUR Tronco" fica com peso 0, a não ser que seja ligada com `-fbburanim` ou `burAnim.ligado` no `fb_data`.
- O que ainda falta:
  - entrar e sair de carro e moto (por enquanto só existe um gesto provisório);
  - empinar a moto;
  - garupa;
  - nadar;
  - braços em primeira pessoa;
  - ao andar com o fuzil, os braços ainda ficam esticados.

### Desempenho (meta de 120 FPS, ou 8,3 ms por quadro)
- No PC do Ruan (RTX 3080, Ryzen 7 5700X), o jogo está limitado pela **CPU**, principalmente pelo envio de render (draw calls e sombras). Os bots e as animações não são o problema.
- PT e Centro já ficam em 4 a 8 ms de GPU.
- Ainda falta aplicar:
  - o plano de CPU (`cpu_plan.md`);
  - o teste A/B do GPU Resident Drawer (`BuritiroResidentDrawer.Liga`, que está em `patch/resident_drawer`);
  - os ajustes de sombras, Graphics Jobs e GPU skinning;
  - juntar objetos na Vila Jardim.

---

## 7. O que cada thread fez

Os nomes abaixo são os das threads do projeto. Entre parênteses, a situação de cada uma em 02/11.

### Mapas no Blender (todas encerradas)
- **Reconstruir Praça do PT e Mirante:** fez a primeira versão dos 2 mapas como lugares reais, em GLB e FBX, com origens fixas.
  - PT: lat 2.807062, lon -60.7412694.
  - Mirante/Orla: lat 2.8113, lon -60.6705, que também é a origem da cidade.
  - Entrega em `BURITIRO_MAPAS/`, mais o relatório em https://claude.ai/artifact/NLEdb8FDm2WP8msfuAGpMy.
- **Capturar referências Street View dos mapas:** tirou mais de 3.700 capturas pelo Chrome do PC do Ruan, num perfil separado com depuração remota. O plano está em `PLANO_MAPEAMENTO.md`. As imagens do Google são só referência e nunca entram em entrega.
- **Detalhar torre do Mirante Edileusa Lóz:** fez a torre com 120 m, observatório a 93 m, elevador de vidro na face S, 6 varandas e passarela estaiada de 16 m. Entrega em `_TRABALHO/area1_torre`.
- **Detalhar Orla, praia e Selvinha** e **Detalhar Orla e Selvinha com precisão:** área 2, versão v3.1, em `_TRABALHO/area2_orla_v3_1`.
  - O chão foi aprofundado e o rio fica a -8,6 m.
  - A Selvinha ganhou os animais e a árvore de 15 m com escorregadores, o igarapé ganhou 28 buritis e a areia ficou bege.
- **Detalhar ruas e fachadas do Mirante:** área 3, com 1.436 prédios do Overture, a Igreja Matriz e o Terminal.
- **Detalhar Mirante e ruas com precisão:** área 5 (`MIRANTE_ORLA/v3_detalhe_ruas`). Fez muros, portões, fios, sujeira e buracos no asfalto, e refez a Igreja Matriz a partir de 107 fotos.
- **Finalizar e montar mapa do Mirante:** área 4 (vegetação, postes e entorno) e a junção final, em `MIRANTE_ORLA/mirante_orla.glb` (10,2 milhões de triângulos) e `_leve`. O passo a passo está em `pipeline.sh`.
- **Refinar Praça do PT com fotos novas:** o palco branco com marquise, o piso de bloquete bege e os contêineres no lugar real.
- **Detalhar Praça do PT com precisão:** fez a v3, com 276 casas, lojas, muros, placas e carros, e depois a v4 a partir das fotos de visitantes (onça, filhotes, parquinhos e esculturas).
  - Ainda faltam o píer e o anfiteatro, porque nenhuma foto mostra onde eles ficam.
- **Criar props e vegetação de Boa Vista:** uma biblioteca de 69 itens com LOD (`BURITIRO_MAPAS/props`).
- **Buscar plantas oficiais das obras:** nenhuma planta foi publicada na internet. Os achados ficaram em `BURITIRO_PLANTAS`. **Não pesquisar de novo.**

### Cidade e arenas
- **Construir mapa de Boa Vista por bairros** (concluída): fez o plano, a base C da cidade com relevo e o Centro v2 (25 blocos), além da conferência das ruas com o satélite e do horizonte de 3 km para as arenas.
  - Grade: células de 48 m e blocos de 384 m (`BV_<i>_<j>`).
  - Ofereceu começar o próximo bairro "se o Ruan aprovar". Pela regra do Ruan, isso só acontece quando o Centro estiver 100%.
- **Modelar Centro Cívico e Monumento ao Garimpeiro** (concluída): fez o nível A dos marcos e converteu a Praça das Águas do amigo (645 peças) para dentro do Centro.
- **Planejar mapas competitivos 10x10** (concluída): fez a especificação das 5 arenas e o plano de desempenho.
- **Preparar mapa do Mirante para Unity** (aberta, esperando a física): fez o `prepara_unity.py` (cortes, LOD, colisão, spawns, rotas e `basePairs`) e as arenas v4 a v7.
  - Está esperando a medição da Orla com o Resident Drawer, para decidir se faz uma v8.

### Jogo no Unity
- **Ajustar física do jogo BURITIRO** (pausada): é a única thread que edita o Unity.
  - Reconstruiu o projeto e consertou o null do Editor, a tela preta, os shaders, a pegada da arma, os bots deslizando e presos e os 21 bugs.
  - Montou as arenas, plantou as árvores, colocou as esculturas e fez o piloto, o laboratório, o teste do .exe, o GitHub, o CLAUDE.md e o PENDENTE.md.
- **Criar modo 10x10 com rounds e compra** (concluída, v14): versões v1 a v14, bases sorteadas, compra por time e análise de equilíbrio.
- **Polir mapa Vila Jardim com realismo** (concluída): fez a ferramenta `BuritiroVilaJardim.cs` e as versões v2.2b.
- **Recuperar shaders perdidos do jogo** e **Juntar recursos para melhorar o gráfico** (concluídas): os 15 shaders, os pacotes de recursos 1 e 2 e o guia de URP.
- **Pesquisar pack de animações para o jogo** (concluída): o relatório está em https://claude.ai/artifact/5xM58BhWBH6zQ7znBRbdk9.
- **Preparar animações para o personagem** (concluída): as 82 animações grátis.
- **Liberar espaço na pasta do projeto** (concluída): a pasta foi de 9,4 para 7,0 GB. Cada pasta afetada tem um `APAGADO_LEIA.txt`.
- **Colocar BURITIRO no GitHub Desktop** (concluída): criou a organização, o repositório e o LFS, e fez o primeiro envio.

### Marketing e Tripo
- **Criar fotos e vídeos para Instagram** (concluída): 6 carrosséis, stories, reels e legendas em `BURITIRO_INSTAGRAM`, mais o pacote do investidor em `INVESTIDOR` (50 imagens e 91 fotos 4K de dentro do jogo).
  - Ela também operou o Tripo no PC e fez as 6 esculturas da PT.
  - O Tripo da Selvinha ficou **pausado**, com uns 1285 créditos sobrando.
- **Estudar o Tripo 3D a fundo** (esperando revisão): o guia de custos e o estudo dos mapas estão em `BURITIRO_TRIPO`.
  - O Ruan aprovou uns 610 créditos para o Garimpeiro e as 14 peças da Selvinha, mas a geração foi pausada.
  - Antes de gastar crédito, confira o que já foi gerado.

---

## 8. Bugs conhecidos (02/11)

1. **O piloto (e possivelmente os bots) trava na ponta do muro grafitado da R. Carmelo, na PT**, em (-240,7; 0,17; 45,8).
   - O muro e o NavMesh estão certos. O problema é que o bot corta o canto com raio fino e a cápsula bate.
   - A correção já está pronta e aplica sem erro, mas **não foi testada**: tarefa 1 do `PENDENTE.md`.
2. **O NavMesh passa por dentro de objetos sem buraco**:
   - pontos de ônibus, orelhões e lixeiras da `FB_Rua171`;
   - o food truck, as mesas e o ônibus da Praça das Águas;
   - o parquinho `PA_parquinho_166`.

   Ainda não tem correção, só a ideia: tarefa 2 do `PENDENTE.md`.
3. **A rodada de pistola (R1) acaba no tempo em 5 de 6 partidas**, porque a pistola mata pouco.
   - Fica para depois.
   - É jogabilidade, então está dentro da aprovação permanente.
4. **Desempenho abaixo de 120 FPS**: a Orla fica em uns 82–99 FPS e a Vila Jardim tem draw calls demais (ver a seção 6).
5. **Coisas antigas que precisam ser conferidas antes de declarar resolvidas:**
   - a Barrett e o RPG ainda entravam um pouco no corpo ao correr (31/10);
   - o texto do menu aparecia como "RODADAS Â· COMPRA" (`FBMenu.cs`, linha 22, 01/11);
   - 2 bots ficavam na borda oeste da PT em (-299,7; 16,3) no par de bases 5 (01/11);
   - o teleporte antigo de stuckN 10 disparava à toa no Centro (01/11).

   Algumas dessas podem ter sido corrigidas nos pacotes de 01/11 e 02/11.
6. **Os GLBs antigos da PT** (`praca_do_pt.glb` e `praca_do_pt_v3.glb`) têm só 1 imagem para todas as texturas. Use a v4 ou o FBX.

---

## 9. O que falta, por ordem de prioridade

1. **Aplicar e testar a correção do muro.** É a tarefa 1 do `PENDENTE.md`, e o teste A/B (com e sem `cantofino`) já está descrito lá.
2. **Abrir buraco no NavMesh dos objetos atravessados.** É a tarefa 2 do `PENDENTE.md`.
3. **Levar o jogo a 120 FPS:**
   - aplicar o plano de CPU (`cpu_plan.md`, grupo 1 e depois o grupo 2), junto com a tarefa 1, porque os dois mexem em `FBBot` e `FBWorld`;
   - fazer o A/B do GPU Resident Drawer na Orla;
   - ajustar sombras, Graphics Jobs e GPU skinning;
   - se não bastar, pedir a v8 da Orla e juntar objetos na Vila Jardim.
4. **Aumentar a letalidade da pistola na R1** (dano ou mira dos bots) e testar de novo com `10x10 <mapa> 13 9x9`. Antes de concluir qualquer coisa, tabele todos os relatórios e compare com os da v14.
5. **Teste do Ruan:** mandar a build nova com prints, para ele jogar o 10x10.
6. **Convidar o amigo no GitHub** (ação do Ruan, ver a seção 4).
7. **Fechar o Centro 100%:**
   - recortar a arena da Praça das Águas a partir dos blocos do Centro, se o Ruan quiser trocar o mapa antigo do amigo;
   - montar o Centro completo (modo livre de carro) no Unity, com carregamento por blocos.
8. **Tripo:** retomar o Garimpeiro e a Selvinha só depois de conferir o que já foi gerado. O OK de uns 610 créditos já foi dado.
9. **Animações:** entrar e sair de carro e moto, e os braços ao andar com o fuzil.
10. **Detalhes que ainda faltam nos mapas:**
    - o píer e o anfiteatro da PT, que só podem ser feitos se aparecerem fotos;
    - a Av. Nazaré Filgueiras, que não tem fotos;
    - o interior e o topo do Mirante, que hoje são aproximados.
11. **Instagram:** refazer as fotos da PT com as esculturas novas, quando der tempo.
12. **Outros bairros de Boa Vista:** só depois do Centro 100%.
13. **Unity 7.0 Alpha:** testar só numa cópia do projeto.

---

## 10. Armadilhas técnicas (já custaram tempo)

- **Água pelo nome do material:** o `FBWater` transforma em água todo material com "agua", "rio", "piscina" ou "lagoa" no nome.
  - Não use essas palavras em areia, parede ou tinta.
  - Nomes de água que funcionam: `A2_agua_rio_branco`, `A2_agua_lagoa`, `A2_agua_igarape`, `MT_agua_fonte` e `BV_C_Agua`.
- **Coordenadas:**
  - Do GLB da arena para o Unity, o x troca de sinal: (-x, y, z).
  - No `fb_data`, o x fica negado e o yaw é -yawUnity em radianos.
  - A escala antiga do jogo era 1,2 vez o real; a cidade nova usa a escala real, 1,0.
- **NavMesh:** `FBNav.Path` aceita caminho parcial, então use caminho completo nos dois sentidos para validar bases. A âncora tem que ser posta no Setup do 10x10.
- **Visão dos bots:** os bots enxergam só as camadas 0, 8 e 11 (máscara 2305). O jogador fica na camada 10.
- **Editor:** a primeira renderização logo depois de abrir uma cena perde o chão. Isso só acontece no laboratório; renderize 2 vezes.
- **Unity Editor:** o padrão do código decompilado `GetComponent<T>() ?? AddComponent` falha por causa do null falso do Unity. Use `OrNull()`.
- **Builds:** o build é recusado se houver cena com mudança não salva. Peça `salvar` antes.
- **Testes:**
  - As fotos e o censo do próprio teste aparecem como engasgos falsos de 130 a 170 ms. Confira o teste antes de culpar o jogo.
  - Antes de afirmar viés ou bug de equilíbrio, tabele **todos** os relatórios. Já houve um "17 de 17" errado.
- **Blender (bpy 5.1.2, sem tela):**
  - FBX com Apply Transform sai em pé no Unity; o `exporta.py` corrige isso.
  - O FBX perde as tintas, então asse a cor na textura.
  - Textura grande repetida vira grade quando vista de longe.
  - Para decimar, use uma cena vazia temporária e remova essa cena antes de exportar, senão o glTFast trava.
  - Palmeira: decimar o tronco separado da copa, e conferir com `verifica_palmeiras_v2.py`.
- **Comandos:** nunca coloque um padrão de `pkill -f` ou `pgrep -f` no mesmo comando que contém esse padrão, porque o shell se mata (saída 144).
- **Pasta compartilhada:**
  - Ela muda o MD5 de JPG e PNG, porque acrescenta metadados. Mande imagens dentro de .zip.
  - Arquivos acima de uns 500 MB somem.
  - `device_commit_files` aceita no máximo 30 MB por arquivo, então mande GLBs em partes de 24 MB, com MD5.
- **PowerShell 5.1:**
  - Script com acento precisa de UTF-8 com BOM.
  - Ele descarta argumento vazio, então use `cmd /c`.
  - O desktop-commander estraga `$var` em PowerShell inline: escreva um .ps1 e rode com `-File`.
- **Segurança:** a linha de comando do processo do Unity tem um `accessToken`. Nunca mostre essa linha.
- **Rede da nuvem:**
  - Bloqueados: Google Maps e Street View, OSM, sites da Prefeitura e downloads em zip do GitHub.
  - Funcionam: Overture Maps no S3 (release 2026-10-21), Sentinel-2, Poly Haven, ambientCG e itch.io.
  - Os sites bloqueados podem ser acessados pelo PC do Ruan.

---

## 11. Pesquisas já feitas (não refazer)

- **Plantas oficiais:** não existem online (Prefeitura, PNCP, Transparência e TCE já foram conferidos). A única via é a LAI, que o Ruan deixou de lado.
- **Street View:**
  - A API do Google é paga, e o Ruan recusou.
  - O caminho que funcionou foi o Chrome do PC com perfil separado e `Emulation.setFocusEmulationEnabled`.
- **Medidas reais:** as do Mirante, da Orla, da PT e do Centro Cívico estão nas notas da memória do projeto (`buritiro-detalhes-reais`, `buritiro-centro-civico-nivel-a` e `buritiro-pt-refino-fotos`) e nos LEIA-ME de cada entrega.
- **Packs de animação:** os recomendados eram Human Soldier (Kevin Iglesias), Car e Motorcycle Interaction (SpectraMotions), Mixamo e Quaternius. Nenhum pack barato cobre empinar a moto, garupa, tiro na moto ou braços em primeira pessoa.
- **Árvores e palmeiras grátis:** não existe palmeira, buriti, mangueira ou ipê grátis que seja realista e leve. O Super Palm Pack Mobile precisa do login do Ruan na Asset Store.
- **Tripo:** os custos, as regras de foto e a receita estão em `BURITIRO_TRIPO/GUIA_TRIPO.md`. Antes de cada clique, confira o preço que aparece no botão amarelo.
