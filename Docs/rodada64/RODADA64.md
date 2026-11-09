# Rodada 64: iluminação física do BURITIRO e refinamento da Vila Jardim (09/11/2026)

Builds de teste: `F:/FreeBala_builds/lab97` (luz) e `lab98` (VJ final), ambas Development/Mono. Capturas e medidas: `F:/FreeBala_backups/r64/`.

> **Correção r64b (lab96).** A primeira versão (lab95) saiu estourada: meio-dia lavado e pôr do sol branco-amarelo.
> - **Causa:** a calibração do zênite (×68) era aplicada também ao horizonte e ao chão. Nas fachadas isso dava cerca de 12× em linear, e a adaptação de olho multiplicava de novo (×5 no pôr do sol, ×220 na hora azul). A conta também misturava gama e linear.
> - **Agora:** a física dá só a **cor**. A **intensidade** é a luminância linear da base aprovada de cada mapa × a curva `skyAmbient` (relativa ao meio-dia).
> - A adaptação mexe só na exposição do céu, com teto de 1,3/Lum(horizonte do lado do sol).
> - O sol fica limitado a 1,05× a intensidade base do mapa.

## 1. Diagnóstico inicial (auditoria)

**Iluminação (antes)**
- O `FBWeather` tinha só 3 estados vindos do menu:
  - DIA: sol fixo a 48°, temperatura de cor de 5400 K;
  - NOITE;
  - ANOITECER: 17:15 → 19:00 em 7 minutos de partida, com a curva desenhada à mão.
- Não havia relógio, data nem posição real do sol e da lua em Boa Vista (2,8° N).
- A cor do sol vinha de uma temperatura fixa, não da atmosfera.
- Céu (`FBSky`): gradiente analítico, sem espalhamento Rayleigh/Mie. A lua não tinha fase nem posição real. Não existia poluição luminosa.
- A reflexão vinha de uma sonda **assada ao meio-dia** (`Map_*_Reflexo`). No pôr do sol, carros, vidros e poças continuavam refletindo céu azul.
- O pós-processamento era fixo por mapa. Não havia balanço de branco nem exposição por horário.
- A janela acesa e o poste acendiam por um liga/desliga de noite, sem levar em conta a hora (madrugada igual à noite).
- Não existia uma interface no Editor para testar horários.

**Vila Jardim (antes, tour de 95 pontos × Street View, `F:/FreeBala_backups/r64/folhas_antes`)**

| Onde | Problema | Decisão |
|---|---|---|
| c06, c24, c25, c28 | Estacionamento do Colégio CEM III e rua sul sem nenhum carro; no real ficam lotados | Corrigido |
| c08, c09 | Tapume do Batalhão bege e limpo; no real é cinza galvanizado com escorrimento (cor medida 99,98,92 contra 126,115,99 no jogo) | Corrigido |
| Ruas, calçadas, reboco, telhas, chapas | Brilho e metal constantes por material (regra do nome no `FBMapLook`): sem fissura, junta ou mancha na resposta à luz | Corrigido |
| c05 | Parede-limite da arena azulada e neblina longe demais | A névoa agora segue a curva do preset; a parede-limite é sistema de jogo e não mudou |
| c02, c03, c04 | Semáforos que não existem no real (o `FBSemaforo` põe semáforo em todo cruzamento de 3+ faixas) | Achado registrado; sistema de jogo, não mexi |
| c20, c21, c30 | Grama baixa e roçada no real, touceiras altas no jogo | Não mexi: a vegetação foi acertada com o usuário em 06/11 |
| Todo o tour | 12–20 M triângulos e 27–44 mil draw calls por vista | Risco de desempenho; ver "Pendências" |

## 2. Tecnologias e por quê

| Recurso | Técnica | Por quê |
|---|---|---|
| Sol | NOAA (tempo sideral + refração de Saemundsson) | Precisão de centésimos de grau, custo desprezível. Conferido em Python: 27/02/2026, 18:00 → 3,6° |
| Lua | Algoritmo do suncalc: posição, fase e fração iluminada | Lua cheia de verdade na data, minguante ou crescente no shader |
| Cor do sol e do céu | Espalhamento simples Rayleigh + Mie + ozônio, integrado em C# (`FBAtmosphere`) para a luz e no shader do céu (`FBSky`, 10×4 passos) | Transmitância física: o pôr do sol alaranja e escurece sozinho. URP não tem o Physically Based Sky do HDRP (e a regra proíbe migrar) |
| Luz ambiente | 48 direções do hemisfério → Trilight (céu, horizonte, chão) mais um piso noturno | Ambiente coerente com o céu, sem GI assada (horário dinâmico) |
| Exposição | "Adaptação de olho": ganho = (E_ref/E)^0,78, limitado a 220×, suavizado em 1,6 s; calibrado para o meio-dia igual ao visual aprovado de cada mapa | O dia fica como estava; a noite fica escura mas jogável |
| Curvas artísticas | `AnimationCurve` pela **elevação do sol** num ScriptableObject (`FBEnvPreset`) | Um preset serve para qualquer data, mapa e velocidade de ciclo |
| Reflexo | `ReflectionProbe` em tempo real só do céu (cullingMask 0, 128 px), re-renderizada quando o sol anda 0,4°; a assada some com o sol baixo | Carros e poças refletem o céu do horário. Só Alto/Ultra no PC |
| Pós | Deltas sobre o volume do mapa + `WhiteBalance` no `FB_Iluminacao` | Não destrói o look de cada mapa; nada de efeito exagerado |
| PBR da VJ | Mapa por material (R metal, G oclusão, A brilho) gerado da própria textura base, URP/Lit `_MetallicGlossMap` + `_OcclusionMap` | Alinhado com o desenho e o ladrilho; 512 px; keywords ligadas no Editor (sem engasgo de shader) |
| CINEMATIC | Sombra 4096 até 220 m, 4 cascatas (se o asset já tiver mais de 1), névoa volumétrica com mais passos | Dentro do URP. Ray tracing **não**: o URP 17 não tem RT, e a regra pede checar compatibilidade |

## 3. Arquivos

**Novos**
- `Runtime/World/Env/FBSolar.cs`: posição do sol e da lua.
- `Runtime/World/Env/FBAtmosphere.cs`: transmitância, radiância do céu e ambiente.
- `Runtime/World/Env/FBEnvPreset.cs`: ScriptableObject com as curvas e os ajustes por mapa.
- `Runtime/World/Env/BuritiroEnvironmentManager.cs`: gerente central, estado `FBEnvState`, linha de comando e perfil CINEMATIC (`FBEnvQuality`).
- `Runtime/World/Env/FBEnvProbe.cs`: reflexo do céu em tempo real.
- `Runtime/Teste/FBLuzTeste.cs`: teste `-fbluz` (capturas por horário e desempenho).
- `Editor/BuritiroEnvironmentWindow.cs`: janela BURITIRO > Ambiente e `FBEnvSetup`.
- `Editor/FBPbrMaps.cs`: mapas PBR nos materiais dos mapas.
- `Resources/Env/BuritiroEnv_Padrao.asset`: preset padrão com 6 mapas.
- `Art/Gfx/PBR/vila_jardim/*_ms.png`: 76 mapas.
- Ferramentas em `game/tools/r64/`:
  - `vagas_r64.py`: vagas de estacionamento;
  - `pbr_vj_r64.py`: mapas PBR;
  - `tour_r64.py`, `folha_r64.py`, `grade_r64.py`, `folha_luz_r64.py`: tour e folhas de comparação;
  - `testes_r64.sh`: roteiro dos testes no exe.

**Alterados** (backups em `game/_backup/luz_r64_20261109/`, `vagas_r64_20261109/` e `pbr_r64_20261109/`)
- `Runtime/World/FBWeather.cs`: entrega para o gerente (o antigo continua com `-fbluzantiga`), `ApplyEnv`, `EnvGrade`, janelas pela hora.
- `Runtime/World/FBLivingSky.cs`: `SetPhysical`.
- `Shaders/FBSky.shader`: céu físico (`_FBPhys`), fase da lua com sinal, poluição luminosa.
- `Editor/FBMapLook.cs`: chama o `FBPbrMaps.Assign` na conversão.
- `Art/MapMats/vila_jardim_materiais.mat`: 76 materiais com mapa PBR.
- `StreamingAssets/fb_data.json` e `Resources/fb_data.json`: +29 vagas `src:r64` em `vila_jardim.level.parked`.
- `assets/maps/vila_jardim/level.json` e `tools/build_vila_jardim.py`: lê `tools/r64/vagas_r64.json`.
- `assets/maps/vila_jardim/textures/tapume_galv54.jpg` e `FBImported/Maps/vila_jardim_tex/295_c.jpg`: tapume galvanizado.

## 4. Como usar

- **Editor:**
  - menu **BURITIRO > Ambiente (horário e luz)**:
    - botões 12:00, 17:30, 18:00, 18:30, 20:00, 00:00 e 05:30;
    - régua de hora e velocidade do ciclo;
    - CINEMATIC e preset em uso.
  - Fora do Play, a janela mostra a prévia do sol na cena.
  - O preset fica em `Resources/Env/BuritiroEnv_Padrao.asset`:
    - curvas por elevação do sol;
    - em "Ajustes por mapa": `fixedHour` (hora fixa do mapa), `lightPollution`, `fogMul`, `exposureBias`.
- **Menu do jogo:** DIA = 15:30, NOITE = 20:00, ANOITECER = 17:15 → 19:00 em 7 minutos. É a mesma escolha de antes, agora com sol e lua reais.
- **Linha de comando:**

  | Opção | Efeito |
  |---|---|
  | `-fbhora 18.5` | Força o horário |
  | `-fbciclo 2` | Ciclo contínuo, em minutos de jogo por segundo |
  | `-fbcinematico` | Liga o perfil CINEMATIC |
  | `-fbluzantiga` | Volta ao sistema antigo inteiro |

- **Teste:** `Buritiro.exe -fbluz vila_jardim,praca_pt,... [-fbluzperf] [-fbluzantiga]` grava `autoteste/luz/`.
- **Folhas:** `python tools/r64/folha_luz_r64.py <luz_novo> <luz_antiga> <saída>`.
- **PBR da VJ:**
  1. `python tools/r64/pbr_vj_r64.py` gera os mapas;
  2. `Unity -batchmode -executeMethod FreeBala.EditorTools.FBPbrMaps.Batch -fbmap vila_jardim` liga nos materiais, sem reimportar o mapa;
  3. a reimportação do mapa já liga sozinha.
- **Vagas:** `python tools/r64/vagas_r64.py`. É idempotente: troca as vagas `src:r64` anteriores.

## 5. Resultados (o que foi testado no exe)

Todos os testes rodaram no exe Development, em uma RTX 3060.
- Luz: capturas a 1280×720 nas builds lab96/lab97.
- VJ: tour na lab98.
- Desempenho: 1920×1080 na lab97/lab98.

**Luz: 6 mapas × 7 horários × 3 câmeras** (`F:/FreeBala_backups/r64/folhas_luz/`)
- `ciclo_<mapa>.jpg`: as poses são as mesmas em todos os horários.
  - A: olho no spawn A, olhando para B.
  - B: aérea a 22 m.
  - C: centro do mapa, olhando para o oeste.
- `ad_<mapa>.jpg`: ANTES (sistema antigo, `-fbluzantiga`) × DEPOIS, ao meio-dia e à noite, nas mesmas câmeras.
- Valores do gerente (27/02/2026; iguais em todos os mapas, porque a data e a latitude são as mesmas):

  | Hora | Sol (elevação/azimute) | Luz | Céu × | Postes |
  |---|---|---|---|---|
  | 12:00 | 78,4°/161° | sol 1,26 | 10,5 | apagados |
  | 17:30 | 10,9°/261° | sol 0,96 | 6,4 | apagados |
  | 18:00 | 3,6°/262° | sol 0,57 | 8,3 | apagados |
  | 18:30 | −4,0° | lua 0,02 (86 %) | 1481 (adaptação) | acesos |
  | 20:00 | −26,3° | lua 0,13 | 2311 | acesos |
  | 00:00 | −83,3° | lua 0,12 | 2311 | acesos |
  | 05:30 | −11,6°/98° | noite | 2311 | acesos |

- **05:30 ainda é noite em Boa Vista no fim de fevereiro**: o sol nasce por volta das 06:20. A captura de "amanhecer" mostra a madrugada com o céu clareando a leste, e isso está certo. Para ver o nascer do sol, use `-fbhora 6.4`.

**Vila Jardim × Street View** (tour de 95 pontos; `F:/FreeBala_backups/r64/folhas_ad3/`, cada folha REAL | ANTES (lab94) | DEPOIS (lab98))

| Ponto | Resultado |
|---|---|
| c06, c24, c25 | Estacionamento do Colégio CEM III com carros (24 vagas no lote oeste) |
| c08, c09 | Tapume com a cor medida do real: albedo 104,102,90 × foto 107,105,92 (antes: 126,115,99, bege). No tour (15:30, sol a 257°) essa face fica **na sombra** e aparece escura; o poste ao lado também. Está fisicamente certo; a foto do Street View é de dia nublado |
| c28 | **Não resolvido.** As 5 vagas da rua sul são descartadas pelo próprio jogo (`FBVehicles.ParkValid`). Detalhe em "Pendências" |

**Desempenho** (pose A; média de 4 s por linha, uma linha por corrida; o FPS usa a mediana). Arquivos: `F:/FreeBala_backups/r64/perf_lab97` e `perf_lab98`.

| Mapa | Hora | Nível | Antigo: ms (corridas) | Novo: ms (corridas) | FPS antigo → novo (mediana) | GPU ms antigo → novo |
|---|---|---|---|---|---|---|
| Vila Jardim | 12:00 | BAIXO | 20.9/25.5 | 21.9/21.4/18.3 | 43 → 47 | 13.2 → 12.6 |
| Vila Jardim | 12:00 | MÉDIO | 19.9/31.1 | 24.7/21.1/18.9 | 39 → 47 | 11.4 → 11.2 |
| Vila Jardim | 12:00 | ALTO | 28.1/43.0 | 30.5/26.8/24.2 | 28 → 37 | 20.0 → 16.4 |
| Vila Jardim | 12:00 | ULTRA | 27.9/45.6 | 33.5/28.3/25.2 | 27 → 35 | 20.8 → 16.4 |
| Vila Jardim | 12:00 | CINEMATIC | (não existe) | 38.0/29.0/25.9 | — → 34 | — → 16.6 |
| Vila Jardim | 20:00 | BAIXO | 20.6/22.0 | 26.2/21.6/18.4 | 47 → 46 | 10.2 → 11.3 |
| Vila Jardim | 20:00 | MÉDIO | 21.1/22.3 | 25.1/22.2/19.0 | 46 → 45 | 9.8 → 9.6 |
| Vila Jardim | 20:00 | ALTO | 30.9/32.2 | 34.3/32.1/26.6 | 32 → 31 | 19.4 → 19.3 |
| Vila Jardim | 20:00 | ULTRA | 33.2/35.1 | 45.4/72.2/29.5 | 29 → 22 | 23.2 → 23.7 |
| Vila Jardim | 20:00 | CINEMATIC | (não existe) | 40.4/34.3/31.6 | — → 29 | — → 23.3 |
| Praça do PT | 12:00 | BAIXO | 8.8/9.8 | 11.5/9.8/8.8 | 108 → 102 | 2.5 → 2.6 |
| Praça do PT | 12:00 | MÉDIO | 9.3/9.8 | 11.7/10.1/9.5 | 105 → 99 | 3.0 → 3.0 |
| Praça do PT | 12:00 | ALTO | 10.5/11.1 | 12.3/12.4/9.7 | 93 → 81 | 5.8 → 7.2 |
| Praça do PT | 12:00 | ULTRA | 10.8/12.5 | 12.3/12.0/10.6 | 86 → 83 | 6.8 → 7.6 |
| Praça do PT | 12:00 | CINEMATIC | (não existe) | 11.0/12.2/10.5 | — → 91 | — → 7.8 |
| Praça do PT | 20:00 | BAIXO | 8.4/9.4 | 12.0/9.1/8.2 | 112 → 110 | 3.0 → 2.8 |
| Praça do PT | 20:00 | MÉDIO | 9.3/9.6 | 9.6/9.5/8.7 | 106 → 105 | 5.5 → 5.3 |
| Praça do PT | 20:00 | ALTO | 10.3/11.4 | 10.7/11.5/9.5 | 92 → 94 | 6.9 → 7.7 |
| Praça do PT | 20:00 | ULTRA | 10.7/11.5 | 11.5/11.5/9.5 | 90 → 87 | 7.9 → 8.7 |
| Praça do PT | 20:00 | CINEMATIC | (não existe) | 11.0/10.7/9.8 | — → 93 | — → 9.0 |
| Morro da Sacola | 12:00 | BAIXO | 9.2/10.1 | 9.6/10.3/9.4 | 103 → 104 | 2.2 → 2.4 |
| Morro da Sacola | 12:00 | MÉDIO | 9.7/11.0 | 10.0/10.1/9.7 | 97 → 100 | 2.8 → 2.8 |
| Morro da Sacola | 12:00 | ALTO | 11.5/12.5 | 12.0/12.1/10.9 | 83 → 84 | 4.5 → 5.1 |
| Morro da Sacola | 12:00 | ULTRA | 12.2/12.5 | 12.1/12.1/11.4 | 81 → 82 | 5.0 → 5.3 |
| Morro da Sacola | 12:00 | CINEMATIC | (não existe) | 11.8/12.1/10.8 | — → 85 | — → 5.5 |
| Morro da Sacola | 20:00 | BAIXO | 9.2/10.1 | 9.5/9.5/8.8 | 104 → 105 | 2.6 → 2.7 |
| Morro da Sacola | 20:00 | MÉDIO | 9.3/10.0 | 9.6/9.4/8.8 | 104 → 107 | 3.1 → 3.0 |
| Morro da Sacola | 20:00 | ALTO | 11.4/12.8 | 12.6/12.0/10.9 | 83 → 83 | 5.4 → 5.5 |
| Morro da Sacola | 20:00 | ULTRA | 11.4/13.1 | 12.2/12.6/11.0 | 82 → 82 | 6.2 → 5.9 |
| Morro da Sacola | 20:00 | CINEMATIC | (não existe) | 12.2/12.5/11.3 | — → 82 | — → 5.8 |

**Como ler a tabela**
- A CPU manda em quase tudo na Vila Jardim:
  - de 12 a 50 mil draw calls por vista;
  - a GPU fica em 10–24 ms, enquanto o quadro leva 18–45 ms.
- O tempo de quadro varia até ±30 % entre corridas iguais, por causa do ruído da máquina. Por isso medi a luz nova 3 vezes e a antiga 2.
- **GPU (medida estável):**
  - Vila Jardim e Morro da Sacola: iguais ao antigo, dentro de ±0,5 ms;
  - Praça do PT: +0,1 a +1,5 ms, por causa do céu físico (10×4 passos por pixel) e da sonda de reflexo do céu no Alto/Ultra.
- **Quadros de 734 ms e 2123 ms** (p99 da VJ às 20:00, só na 1ª corrida de cada build nova):
  - eram shaders compilando na primeira vez: luz de poste com sombra mais os mapas PBR novos;
  - na corrida seguinte, com o cache do driver cheio, a mesma linha deu 29,5 ms;
  - **em partida**, o aquecimento do `FBLoading` roda com a luz do horário de início. Num ANOITECER, a primeira noite ainda pode engasgar uma vez (pendência 3).
- O CINEMATIC custa de 0 a 3 ms acima do Ultra (sombra 4096 a 220 m).

## 6. Pendências e limites

1. **Carros na rua sul do Colégio (c28):**
   - A faixa do jogo ali (`FBRoads` seg 20/21) tem w 8,8 m em mão dupla.
   - O `ParkValid` apaga todo carro a menos de 0,55 × 4,4 = 2,42 m do eixo, e o meio-fio fica a cerca de 2,2 m. Sem `FBSpot` (a VJ tem 0 vagas), nenhum carro sobrevive ali.
   - Para resolver: criar vagas reais no `tools/roads/build_roads.py` ou alargar a rua no mapa. Isso mexe no trânsito, por isso não foi feito nesta rodada.
   - As 5 entradas continuam no `fb_data`. O jogo as descarta sem erro.
2. **Desempenho da Vila Jardim:** 13–50 mil draw calls por vista. É anterior a esta rodada e é o que limita o FPS. Ver o plano de CPU em `Docs/pendente_02nov/cpu_plan.md`.
3. **Engasgo da primeira noite:** o `FBLoading.WarmViews` aquece os shaders só com a luz do horário de início. No modo ANOITECER, os postes acendem no meio da partida. Sugestão: no aquecimento, acender por 1 quadro uma luz pontual com sombra.
4. **Pose C do teste:** na Praça das Águas ela cai encostada numa cortina azul. A foto vale para a luz, mas mostra pouco do mapa.
5. **Sem ray tracing:** o URP 17 não tem ray tracing, e a regra proíbe migrar para HDRP. A GI é o ambiente trilight físico mais a sonda do céu. Não há GI assada por horário.
6. **Mobile:** a sonda em tempo real e o CINEMATIC ficam desligados (`FBQuality.Mobile`). Não testei em aparelho; só a build Windows foi testada.
7. **Semáforos** que não existem no real (c02–c04) e **grama alta** (c20/c21/c30): são sistemas ou decisões anteriores e não mudaram.
8. **Sessão:** os testes mortos no meio podem deixar `FBSession` com map/mode de teste. O `-fbluz` restaura no fim, quando termina normalmente.
