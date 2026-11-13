# BV CITY — auditoria dos mapas existentes (Prioridade 1)

> Rodada BV City, 09/11/2026. Esta auditoria **não alterou nenhum asset**: só mediu o que já existe.
> Tudo aqui é medição direta nos arquivos do projeto ou dado do OpenStreetMap (ODbL), com a fonte citada.
> Scripts da medição: `game/tools/bv_city/` (ver `09_relatorios/COMO_FOI_MEDIDO.md`).

## 1. O que existe hoje

| Mapa | Onde | Como entra no jogo |
|---|---|---|
| **Praça das Águas** (`praca_aguas`) | `game/assets/maps/praca_aguas/` (`map.glb`, `level.json`, `nav.json`, `praca_aguas.blend`) | gerador Blender `tools/build_praca_aguas.py` → importado pelo `FBImport` para `Resources/FBImported/Maps/praca_aguas.gltf`; cena `Map_praca_aguas.unity` |
| **Centro Cívico** (`centro_civico`) | `Assets/BURITIRO_Mapas/centro_civico/` (`centro_civico_unity.glb` 160 MB, `_dados.json`, NavMesh) | montado por `BuritiroMontaMapa.MontaCentroCivicoAuto`; cena `Map_centro_civico.unity` |
| **Mini Terminal** | **não é mapa próprio**: está dentro do `praca_aguas`, no objeto `PA_Trecho_Portal_Terminal` | — |

Os dois estão em `GameData.AllowedMaps` e no `fb_data.json`, e são arenas de 600 × 600 m do modo 10x10.

### Números medidos

| | Praça das Águas | Centro Cívico |
|---|---|---|
| Triângulos (LOD0, sem colisão) | **2.605.802** | **1.365.531** |
| Triângulos de colisão | 0 (gerados no Unity) | 338.050 |
| Nós / malhas / materiais | 1.741 / 126 / 439 | 3.087 / 1.025 / 180 |
| Organização | por tipo de prop (`PA_*`) | 51 células juntadas + `Grandes` + `Paisagem` + `Pontos` |

## 2. PRESERVAR — o que está certo e deve ser reaproveitado

**Praça das Águas.** A parte central é trabalho medido, com referência rastreável, e não deve ser refeita:

- **Letreiro "EU AMO BOA VISTA"** — congelado por decisão do usuário (`FBMonumento.cs`, materiais em `Art/Monumento/`). Não tocar.
- **Portal do Milênio** — 17 × 16 m, 3,9 m de profundidade, piso medido em captura de satélite com barra de escala (0,13366 m/px, rotação 41,8°); scripts `tools/pda_ref/z21_grid.py`, `seg_portal.py`. Passou por várias rodadas de correção com o usuário.
- **Espelho d'água em T, chafariz redondo, fonte em Z, rosa dos ventos** — geometria e jatos conferidos contra fotos (F11–F17, F60–F83) e com água animada por shader (`level.json` → `world.js setWater`).
- **Galpões da praça de alimentação** — medidos em captura z20 com barra de escala (0,25 m/px): galpão 1 em x 202,3–249,6; galpão 2 em x 278,8–327,7.
- **Mini Terminal Luiz Canuto Chaves** — 3 volumes já modelados do Street View (prédio branco x 572–604, bloco cinza do letreiro x 604–612, saguão + "asa" x 612–631, 7 pilares dourados), com guichê, 5 catracas, bancos, placas.
- **Sinalização CTB** (`tools/gen_pda_placas.py`), **mobiliário**, **desgaste/sujeira** (`gen_pda_weathering.py`), **correção de z-fighting** (`tools/zfight_fix.py`).
- **Catálogo de referências**: `unity/equipe/pda_referencias.md` — 62 capturas de Street View em `tools/pda_ref/streetview/` + 83 fotos indexadas (F01–F83), com a posição x,y de cada uma. É o ativo mais valioso do projeto para esta operação.

**Centro Cívico.** O que presta aqui é o **urbanismo e a vegetação**, não os prédios:

- Malha de vias, meio-fios, quadras, muros e portões (39 conjuntos `CC_Muros_Portoes_Quadra_*`), estacionamentos.
- Arborização nativa com espécies corretas para Boa Vista e volume alto de geometria: Oiti A 150.136 tri (98 árvores), Nim 102.306 (51), Oiti B 68.796 (49), Ipê-amarelo 54.384 (44), Mangueira 71.318 (39), Caimbé 43.218 (49), Buriti, Coqueiro, Ixora — ~490 mil triângulos de árvores.
- Iluminação pública instanciada: 229 postes LED de 9 m (63.204 tri), 53 postes de energia com cabos e isoladores.
- 119 bancos de parque, 31 lixeiras de poste, carros e motos estacionados.
- Infraestrutura de jogo pronta: `_dados.json` com 10+10 spawns, 5 spawns de polícia, 6 `basePairs` validados no NavMesh, 24 bots, 40 waypoints, 3 lanes de tráfego conferidas, 40 vagas, 584 colisores, LODs em 3 níveis, NavMesh de 2,3 MB.

## 3. RECONSTRUIR — a evidência de que os prédios são blockout

Isto é o problema que o briefing descreve, agora com número.

### 3.1 Centro Cívico: os marcos públicos somam 26 mil triângulos

Todos os materiais dos marcos "nível A" do parceiro têm prefixo `A_`. Somando os triângulos por material (LOD0):

| Material | Triângulos | Extensão medida |
|---|---|---|
| `A_estatua_concreto_patina` | **9.000** | caixa de 1,9 × 1,9 × 3,0 m |
| `A_pintura_branca` (corpos de outros prédios públicos) | 7.927 | x −355…−134, altura até 41,3 m |
| `A_nervura_cobre` | 1.120 | 24,5 × 24,5 m, 11–19 m de altura |
| `A_aluminio_composto_cinza` | 382 | 110 × 106 m |
| `A_concreto_cinza_lamina` | 320 | 63 × 57 m |
| `A_trelica_cobertura` | 294 | 110 × 106 m |
| `A_vitral_azul` | 208 | 45 × 46 m, 2,3–19,4 m |
| **`A_brise_terracota`** | **40** | 15 × 7 m, 1,7–14 m de altura |
| **`A_janela_azul`** | **40** | 8 × 7 m |
| **`A_concreto_torre_bege`** | **10** | 1,8 × 1,8 m, **até 37,3 m de altura** |
| **`A_vidro_azul_reflexo`** | **10** | 23 × 24 m, 1,7–15,5 m |
| **total `A_*`** | **≈ 26.100** | todos os prédios públicos do Centro Cívico |

Tirando a estátua, sobram **~17.100 triângulos para o Palácio Senador Hélio Campos, a Assembleia, o Tribunal de Justiça, a Catedral e o Palácio da Cultura juntos.** Para comparar: **uma única árvore Oiti tem 1.532 triângulos** e as 98 Oitis somam 150 mil — seis vezes todos os prédios públicos.

Casos concretos:

- Uma **torre de 37,3 m de altura** (provável campanário da Catedral) feita de **10 triângulos** — é um prisma.
- Um **painel de brises de 15 m de largura por 12 m de altura** com **40 triângulos** — são 10 caixas, não brises.
- Uma **fachada de vidro de 23 × 24 m** com **10 triângulos** — é um plano com textura.
- `CC_Assembleia_Corpo`: **2.256 triângulos** no prédio inteiro. Medindo a caixa **orientada** mínima da malha (e não a caixa alinhada aos eixos, que dá 109,7 × 119,5 m só porque o prédio está girado no referencial norte-sul do mapa): **86,8 × 96,0 m, rumo 48,5°, altura 22,5 m**.
  - a **orientação está certa**: erro de 0,3° contra o footprint real;
  - a **posição está certa**: os centros das caixas orientadas ficam a 2,9 m um do outro;
  - a **planta transborda o lote**: 96,0 × 86,8 m contra 81,5 × 80,8 m reais — **+14,5 m e +6,1 m**, +27% de área da caixa envolvente, e **37% dos vértices do modelo caem fora do footprint real**;
  - a repartição dos 2.256 triângulos mostra o nível de detalhe: `A_pintura_creme` 900, `A_aluminio_composto_cinza` 382 (a marquise curva), `A_concreto_cinza_lamina` 320, `A_metal_cinza` 308, `A_trelica_cobertura` 294, `A_reboco_cinza_medio` 29, **`A_veneziana_preta` 13** (todo o pano de brise) e **`A_vidro_escuro` 10** (toda a pele de vidro curva).

### 3.2 Nenhum marco do Centro Cívico tem identidade própria no arquivo

Varri os 3.087 nós do GLB por `garimpeiro|palacio|catedral|assembleia|tribunal|cultura|monumento`. O que existe com esses nomes:

- `CC_Assembleia_Corpo` (3 LODs) — o único corpo de prédio nomeado;
- `CC_Garimpeiro_Agua` — o **espelho d'água** do monumento (110 m², 60 triângulos, laje plana);
- `I_CC_Monumento_ao_Garimpeiro_Palmeira_00..02` — **três palmeiras** batizadas com o nome do monumento;
- `I_CC_Palacio_Senador_Helio_Campos_Palmeira_00..11` — **doze palmeiras** batizadas com o nome do palácio;
- `CC_Palacio_Cerca_Mouroes` — a cerca de mourões do terreno (155 × 163 m).

Ou seja: do "Palácio Senador Hélio Campos, Catedral, Assembleia, TJ e Palácio da Cultura" que o `RESUMO_PROJETO_BURITIRO.md` lista como feitos, **só a Assembleia tem um objeto próprio**. Os outros estão juntados dentro das malhas de célula (`C_<i>_<j>`, 1,52 M tri em 51 células), sem nome, sem possibilidade de editar ou substituir um prédio isolado. Isso bloqueia o requisito 4 do briefing ("cada prédio como um projeto arquitetônico individual") no estado atual do arquivo.

**O Monumento ao Garimpeiro não existe como escultura reconhecível.** Há 9.000 triângulos de `A_estatua_concreto_patina` numa caixa de 1,9 × 1,9 × 3,0 m, dentro do espelho d'água. 3 m de altura total para a figura.

### 3.3 Praça das Águas: as fachadas da avenida são o elo fraco

Somando por família de malha no `map.glb`:

| Objeto | Triângulos | O que é |
|---|---|---|
| `PA_Conteiner` (47) | **374.120** | contêineres de cobertura do 10x10 |
| vegetação (`PA_Arvore*`, `PA_fl_*`, `PA_Capim`, `PA_Palmeira`) | ≈ 900.000 | árvores, flores, capim |
| `PA_Trecho_Portal_Terminal` | 107.202 | portal + galpões + praças + **Mini Terminal** |
| `PA_Lotes_Trecho_Novo` | 36.274 | fachadas do trecho novo |
| `PA_Predios_Muros` | 27.496 | prédios e muros |
| `PA_Lotes_Gerados` | 14.070 | fachadas geradas |
| `PA_Cidade_Entorno` | 30.784 | cidade de fundo (blocos) |
| **soma das fachadas de lote** | **77.840** | **as duas calçadas ao longo de ~800 m** |

**Os 47 contêineres de cobertura têm 4,8 vezes mais geometria que todas as fachadas de prédio do mapa.** As fachadas têm ~78 mil triângulos para os dois lados de 800 m de avenida — na ordem de 1.000 triângulos por lote. É o padrão "caixa + telhado + janela pintada" que o briefing proíbe como entrega final.

Não é descuido: o `pda_referencias.md` já lista, na seção "Prioridades", exatamente estas fachadas como o que falta (Clínica Médica com cobogó, Cantinho do Açaí, DecoR, Churrascaria Seu Luís, Roraima Energia, Petrobras, China in Box/Spoleto, TCE-RR, Centro Espírita Lírio dos Vales, SR Imobiliária/Vinhal, prédio da Oi com torre). O levantamento foi feito; a modelagem individual não.

### 3.4 O anel do Centro Cívico está errado nos dois mapas

- No `praca_aguas` o anel foi aproximado por um **círculo de centro x = 748 e raio 109 m**, ajustado por 3 pontos de satélite (`pda_mapa.md`, etapa 3).
- No OSM o anel é um **circuito quadrangular**, não um círculo: quatro vias `primary` de 4 faixas chamadas "Praça do Centro Cívico" fechando **x 743,7 … 1.061,3 por y −132,3 … +118,7**, ou seja **≈ 317 × 251 m**. A ilha interna — Praça do Centro Cívico Joaquim Nabuco — tem **83.219 m²** (x 639,6…1.053,9; y −126,1…112,0), com a Praça do Garimpeiro (826 m²) dentro dela em x 860…881.
- O círculo de raio 109 m tem 37.300 m² e o centro 100 m deslocado: **erra forma, escala e posição.** Foi uma aproximação honesta para fechar a borda do mapa, mas não serve de base para o BV City.

## 4. REMOVER DA NOVA CENA (sem apagar nada dos mapas originais)

- O **tapume** que fecha o `praca_aguas` em XE = 648, atravessado no anel: no BV City a avenida continua.
- O **muro de perímetro creme** de 2,8 m (`PA_Muro_Perimetro`, 55.852 tri) e os trechos de limite jogável: o perímetro do BV City é outro.
- O **círculo do anel** (raio 109 m) do `praca_aguas`.
- A **cidade de fundo** `PA_Cidade_Entorno` no trecho que o Centro Cívico passa a ocupar de verdade.
- Os **contêineres** `PA_Conteiner_*`: são mobília de arena do 10x10, não cenário urbano. Decidir por modo, como já se faz no rolê.

Nada disso envolve apagar arquivo: o `praca_aguas` e o `centro_civico` continuam como estão, com suas cenas e seu NavMesh.

## 5. Limitações desta auditoria (o que eu não pude verificar)

1. **O gerador do Centro Cívico não está neste PC.** `prepara_unity.py` e as partes do Centro Cívico ficam em `BURITIRO_UNITY_MAPAS/` na pasta compartilhada do parceiro (`/mnt/project-files/`). Então: não sei a **origem geográfica** declarada do `centro_civico`, nem posso regerar aquele mapa. O `_dados.json` só diz "blender: x leste, y norte, z cima (metros)" e `centro = [−175, 1045]`, sem lat/lon.
2. **As fotos próprias do usuário não estão mais no disco.** O catálogo cita `C:\Users\Rodrigo\Desktop\praça das aguas\` para as fotos F01–F59; essa pasta não existe hoje. Sobraram as de F60–F83, soltas em `tools/pda_ref/`. Sem elas, parte da verificação de fachada perde a fonte.
3. **Não existe nenhuma referência do Centro Cívico neste PC** — nem foto, nem captura de Street View, nem satélite. As 62 capturas de `pda_ref/streetview` vão só até x ≈ 588 (F45–F47 mostram o Centro Cívico de longe, do anel). Para reconstruir Palácio, Assembleia, TJ, Catedral, Garimpeiro e Palácio da Cultura com fidelidade verificável **faltam referências**, e esse é o gargalo real da operação.
4. **O OSM da área é incompleto.** No perímetro inteiro há **79 footprints de prédio** (≥ 60 m²), dos quais **21 com nome** e **3 com número de pavimentos**. Para 1.250 × 430 m de centro de cidade isso é uma fração dos lotes, e praticamente não há altura. O OSM serve de **referência planimétrica e de controle**, não de inventário completo.
5. **Não medi desempenho.** Nenhuma afirmação de FPS ou de ganho aqui: não rodei o jogo nesta rodada.

## 6. Classificação final

| Asset | Veredito | Por quê |
|---|---|---|
| Letreiro EU AMO BOA VISTA | **PRESERVAR (congelado)** | decisão do usuário |
| Portal do Milênio, fontes, rosa dos ventos, espelho em T | **PRESERVAR** | medidos, revisados com o usuário |
| Galpões de alimentação, mobiliário, sinalização, desgaste | **PRESERVAR** | medidos em satélite/Street View |
| Mini Terminal (3 volumes) | **CORRIGIR** | volumetria e detalhes certos; falta microdetalhe, e o footprint real é 50 × 42 m |
| Vias, meio-fios, quadras, muros do Centro Cívico | **PRESERVAR** | malha urbana coerente |
| Vegetação e iluminação do Centro Cívico | **PRESERVAR** | espécies certas, geometria boa, instanciada |
| Dados de jogo do Centro Cívico (spawns, bases, lanes, NavMesh) | **PRESERVAR** | validados em teste |
| Fachadas dos lotes da Av. Ene Garcez | **RECONSTRUIR** | ~1.000 tri/lote, fachada genérica |
| Assembleia Legislativa | **RECONSTRUIR** | orientação certa, mas planta +19% de profundidade, posição 30 m fora e pele de vidro com 10 triângulos |
| Palácio Hélio Campos, Catedral, TJ, Palácio da Cultura | **RECONSTRUIR** | sem objeto próprio; juntados nas células |
| Monumento ao Garimpeiro | **RECONSTRUIR** | escultura de 3 m em caixa de 1,9 m; sem referência no PC |
| Anel do Centro Cívico (círculo r = 109 m) | **RECONSTRUIR** | forma, escala e posição erradas |
| `PA_Cidade_Entorno`, tapume XE 648, muro de perímetro | **REMOVER DA NOVA CENA** | só no BV City; mapas originais intactos |
