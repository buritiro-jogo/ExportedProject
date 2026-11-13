# BV CITY — rodada 1 (09/11/2026)

Mapa novo que junta **Praça das Águas + Mini Terminal + Centro Cívico** num cenário contínuo, aproveitando os
dois mapas que já existem. Esta rodada cobriu as prioridades **1 (auditoria), 2 (geografia), 3 (inventário)** e
**4 (prova de conceito)** do briefing. Nenhum mapa existente foi alterado.

- Auditoria completa, com medições: [`AUDITORIA.md`](AUDITORIA.md)
- Dados geográficos, inventário e licenças: `game/tools/bv_city/01_georef/`
- Prova de conceito: `game/tools/bv_city/09_relatorios/LEVANTAMENTO_ASSEMBLEIA.md`

## 1. Perímetro e referencial

O BV City usa **o mesmo referencial do mapa `praca_aguas` que já está no jogo**, só estendido até o fim do anel do
Centro Cívico. Isso evita reprojetar o mapa que já foi conferido com o usuário.

| | |
|---|---|
| Origem | **2.82552 N, −60.67784 W** |
| Eixo x | ao longo da Av. Capitão Ene Garcez para SE, **rumo 131,83°** |
| Eixo y | para o sul (perpendicular, à direita de x) |
| Unidade | metro real (o nó da cena no Unity aplica 1,2×, como nos outros mapas) |
| Perímetro | x **−175 … 1.095**, y **−250 … 215** → **1.270 × 465 m = 59 ha** |
| Canto NO | 2.828260 N, −60.677513 W |
| Canto SE | 2.817466 N, −60.671793 W |

Conversão: `N=(lat−2.82552)·110574`, `E=(lon+60.67784)·111184`, `x=0.745E−0.667N`, `y=−0.667E−0.745N`.
A matriz é uma reflexão (det = −1), então **ela é a própria inversa**.

Arquivos reutilizáveis: `01_georef/perimetro_bv_city.geojson`, `referencial.json`, `vias_bv_city.geojson`
(267 vias com faixas, sentido e superfície), todos em WGS84/CRS84.

### Marcos de controle (OSM)

| Marco | x | y |
|---|---|---|
| Bob's (ponta NO) | −138,8 | −15,7 |
| Rosa dos Ventos (Praça das Águas) | −18,9 | 0,9 |
| Tribunal de Contas do Estado | 98,0 | −61,6 |
| Portal do Milênio | 128,8 | −2,0 |
| Praça de Alimentação | 298,6 | 1,1 |
| Monumento dos Pioneiros | 558,2 | 0,0 |
| **Mini Terminal Luiz Canuto Chaves** | 602,4 | −5,7 |
| Tribunal de Justiça de Roraima | 701,1 | −207,2 |
| Igreja Cathedral (Cristo Redentor) | 804,8 | 154,4 |
| Assembleia Legislativa | 814,2 | −193,2 |
| **Monumento do Garimpeiro** | 869,5 | −7,0 |
| Palácio da Cultura | 935,9 | −183,2 |

As três regiões ficam num corredor contínuo de ~1 km: da Praça das Águas (x ≈ 0) ao Garimpeiro (x ≈ 870).

## 2. Como os dois mapas se encaixam

O `centro_civico` do parceiro usa outro referencial (x leste, y norte) e **a origem geográfica dele não está neste
PC** — o gerador (`prepara_unity.py`) fica na pasta compartilhada. Em vez de chutar, a transformação foi
**medida**: ajuste rígido da nuvem de **16.194 vértices de asfalto** do mapa contra **124.833 pontos de eixo de
via do OSM** (densificados a 1 m), com custo de média aparada a 55%.

| | |
|---|---|
| Rotação | **0,144°** (confirma que o referencial é mesmo norte-alinhado) |
| Translação | **(+821,97 ; −1.572,14) m** em leste/norte a partir da origem do corredor |
| Resíduo mediano | **3,40 m** ao eixo da via mais próxima |
| p75 / p90 | 5,60 m / 8,04 m |
| Abaixo de 6 m | **77,6%** |

O asfalto é superfície de ~7 m por sentido, então um resíduo de 0–7 m **ao eixo** é o esperado para um
alinhamento correto. A prancha `07_comparacoes/geo_01_alinhamento_centro_civico.png` mostra a sobreposição.

Passo a passo (em `01_georef/transformacao_centro_civico.json`):
`x_cc = x_glb ; y_cc = −z_glb` → `E = cos(θ)·x_cc − sin(θ)·y_cc + tx ; N = sin(θ)·x_cc + cos(θ)·y_cc + ty` →
`x = 0.745E − 0.667N ; y = −0.667E − 0.745N`.

Com isso, o centro da arena de 600 × 600 m do `centro_civico` cai em **x 831,9 / y −36,7** do corredor —
sobre a Praça do Centro Cívico Joaquim Nabuco (x 830 / y −11). Fecha.

**Os dois mapas se sobrepõem entre x ≈ 410 e x ≈ 648**: Mini Terminal, Praça da Cultura e Monumento dos
Pioneiros estão modelados **duas vezes**, em versões diferentes. Decidir qual vale é tarefa da integração.

## 3. Inventário de construções

`01_georef/inventario_construcoes.csv` — **94 footprints** ≥ 60 m² no perímetro, com ID, posição no corredor,
lat/lon, área, largura, profundidade, rumo da fachada, nº de vértices, pavimentos e referência OSM.

| Região | Footprints | Com nome | Área total |
|---|---|---|---|
| `BVC_PDA_*` (Praça das Águas, x < 395) | 14 | 4 | 9.882 m² |
| `BVC_TRM_*` (Mini Terminal, 395 ≤ x < 700) | 38 | 11 | 24.055 m² |
| `BVC_CCV_*` (Centro Cívico, x ≥ 700) | 42 | 9 | 29.808 m² |

**Este inventário não é completo e não deve ser tratado como tal.** 94 prédios para 59 ha de centro de cidade é
uma fração dos lotes, só 24 têm nome e **3 têm número de pavimentos**. Serve de base planimétrica e de controle;
o inventário de verdade sai do levantamento campo a campo.

## 4. Prova de conceito — Assembleia Legislativa

`BVC_CCV_ASSEMBLEIA`, 13.116 triângulos em 3 camadas, planta do OSM, fachada levantada em foto CC BY 2.0.
Prancha: `07_comparacoes/assembleia_01_foto_x_novo_x_jogo.jpg`. Detalhes e incertezas:
`09_relatorios/LEVANTAMENTO_ASSEMBLEIA.md`.

O achado que importa: o `CC_Assembleia_Corpo` que está no jogo **está no lugar certo** (centro a 2,9 m,
rumo a 0,3°) mas **não é a arquitetura do prédio** — tem colunata de arcos sob abóbada de berço, que a foto não
mostra em ponto nenhum, e não tem nenhum dos cinco elementos que tornam a Assembleia reconhecível. Tem 10
triângulos de vidro onde o prédio real tem uma pele de vidro curva inteira, e 13 triângulos no pano de brise.

**Nenhuma altura deste modelo é medida.** Tudo foi estimado por pavimento e está marcado como tal.

## 5. O que trava a operação agora

Por ordem de impacto:

1. **Não há nenhuma referência do Centro Cívico neste PC** além do que achei no Wikimedia Commons nesta rodada
   (24 fotos de licença livre, baixadas para `02_referencias/commons/`). **O Monumento ao Garimpeiro não tem
   nenhuma foto dedicada no Commons** — e é o marco central do mapa.
2. **As fotos próprias da Praça das Águas sumiram do disco.** O catálogo `pda_referencias.md` indexa F01–F83,
   mas a pasta `Desktop\praça das aguas\` não existe mais; sobraram F60–F83 soltas em `tools/pda_ref/`.
3. **Quase nada tem altura.** 3 prédios com pavimentos em 94. Sem foto com escala, toda altura é estimativa.
4. **O gerador do Centro Cívico não está aqui.** Dá para transformar e recortar o GLB, mas não para regerar
   aquele mapa a partir da fonte.

### Fotos que resolveriam (em ordem)

| Prioridade | O que fotografar | Como |
|---|---|---|
| 1 | **Monumento ao Garimpeiro** — frente, duas laterais, trás, mais um detalhe da base | com uma pessoa ou um banco no quadro, para dar escala |
| 2 | **Assembleia Legislativa** — as quatro fachadas | confirma a face da foto de 2011 e dá as alturas |
| 3 | **Mini Terminal** — plataformas, guichê, face leste | o modelo existe mas nunca foi conferido de perto |
| 4 | **Palácio Hélio Campos, Catedral, TJ, Palácio da Cultura** — frente e uma lateral cada | o Commons só tem uma vista de cada |
| 5 | **Fachadas da Av. Ene Garcez** — uma foto por lote, de frente | libera reconstruir os ~40 lotes da avenida |

Regra que vale para todas: **um objeto de escala conhecida no quadro** (pessoa, carro, poste, banco) e,
de preferência, a mesma fachada de dois pontos diferentes.

## 6. Próximas rodadas

| Prioridade | Trabalho | Depende de |
|---|---|---|
| 5 | Monumento ao Garimpeiro e geometria real do anel (não o círculo de r = 109 m) | fotos do monumento |
| 6 | Conferir e corrigir o Mini Terminal contra o footprint real (50 × 42 m) | fotos do terminal |
| 7 | Palácio, Catedral, TJ, Palácio da Cultura pelo método da Assembleia | fotos do Commons já baixadas (uma vista cada) |
| 8 | Cena `BV_City.unity`: recortar o `centro_civico` pela transformação medida, emendar com o `praca_aguas` em x ≈ 410–648 e resolver a sobreposição | nada — pode começar |
| 9 | Fachadas da Av. Ene Garcez, uma a uma | fotos da avenida |
| 10 | LODs, colisores, NavMesh, desempenho medido | cena montada |

## 7. O que esta rodada NÃO fez

- Não criou a cena `BV_City.unity` nem mexeu em cena nenhuma.
- Não alterou `praca_aguas`, `centro_civico`, `fb_data.json` ou qualquer asset existente.
- Não mediu desempenho: o jogo não foi aberto.
- Não modelou o Monumento ao Garimpeiro (sem referência).
- Não fez textura PBR própria: o modelo da prova de conceito usa cores lisas com parâmetros PBR, não mapas.

---

# Rodada 2 (09/11/2026) — os marcos do Centro Cívico

Pedido: reconstruir os outros marcos antes de montar a cena. Resultado: **um marco pronto, um levantado mas
travado, e a referência que faltava para o mapa inteiro.**

## 8. A aérea que mudou o jogo

Achei no Wikimedia Commons uma **foto aérea CC0 de 12 megapixels do Centro Cívico inteiro**
(*"Centro cívico de Boa Vista, RR"*, Dieny Portinanni Araujo Cavalcante) e **casei a câmera dela com a planta do
OSM**: projetei as vias e os marcos sobre a foto e ajustei até o traçado coincidir — rumo ≈ 95°, elevação ≈ 32°,
distância ≈ 380 m, f ≈ 2.200 px (`01_georef/scripts/sobrepoe.py`, `grade.py`;
prancha `07_comparacoes/`). A partir daí a foto deixa de ser ilustração e passa a ser **referência posicionada**:
dá para dizer o que é cada coisa e medir sobre ela.

Isso resolveu de uma vez a lacuna nº 1 da rodada 1 ("não há nenhuma referência do Centro Cívico neste PC").

> **Erro meu, corrigido:** a primeira tentativa de camera matching saiu espelhada porque o referencial do
> corredor é de **mão esquerda** (a conversão a partir de lat/lon tem determinante −1), e eu calculei o vetor
> "direita" da câmera como se fosse de mão direita. Com isso cheguei a conclusões trocadas sobre qual marco
> ficava de que lado. Corrigido projetando em (E, N, z).

## 9. Palácio Senador Hélio Campos — PRONTO

`BVC_CCV_PALACIO`, **6.962 triângulos**. Detalhes: `09_relatorios/LEVANTAMENTO_PALACIO.md`.
Pranchas: `07_comparacoes/palacio_01_foto_x_novo.jpg` e `palacio_02_foto_x_modelo.jpg`.

- Planta **53,7 × 47,9 m**, altura **13,70 m** — medida no volume do próprio mapa do parceiro (`COL_-6_22`),
  porque **o OSM não tem este prédio**.
- Arquitetura levantada em duas fotos de licença livre: colunata de 11 + 10 vãos, pele de vidro contínua em
  grelha fina, platibanda com painéis rebaixados e brasão, pórtico de entrada com frontão, pátio interno com casa
  de máquinas, caixa d'água, antena inclinada e 16 condensadoras, cobertura em caimento, anexo com placas
  solares, palmeiras e cerca de mourões.
- **O jogo não tem prédio nenhum aqui**: só as 12 palmeiras batizadas "Palacio_Senador_Helio_Campos", a cerca
  (4.494 tri) e o volume juntado na célula, sem nome.
- A primeira passada do modelo errou: eu tinha feito duas fitas de janela separadas por peitoril. A foto de solo
  mostrou pele de vidro contínua do piso ao topo — corrigido.

## 10. Catedral Cristo Redentor — LEVANTADA, MODELO TRAVADO

`09_relatorios/LEVANTAMENTO_CATEDRAL.md`. O levantamento da fachada está completo (borda superior em curva
côncava, duas bandas de brise, marquise elíptica, campanário de duas lâminas, tambor cilíndrico, lâminas curvas,
base envidraçada). **O modelo não fechou** e não entra como asset: o `building=church` do OSM é um contorno em
"C" de 53 vértices que não bate com as fotos, e não dá para saber qual lado é a fachada principal.

Destrava com **uma captura de satélite com barra de escala** da quadra — o mesmo método do Portal do Milênio.

No jogo a Catedral tem ≈ 2.048 triângulos, e **o campanário de 37,3 m é um prisma de 10 triângulos**.

## 11. Mapa de cada marco: o que o jogo tem, prédio por prédio

Casando cada material `A_*` do mapa do parceiro com o marco real mais próximo
(`01_georef/scripts/mapeia_marcos.py`, resultado em `marcos_cc_mapeados.json`):

| Marco | O que o jogo tem | Triângulos |
|---|---|---|
| **Monumento ao Garimpeiro** | estátua + espelho d'água + borda + mosaico + painel | **≈ 9.240** (9.000 só na estátua) |
| **Palácio da Cultura** | `A_nervura_cobre` 1.120, `A_brise_terracota` 40, `A_janela_azul` 40, `A_vidro_azul_reflexo` 10, `A_revestimento_pedra_escura` 10 | ≈ 1.220 |
| **Catedral** | `A_pintura_branca` 1.208, `A_laje_cobertura` 574, `A_vitral_azul` 208, torre 10 | ≈ 2.048 |
| **Assembleia** | corpo com colunata e abóbada (arquitetura errada) | 2.256 |
| **Tribunal de Justiça** | `A_reboco_azul_royal` 60 + `A_letras_douradas` 20 | **80** |
| **Palácio Hélio Campos** | nada com nome próprio | — |

O TJ-RR, com fachada azul royal e letras douradas, tem **80 triângulos** no jogo inteiro.

## 12. Dúvida levantada, a verificar

No ajuste da aérea, o nó **"Monumento do Garimpeiro" do OSM não caiu sobre o monumento** que se vê na ilha,
enquanto a Catedral casou bem com o mesmo ajuste. Ou o nó do OSM está mal posicionado, ou o resíduo do ajuste
de câmera explica a diferença. **Não mexi em nada por causa disso** — fica registrado para conferir quando houver
foto própria ou satélite com escala. O alinhamento dos mapas (rodada 1) não depende desse nó: foi feito contra
as vias.

## 13. Estado dos marcos

| Marco | Situação | Falta |
|---|---|---|
| Assembleia Legislativa | **modelo pronto** (13.116 tri) | confirmar a face da foto; alturas medidas |
| Palácio Hélio Campos | **modelo pronto** (6.962 tri) | planta por satélite com escala; faces leste e norte |
| Catedral Cristo Redentor | levantada, modelo travado | **planta por satélite com escala** |
| Palácio da Cultura | foto baixada (CC BY-SA), não modelado | levantamento |
| Tribunal de Justiça | foto ainda baixando | levantamento |
| Monumento ao Garimpeiro | **primeira referência obtida** (aérea CC0) | fotos de solo dos quatro lados |

As fotos de licença livre baixadas até agora estão em `tools/bv_city/02_referencias/commons/` com licença e autor
registrados em `commons_licencas.json`.

---

# Rodada 3 (10/11/2026) — Tribunal de Justiça; a Catedral continua travada

## 14. Tentei tirar a planta da Catedral da aérea CC0 — e medi que não dá

Antes de recorrer a imagem de satélite, tentei resolver a câmera da aérea CC0 por otimização e projetar o chão
de volta. **Não funciona, e o teste está documentado:** ajustando a mesma câmera com três raios de pontos de
controle (420 m, 235 m, 150 m), as soluções divergem — rumo 119,4° → 115,7° → 147,4°, distância 566 → 495 →
475 m, focal 1.841 → 2.275 → 1.819 px. Reprojetando cinco pontos conhecidos, duas dessas soluções **discordam
de 128 a 534 px** (mediana 367), o que na escala da foto são dezenas de metros no chão.

É uma foto oblíqua de drone, sem ponto de controle verdadeiro: o gradiente das bordas tem muitos mínimos locais
e cada conjunto de pontos cai num deles. Serve para **identificar e entender**, não para **medir planta**.

O que ficou esclarecido: ampliando a Catedral com a projeção por cima, o **`building=church` do OSM não é o
prédio** — cai deslocado e com forma que não corresponde; o `place_of_worship` (59 × 72 m) é o **terreno**. A
forma real aparece clara na foto: planta em **leque**, com o ápice junto ao campanário.

Também verifiquei: a **OpenAerialMap não tem cobertura** de Boa Vista, e Sentinel (10 m/px) e CBERS (2 m/px) são
grosseiros demais para uma planta de 35 m.

**Fica uma decisão para o dono do projeto:** a captura que resolve é a do Google Maps com barra de escala — o
mesmo método já usado no Portal do Milênio e nos galpões. A seção 3 deste briefing proíbe "digitalização de
construções ... derivadas de imagens restritas do Google". Existe precedente no projeto, mas a escolha não é
minha. A alternativa sem esse impasse são **fotos próprias** das quatro faces com objeto de escala.

## 15. Tribunal de Justiça de Roraima — PRONTO

`BVC_CCV_TJ`, **1.660 triângulos**. `09_relatorios/LEVANTAMENTO_TJ.md`;
prancha `07_comparacoes/tj_01_foto_x_novo_x_jogo.jpg`.

- Planta **45,24 × 35,84 m** do OSM (`amenity=courthouse`, 7 vértices) — **medida**.
- Fachada levantada em foto de **domínio público**: pórtico central saliente em verde-água com letreiro dourado
  de duas linhas e dois brasões, três panos de vidro em grelha fina com moldura branca, escadaria central,
  rampa de acessibilidade com corrimão, gradil geométrico, adro de bloquete com canteiros de ixora.
- **No jogo são 80 triângulos**: 60 de `A_reboco_azul_royal` e 20 de `A_letras_douradas`. É o prédio com menos
  geometria de todo o mapa.
- Achado de cor: o material do parceiro chama-se "azul royal", mas na foto a cor do pórtico é **verde-água claro**.

## 16. Rodada 4 (10/11) — a Catedral destravou, e sem imagem restrita

A autorização era para medir a planta em captura de satélite do Google. **Não precisei dela**, e o caminho que
apareceu é melhor do que o autorizado.

**O que eu disse na rodada 3 estava errado pela metade.** Eu havia descartado o `building=church` do OSM
(53 vértices, contorno em "C") dizendo que "não é o prédio". Ele **é** o prédio: estava apenas **deslocado
6,5 m para oeste**, e eu não tinha ortoimagem contra a qual medir esse deslocamento. A reentrância em "C",
que me pareceu inexplicável, é a **fachada côncava** — exatamente o que a foto frontal mostra.

### Fonte de imagem, testada antes de abrir a restrita

| Fonte | Resultado |
|---|---|
| **Esri World Imagery** | cobre Boa Vista só até **z17 = 1,19 m/px**; acima disso devolve "Map data not yet available" |
| OpenAerialMap | sem cobertura |
| Sentinel-2 / CBERS | 10 m/px e 2 m/px — grosseiros demais |
| **Aérea da Bing (Microsoft)** | **z19 = 0,298 m/px**, imagem real e nítida sobre a quadra |

A aérea da Bing é a camada que os editores do OpenStreetMap usam: a **Microsoft concedeu ao OSM permissão
expressa para digitalizar construções a partir dela**. São 25 tiles da quadra e do entorno, mosaicados e
georreferenciados pela própria malha Web Mercator — escala exata, sem barra de escala para ler.
**A geometria do asset continua sendo do OSM (ODbL);** a imagem serviu para medir três números.

### O registro, medido

Alinhamento pelo **gradiente da imagem ao longo das arestas** do polígono (o critério por área não serve:
a mancha clara inclui o adro e o IoU satura em 0,51).

- deslocamento **dE −6,5 m, dN −0,5 m**, giro **+1,0°**;
- gradiente médio sob as arestas **0,362** contra **0,115** sem corrigir (**+215 %**);
- o pico cai para 0,294 a 1 m, 0,230 a 2 m, 0,180 a 4 m — ou seja, está bem determinado, ±0,5 m;
- resultado: **881 m²**, caixa orientada **47,8 × 34,8 m**, eixo no rumo 228,7°.

### Uma conferência que corrigiu o automático

Sobrepus os eixos de via do OSM à aérea para ver se a imagem estava no lugar. Um otimizador de "cara de
asfalto" pediu deslocar as vias em −7,0/+7,5 m. A prova visual mostrou que **ele estava errado**: deslocadas,
as vias caem sobre os carros estacionados e o canteiro; sem deslocar, os dois eixos da rua entre a praça e a
Catedral caem na rua. Adotado **sem deslocar**. O critério tinha sido puxado pelas áreas pavimentadas grandes,
que também são cinza. Prancha: `02_referencias/sat/conf_rua.png`.

### Qual lado é a fachada

Três evidências concordam: a reentrância abre para **leste-sudeste** (rumo 103,8°); o **tambor cilíndrico**
(Ø 8,8 m medido) fica ao sul dela, e na foto frontal aparece **à esquerda** de quem olha — o que só fecha com
o observador a leste; e o **adro largo** está a leste. Corda da fachada **19,71 m**, flecha 4,0 m.

### Modelo

`BVC_CCV_CATEDRAL`, **4.422 triângulos** (488 + 1.174 + 2.760). Fachada côncava com duas bandas de brise, a
banda alta **acompanhando a curva do telhado**; vitrais azul/amarelo/branco; marquise elíptica de 14,2 × 6,6 m
em três pilares; campanário de 37,3 m com vazio em cruz; tambor com vitral na base; vela curva; ala baixa
envidraçada ligando a fachada ao tambor; casca do telhado em leque com fáscia saliente.

Relatório: `09_relatorios/LEVANTAMENTO_CATEDRAL.md`. Pranchas: `07_comparacoes/catedral_01_foto_x_novo.jpg`
e `catedral_02_volume_e_planta.jpg`.

**Não verificado:** todas as alturas (estimadas em proporção de foto), as três fachadas sem foto, o interior,
a curvatura real da casca, a posição exata do campanário e o desenho dos vitrais. A planta tem precisão da
ordem de **±1 m**, limitada pela resolução da ortoimagem.

## 17. Rodada 5 (10/11) — Palácio da Cultura Nenê Macaggi

`BVC_CCV_CULTURA`, **2.321 triângulos** (no jogo: ≈ 1.220).
Relatório: `09_relatorios/LEVANTAMENTO_CULTURA.md`. Pranchas: `07_comparacoes/cultura_01_foto_x_novo.jpg`
e `cultura_02_planta.jpg`.

### A planta NÃO veio do OSM, e vale dizer por quê

Tentei repetir o que destravou a Catedral — registrar o `way 1332740171` pelo gradiente de borda — e
**o método falhou aqui**:

- o ajuste de um prédio só deu dE −11,5 / dN −4,0 / giro −3,0°, com pico fraco (0,307 contra 0,144), e o
  telhado é **nervurado**, o que cria vários falsos picos paralelos;
- ajustando os **nove prédios da quadra juntos** o deslocamento comum ficou em dE −9,5 / dN −1,0, mas os
  melhores individuais variaram de dE +15 a −12 — o critério não discrimina nesta quadra;
- a prova visual explicou: os **Correios**, ao lado, encaixam **sem deslocamento nenhum**. Ou seja, o OSM
  daqui não está deslocado; o polígono do Palácio é que foi **desenhado largo**, e o entalhe que ele traz
  está no canto **oposto** ao que a imagem mostra.

Então **tracei a planta direto na ortoimagem**, no referencial do corredor, onde o prédio aparece alinhado
à malha da cidade: **x 907..945 / y −193..−160 = 38,0 × 33,0 m = 1.254 m²**, precisão ±2 m. O OSM concorda
em área dentro de 6 % e, depois do registro do conjunto, em posição dentro de ~3 m — serve de conferência,
não de geometria.

### Qual lado é a fachada

A praça pavimentada com palmeiras da foto está a **sudoeste** (rumo 221,8°); nos outros três lados há
árvores e o bloco dos Correios. Com a fachada aí, a foto fecha: **ala envidraçada à esquerda**
(x 907..929) e **parede cega de pastilha à direita** (x 929..945) — e é justamente nesse trecho que a
ortoimagem mostra **área escura**, isto é, pátio ou terraço de um pavimento atrás de uma parede-biombo.
A foto mostra exatamente isso: parede alta sem janelas de verdade, só três frestas horizontais,
servindo de suporte ao letreiro.

### Modelo

Parede de pastilha cinza-azulada com remate branco; letreiro em duas linhas ("Palácio da Cultura" fina
em cima, **"Nenê Macaggi"** grande embaixo, 30 letras); três frestas de vidro escuro com moldura
alaranjada; ala envidraçada recuada sob a laje saliente de forro vermelho; duas faixas vermelhas com
58 brises verticais; faixa de vidro iluminada na base; escada e rampa em lajota com corrimão contínuo;
jardineiras, três palmeiras e o poste ornamental.

**Não verificado:** todas as alturas (proporção de foto **noturna, sem régua no quadro** — são as medidas
mais frágeis até aqui); o que existe no quadrante sudeste (adotei pátio sobre bloco de um pavimento);
as três outras fachadas; a tipografia do letreiro; o tom da pastilha; o número de pavimentos.

## 18. Rodada 6 (10/11) - montagem da cena BV City: medido, preparado, **nao rodado**

A cena ainda nao existe. Esta rodada **mediu e preparou** tudo o que a montagem precisa, e achou um
problema que teria entortado a cena se eu tivesse montado as cegas.

### Como o projeto monta um mapa

- Mapas **do jogo web** (`praca_aguas`, `vila_jardim`, `praca_pt`, `orla`): a malha vem de
  `assets/maps/<id>/map.glb` e e importada para `Assets/FreeBala/Resources/FBImported/Maps/<id>.gltf`.
  A cena `Map_<id>.unity` guarda so colisores, postes e pontos.
- Mapas **do parceiro** (`centro_civico`, `morro_sacola`): `BuritiroMontaMapa.Monta(id)` monta a cena a
  partir de `Assets/BURITIRO_Mapas/<id>/<id>_unity.glb` + `<id>_dados.json`, usando marcadores `REF`
  dentro do GLB para converter metros em posicao.
- Para o mapa aparecer no menu falta liberar o id em `GameData.AllowedMaps` e no filtro do `FBMenu`.

### Escala: os dois mapas estao em 1 unidade = 1 metro

Eu desconfiava de um descompasso de 1,2 (o codigo fala em "o mundo do jogo e 1,2 unidade por metro").
**Nao ha.** Ajustei a conversao de cada mapa com os pontos das proprias cenas montadas:

| Mapa | Conversao para o Unity | Pontos | Residuo |
|---|---|---|---|
| `centro_civico` | Unity = (-E, h, -N) | 90 | **0,0000 m** |
| `praca_aguas` | Unity = (-p[0], p[1], p[2]) do `level.json` | 40 | **0,0000 m** |

Os dois colocam seus dados **1:1**. O 1,2 e escala de personagens e veiculos importados, nao dos mapas.

### A transformacao entre os mapas, conferida quatro vezes

`E = cos(0,144°)·E_cc - sin(0,144°)·N_cc + 821,97` e `N = sin(0,144°)·E_cc + cos(0,144°)·N_cc - 1.572,14`,
depois `x = 0,745E - 0,667N` e `y = -0,667E - 0,745N`.

1. ajuste rigido de 16.194 vertices de `A3_asfalto` contra 124.833 pontos de eixo de via do OSM - residuo
   mediano **3,40 m** (rodada 1);
2. centro da arena (-175, 1045) -> corredor **x 831,93 / y -36,73**, igual ao valor da rodada 1;
3. o espelho de agua do Garimpeiro cai em **E 654,7 / N -575,8**; o no do OSM esta em E 652 / N -575 - **3 m**;
4. **novo:** nuvem de 220.000 pontos de cada mapa, extraida dos GLB, sobreposta as vias e aos predios do
   OSM. O `centro_civico` encaixa; espelhado **no proprio centro**, nao encaixa
   (`02_referencias/sat/cc_quiralidade.png`). O `praca_aguas` idem (`pa_vs_realidade.png`). E as duas
   nuvens se encontram no trecho que ambos modelam (`sobreposicao_mapas.png`).

### O problema que apareceu

Os dois mapas **posicionam seus dados por convencoes diferentes**: o `praca_aguas` pelo x/y do corredor,
o `centro_civico` por leste/norte. Essas duas convencoes diferem por uma **reflexao**. Posta assim, sem
conferir, a montagem entraria com o Centro Civico **espelhado** - e isso so se ve olhando de cima.

Nao da para decidir isso no papel com o que esta no disco: os controles que eu tinha (a malha de navegacao
do `praca_aguas`, o anel do Centro Civico) sao **simetricos demais** para distinguir. O teste que decide e
um **print ortografico de cima** da cena montada, comparado com a ortoimagem - e para isso a cena precisa
existir. Por isso o montador tem a chave `-fbespelho`: roda-se com 0 e com 1, e so uma das duas encaixa.

### O que ficou pronto

- `Assets/FreeBala/Editor/FBMontaBVCity.cs` - monta `Map_bv_city.unity` sem alterar nenhum dos dois mapas:
  instancia o `praca_aguas` no lugar (o referencial do corredor e o dele), pousa o `centro_civico` pela
  transformacao medida (resolvendo a pose por tres pontos, sem supor como o glTFast converteu os eixos),
  insere os **cinco marcos** levantados, costura a faixa repetida e grava o print de cima.
- **Costura proposta** para a faixa que os dois repetem (x 410..648 - Mini Terminal, Praca da Cultura,
  Pioneiros): em **x = 530**, o meio da faixa. Antes dela manda o `praca_aguas`, depois o `centro_civico`.
  Nada e apagado: os objetos de fora da costura ficam **desligados**.
- Os cinco GLB copiados para `Assets/BURITIRO_Mapas/bv_city/marcos/`.
- Todos os numeros em `tools/bv_city/01_georef/montagem_bv_city.json`.
- Ferramentas novas: `glb_nuvem.py` (nuvem de pontos de um GLB com as transformacoes de no aplicadas),
  `sobrepoe_mapas.py`, `confere_realidade.py`, `confere_cc.py`, `sat_quadra.py`, `sat_vista.py`.

### Por que nao rodei

O Unity esta **aberto** nesta maquina, e a regra do projeto e so um editar por vez. Alem disso a montagem
abre uma cena nova: se houvesse mudanca nao salva, ela se perderia. O montador recusa rodar nesse caso,
mas a decisao de tomar o editor e do dono do projeto. Com o aval, roda-se pelo laboratorio do terminal,
com o Unity aberto: pedido `metodo FBMontaBVCity.MontaNormal` e depois `metodo FBMontaBVCity.MontaEspelho`,
comparando os dois prints.

## 19. Rodada 7 (10/11) - a cena BV City EXISTE e o mapa entrou no jogo

`Assets/FreeBala/Scenes/Map_bv_city.unity`, **21,5 MB**. Montada por
`Assets/FreeBala/Editor/FBMontaBVCity.cs` em lote, com o Unity fechado.

### A reflexao, decidida por medida

O montador poe o centro_civico resolvendo a pose por **tres pontos de prova**: se a transformacao
precisasse de reflexao, o terceiro ponto nao fecharia. E e exatamente o que aconteceu:

| | terceiro ponto |
|---|---|
| `-fbespelho 0` (corredor -> Unity = (-x, h, **-y**)) | erra **399,99 m** |
| `-fbespelho 1` (corredor -> Unity = (-x, h, **+y**)) | fecha em **0,009 m** |

E o print de cima com espelho 1 bate com a ortoimagem: o anel, as radiais, o Palacio no meio do anel,
a avenida inteira e a Rosa dos Ventos caem no lugar. **Espelho 1 virou o padrao do montador.**

### Dois erros meus que a conferencia pegou

1. **A cena vazia nao servia de base.** Na primeira versao eu instanciava o `.gltf` do praca_aguas numa
   cena nova - e o mapa ficava **sem colisao nenhuma**, porque os colisores do mapa web (`FBCol_*`) moram
   na cena `Map_praca_aguas.unity`, nao no arquivo. A base passou a ser **a propria cena do praca_aguas**,
   salva com outro nome. `Map_praca_aguas.unity` **nao e tocado** (continua com a data de 28/10).
2. **7,8 % da imagem saia rosa.** Rodei o mesmo print na cena do parceiro como controle: **0 % de rosa**.
   Ou seja, o defeito era meu. Causa: o GLB do centro_civico traz um grupo **`Colisao`** cuja geometria o
   montador do parceiro converte em `MeshCollider` **apagando o renderizador**; sem esse passo ela continua
   sendo desenhada, em rosa. Passei a chamar os mesmos metodos do parceiro
   (`MontaColisores`, `MontaLODs`, remocao do `REF`), abrindo a visibilidade deles de `static` para
   `internal` - unica mudanca no arquivo dele, guardada em `_backup/bvcity_20261110/`.

### O que a cena tem

| | |
|---|---|
| Base | `Map_praca_aguas.unity` (malha, colisores, postes, vegetacao, pontos) |
| Centro Civico | instancia do GLB do parceiro na pose medida, **585 colisores** e **918 grupos de LOD** |
| Marcos | **5** (Assembleia, Palacio Helio Campos, Tribunal de Justica, Catedral, Palacio da Cultura) |
| Costura | **767** objetos desligados (nao apagados) na faixa repetida, com corte em x = 530 |
| Build | a cena entrou em `EditorBuildSettings` |

### O mapa no jogo

- `GameData.AllowedMaps` ganhou **`bv_city`** (o filtro do `FBMenu` ja usa essa lista).
- `StreamingAssets/fb_data.json` ganhou a entrada `bv_city`, com copia em `Resources/fb_data.json`.
  Backup do arquivo antigo em `_backup/fb_data_bvcity_20261110/`.
  Os pontos sao a uniao dos dois mapas, **recortados ao perimetro**: nascer A na Praca das Aguas
  (x -72..-48), nascer B no anel do Centro Civico (x 827..842), 39 bots, 9 pontos e 68 waypoints
  espalhados de x -149 a x 1102.

Conversao usada (o `fb_data` guarda coordenada do jogo web, nao do Unity; o `Coords.FromWeb` inverte o x
na carga): ponto do centro_civico `(E, h, -N)` -> corredor pela transformacao medida -> `(x, h, y)`.
Conferido: o centro da arena do parceiro cai em **x 831,93 / y -36,73**, igual ao valor da rodada 1.

### NavMesh (assado e conferido)

`Assets/BURITIRO_Mapas/bv_city/NavMesh_bv_city.asset`, **3,8 MB**, ligado na cena pelo `FBNavData`.
Assado pelo mesmo caminho do parceiro - colisores da camada do mapa -, mas sobre o **corredor inteiro**
(1.310 x 505 m com folga, 59 ha), nao sobre uma arena de 600 x 600: **1.749 colisores** entraram.

| conferencia | resultado |
|---|---|
| grade de 8 m, 9.381 pontos com chao | **98,2 % navegavel** a menos de 1 m do chao |
| pontos do mapa no `fb_data` | **100 de 100** sobre a malha |

**Cuidado com o numero facil:** a primeira conferencia que escrevi usava raio de busca de **6 m** e deu
**100 %** - de cima de um telhado ela achava a rua. O numero que vale e o estrito: raio de 1 m a partir do
chao achado por raio de cima para baixo.

### Dois defeitos que a conferencia do NavMesh pegou

1. **A costura desligava so os renderizadores.** Os **colisores** do mapa que nao manda naquele trecho
   continuavam ligados - colisao invisivel na faixa, e o NavMesh (que nasce dos colisores) saia sujo.
   Agora ela desliga renderizador **e** colisor, com `Physics.SyncTransforms()` antes de ler as caixas,
   porque o Centro Civico acabou de ser movido. Passou de 767 para **949** objetos desligados.
2. **Os pontos do mapa ignoravam a costura.** Eu tinha juntado os pontos dos dois mapas so recortando ao
   perimetro: 13 deles caiam sobre o chao do mapa que agora esta desligado ali. A regra passou a ser
   **praca_aguas ate x = 520, centro_civico a partir de x = 540**, com 20 m de guarda. Os pontos cairam de
   170 para **100**, e os 100 estao sobre a malha.

### Rodada 8 (10/11) - TESTADO NO JOGO

Build Mono nova (14,6 GB, 16 min, 0 erros) em `E:\FreeBala_builds
66_bvcity\WindowsMono`, porque a de
17:25 era anterior a cena. Tres testes:

| teste | resultado |
|---|---|
| `-fbtesteexe paredes bv_city` | **0 erros/excecoes**. NavMesh com 96.340 vertices e 43.294 triangulos; de **87.109 arestas**, 70 atravessam parede e 137 passam sob teto baixo - **0,24 %**, e a maior parte nos mesmos objetos do praca_aguas que ja estao no `PENDENTE.md` (`PA_Lotes_Gerados`, `PA_Predios_Muros`, `PA_parquinho_166`) |
| `-fbtesteexe 10x10 bv_city 2 piloto` | **0 erros/excecoes**. 2 rodadas em 102 s, placar 2 x 0. **0 bots presos** nas duas rodadas. O piloto comprou 5 itens, andou 166 m, deu 212 tiros com 41 acertos (19 %) e 13 abates, **0 travadas, 0 camera na parede, 0 mortes**. FPS medio 83 a 110, quadro 9 a 12 ms |
| `-fbmenu` | o cartao **BV CITY** aparece na lista de mapas e a pagina ESCOLHER PARTIDA desenha inteira |

Observacoes do teste:

- o `FBModo10x10` **monta a propria arena** a partir do mapa (bases a 94 m uma da outra, dentro da Praca
  das Aguas), entao o mapa ja e jogavel no modo competitivo sem ajuste - os nascimentos distantes do
  `fb_data` nao atrapalham;
- o minimapa "neutro" cai para a foto do mapa (`Texture2D.GetPixels32 ... not readable`). **Nao e nosso:**
  todas as capas em `Resources/MapShots` tem `isReadable: 0`, inclusive as que ja existiam;
- a juncao por celula do lado do praca_aguas rodou normal: 957 pecas -> 183 malhas em 40 ms;
- censo em jogo: 4.740 renderizadores ligados, 1.534 cortados por distancia; o maior grupo e
  `BVC_centro_civico/Celulas` com 967.

### O que ainda falta

1. **A piscada do menu nao tem prova quadro a quadro.** A pagina ESCOLHER PARTIDA desenha inteira e nada
   quebrou, mas a transicao dura 0,22 s e uma foto nao mostra isso. A prova esta no codigo (o `FBFadeIn`
   simplesmente nao e adicionado quando a pagina so se atualiza); conferir a olho e o que fecha.
2. **A costura e visivel em qualidade**, nao em geometria: nao ha buraco no chao, mas o lado do Centro
   Civico tem predios e arvores modelados e o lado da Praca das Aguas naquele trecho e cenario de fundo
   mais raso (`07_comparacoes/bv_city_costura.jpg`).
3. **Capa provisoria.** `Resources/MapShots/bv_city.jpg` foi recortada do proprio render de cima; as outras
   capas do jogo sao fotos em perspectiva.
4. **Arena dos modos.** Os modos competitivos usam 600 x 600 m; o BV City tem 1.270 x 465 m e os
   nascimentos ficam a ~900 m um do outro - bom para modo livre, longe demais para o 4x4.
5. Nada foi commitado.

## 19. Estado dos marcos, atualizado

| Marco | Situacao | Triangulos (novo / no jogo) |
|---|---|---|
| Assembleia Legislativa | **pronto** | 13.116 / 2.256 |
| Palacio Helio Campos | **pronto** | 6.962 / sem objeto proprio |
| Tribunal de Justica | **pronto** | 1.660 / **80** |
| Catedral Cristo Redentor | **pronto** | 4.422 / 2.048 |
| Palacio da Cultura | **pronto** | 2.321 / aprox. 1.220 |
| Monumento ao Garimpeiro | referencia obtida, nao modelado | - / aprox. 9.240 |

Nada foi commitado. Nenhum dos dois mapas existentes foi alterado.

# Rodada 10 (11/11/2026) - levantamento completo e planta r10

Relatorio inteiro: `game/tools/bv_city/09_relatorios/RODADA10.md`; por setor: `RELATORIO_SETORES.md`. Nada entrou no Unity.

- **A aerea Bing esta ~8 m a oeste do GPS; o OSM esta certo.** Quatro referencias independentes (enderecos do IBGE, trilhas de GPS,
  contornos do Google, registro na aerea). A leitura da rodada 9 ("predios do OSM deslocados") estava invertida.
  `validacao/desloc_aerea.json` guarda o campo; `aerea.py` ja amostra com a correcao.
- **A corrigir na Fase 4:** `BVC_CCV_CATEDRAL` (contorno movido 6,5 m para a aerea na rodada 4) e `BVC_CCV_CULTURA`
  (planta tracada na aerea) estao ~6-7 m a oeste do lugar.
- **Cadastro de edificacoes:** 21.586 contornos com identificador (OSM 1.182 + Google Open Buildings 20.143 + Microsoft 261);
  2.083 conferidos um a um na aerea, 1.345 divergem, o resto sem decisao automatica. Provisorio; sem lotes, sem fachadas.
- **Larguras:** medidas na aerea corrigida; por comprimento 53,8 km medidos, 159,2 km calibrados por classe, 44,8 km estimados.
- **Relevo:** Copernicus GLO-30; rio a -22 m (era -3), barranco e vale do igarape; plato e corredor planos por decisao.
- **Planta r10:** 160 setores, 676.783 triangulos, 21.545 volumes provisorios; conferencia: marcos a 3-6 cm.
- Perimetro mantido (20,29 km2): a imagem do pedido nao chegou anexada; Paraviana, Dos Estados, Garden Shopping, Mirandinha,
  Aeroporto e Cacari continuam fora.

## Rodada 11 (11/11/2026) - planta no Unity por setor e inicio da Fase 4

Relatorio completo: `r11/RODADA11.md` (copia de `tools/bv_city/09_relatorios/`). Nada foi jogado; sem build.

- **Planta r10 na cena:** 160 setores como prefabs (`Resources/BVCity/Setores`, 650.854 triangulos, 21.239 volumes PROVISORIOS),
  carregados por distancia pelo `FBBVCitySetores` (entra a 700 m, sai a 900 m). Posicao conferida: erro 0,0000 m em 320 pontos.
- **Defeitos antigos corrigidos:** Centro Civico 1,5 m acima da Praca das Aguas (desceu 1,53 m); marcos enterrados e com pivo e giro
  errados (pose refeita por encaixe no contorno do cadastro); Tribunal de Justica desligado sem querer (religado).
- **Marco "Palacio Helio Campos" desligado:** estava sobre o lote do Palacio da Cultura e com planta de outro predio. O Palacio do
  Governo fica dentro do anel, em torno de (724; -4). A refazer.
- **Saiu de cena:** pano de fundo de 7 km do parceiro e o recheio generico em volta da Praca das Aguas; a planta entra no lugar.
- **Pendente:** jogar (10x10 e paredes na cena nova), carga assincrona no jogo, NavMesh fora das zonas, agua sem leito, faixa
  x 530-757 sobreposta, Assembleia com planta 23 % maior, lotes e fachadas.

## Rodada 12 (11/11/2026) - verificacao independente, Palacio do Governo refeito, leito da agua

Relatorio completo: `r12/RODADA12.md`. Nada foi jogado; sem build.

- **Errata da rodada 11:** a Assembleia nao e 23 % maior (o corpo segue o contorno; a laje de cobertura tapava o recorte do
  anexo); o modelo antigo do Palacio era do predio certo, mas grande demais, com a fachada no eixo errado e o patio tampado.
- **Palacio Senador Helio Campos refeito** pela aerea corrigida e pela foto: 46 x 36,5 m, patio aberto, 10 vaos, frente para o
  gramado (rumo 133). Altura ESTIMADA. A entrada, o canteiro e as palmeiras que estao na cena sao do mapa do parceiro.
- **Assembleia** sobre o telhado medido (o contorno do OSM dela esta ~7 m deslocado). **Lado da fachada em duvida.**
- **Leito com colisor debaixo de toda agua** (profundidade ESTIMADA); 1 ponto sem chao em 95.200 na janela do corredor.
- **A Praca das Aguas termina em x = 530**: muro e tapume dela atravessavam o anel do Centro Civico.
- Verificacao independente (2 agentes em serie): 6 defeitos achados e corrigidos; degrau planta x zona era maior que o relatado.

## Rodada 13 (11/11/2026) - consulta ao Street View e Assembleia remodelada

Relatorio completo: `r13/RODADA13.md`. Nada foi jogado; sem build. Foi so consulta: nada do Google entrou no jogo.

- **Assembleia:** a fachada da testeira curva (vidro e arcos) fica na Av. Ville Roy; o lado da praca e uma entrada coberta com
  pilares curvos e molduras em arco. Modelo refeito: casca de cobertura subindo de norte para sul (topo ~24 m, ESTIMADO), parede
  oeste de 64,9 m, entrada coberta de 12 m (ESTIMADO). Pose por construcao: pivo (769,91; -222,99), giro 90.
- **Palacio do Governo:** a entrada com frontao e sacada que esta na cena (do mapa do parceiro) confere com foto de 2020.
- Catedral, Tribunal de Justica e Palacio da Cultura ainda nao foram consultados no Street View.

## Rodada 14 (11/11/2026) - consulta ao Google Maps: TJ, Cultura e Catedral

Relatorio completo: `r14/RODADA14.md`. Nada foi jogado; sem build. Foi so consulta.

- **Tribunal de Justica:** o predio e azul (corpo e portico); o modelo estava branco com portico verde-agua, de uma foto antiga. Corrigido.
- **Palacio da Cultura:** atras da parede do letreiro ha um telhado escuro de quatro aguas (nao um patio). Acrescentado; cai sobre a
  mancha escura da aerea a ~1 m, o que confirma a pose. A planta do modelo ainda diverge do contorno do OSM.
- **Catedral:** uma vista obliqua confere com o modelo; nada mudou.
- Alturas de todos os marcos continuam ESTIMADAS.

## Rodada 15 (11/11/2026) - Praca das Aguas em escala real, Praca Velia Coutinho, texturas

Relatorio completo: `r15/RODADA15.md`. Nada foi jogado; sem build. Google Maps foi so consulta.

- **A Praca das Aguas estava 1,2 vez maior que o real** (mapa do jogo web, ampliado para o personagem de 2,1 m). Reduzida so na cena do
  BV City: avenida 87,8 m no real, 87,5 m na cena (era 105 m). Zona mantida nova: x -100..530, y +-49,2 m.
- O corte antigo em x = 530 valia 442 m reais: o trecho leste da praca voltou a cena.
- **Praca Velia Coutinho:** tres predios novos (dois restaurantes com passarela, um predio terreo de cinco pavilhoes). Alturas ESTIMADAS.
- **Texturas proprias** (geradas por script) nos predios novos, nos marcos, no chao da planta e nos volumes dos predios (reboco + classe de
  telhado lida na aerea). Os volumes continuam provisorios, sem fachada.
- Verificacao independente: 3 defeitos corrigidos (postes, colisores, vegetacao); medicao aerea x cena em
  `tools/bv_city/09_relatorios/verificacao_r15/`.
- **Nao feito:** muro da arena em volta da praca; inclinacao de 0,47 grau; mapa 2 a 11 m a leste do real de x = 145 em diante; canteiro
  central 12,5 m mais estreito; lote x 350..470 inventado.

## Rodada 16 (11/11/2026) - muro fora, Mini Terminal, video proprio da frente norte

Relatorio completo: `r16/RODADA16.md`. Nada foi jogado; sem build.

- **Muro da arena** da Praca das Aguas saiu; as 10 ruas transversais reais foram abertas no x real (`06_unity/bocas_pa.txt`).
- **Mini Terminal Luiz Canuto Chaves** refeito (marco `BVC_CCV_TERMINAL`); antes havia um bloco generico. Alturas ESTIMADAS.
- **Video proprio de 11/11** (`E:\BURITIRO IMAGENS\PRAÇA DAS AGUAS`): quadros extraidos, faixa continua da frente norte e 15 fachadas
  RECRIADAS em 3D a partir dele (marco `BVC_PDA_FRENTE_NORTE`). Letreiros so com o nome; nenhum logotipo.
- **Limites:** o Tribunal de Contas recriado ficou pior que a foto e precisa de modelo proprio; frente sul sem video; verificacao
  independente do muro nao terminou e a das fachadas nao foi feita.


## Rodada 17 (12/11/2026) — TCE, prédio da Oi, fachadas revistas, verificação independente

Relatório: `r17/RODADA17.md`. **Não está 100 % fiel; nada foi jogado.**

- Marcos novos: `BVC_PDA_TCE` (2.998 triângulos) e `BVC_PDA_OI` (4.112: prédio, anexos, gradil, torre de concreto, torre treliçada). No lote da Oi saiu o galpão genérico do mapa do parceiro (13.542 triângulos).
- Frente norte revista (especificação v2), com 9 placas de trânsito, 2 totens e o abrigo de ônibus; 42 itens de mobiliário do vídeo não foram postos.
- Restaurante norte corrigido pela referência própria (telha marrom, painel do oitão, faixa com o nome).
- Verificação independente: 55 diferenças (10 graves). Corrigidas na origem: cores que viravam oliva, recuos cortados, telhado largo demais, vão inexistente no TCE, composição da fachada da Oi. A lista do que continua diferente está no relatório.
- Estimados: todas as alturas, a posição e a altura das duas torres da Oi, os lados que o vídeo não mostra.


## Rodada 18 (12/11/2026) — pendências da frente norte e segunda verificação independente

Relatório: `r18/RODADA18.md`. **Não está 100 % fiel; nada foi jogado.**

- Peças refeitas com geometria própria: obra em esqueleto de concreto (segue pela R. Pedrinho Filho), pilone com furo e marquise curva da ESCON, grades em espinha de peixe, cobogó, passarela metálica na lateral do TCE, piso de cimento no lote da Oi.
- Achado: as alturas da especificação estavam infladas em três fachadas (ESCON × 0,72; casa 646 × 0,80; obra refeita). As outras 11 não foram reconferidas.
- Segunda verificação independente: 49 diferenças (2 graves, 20 médias). As 2 graves e 11 médias foram corrigidas; 4 médias em parte; 5 não. As correções finais não passaram por terceira verificação.
- A primeira verificação tinha descrito uma escada em zigue-zague no TCE; os quadros mostram uma passarela de um nível. Descrição de revisor também precisa ser conferida no quadro antes de modelar.


## Rodada 19 (12/11/2026) — alturas reconferidas e terceira verificação independente

Relatório: `r19/RODADA19.md`. **Não está 100 % fiel; nada foi jogado.**

- Erro da rodada 18 corrigido: o muro e a faixa azul ao lado do TCE tinham sido subidos para 4,2–5,5 m; o real tem ~2,6–3,5 m (placa de bicicleta, portão, porta e andares da torre concordam). A faixa do nome do TCE desceu para 2,55–3,50 m.
- Das 10 fachadas medidas, 7 conferem. Prédio branco e corretora corrigidos.
- Prédio antigo do pórtico: tapume 8,7 m atrás da linha do lote e alas do meio 6,2 m atrás da ala oeste (paralaxe + contorno do cadastro).
- Terceira verificação: 30 diferenças (4 graves). Duas graves corrigidas (laje inclinada da obra, janela da corretora); uma é do mapa de base (ciclovia ~2,5 m perto demais das fachadas); uma continua aberta (larguras do trecho leste do TCE, que vieram de uma escala de vídeo esticada).


## Rodada 20 (12/11/2026) — posições em planta do trecho TCE → prédio branco → ESCON

Relatório: `r20/RODADA20.md`. **Não está 100 % fiel; nada foi jogado.**

- Aérea Bing no nível 19 (0,30 m por pixel) baixada com autorização do dono; só conferência.
- Velocidade do carro no vídeo calibrada pelas ruas transversais (0,17 m por quadro entre a Coronel Mota e a Pedrinho Filho; a tabela antiga usava 0,202 e a velocidade varia dentro da quadra).
- Prédio branco ~4 m a oeste (x 116,7–124,6); muro do TCE 5,3 m; ESCON encostada no prédio branco (vão de 1,5 m; antes 5 m).
- Verificação independente: confirmou TCE, muro e prédio branco (±1,5 m); refutou um "anexo branco" que eu tinha posto por ler a aérea com 5 m de deslocamento, e a guinada da câmera (10 graus, não 23). Corrigido.
- Calçada e ciclovia: na cena (mapa do parceiro) há 4,1 m da guia ao muro; no real, 6,9 a 8,3 m. A correção tira uma faixa de rolamento da rua do parceiro: aguardando decisão do dono.


## Rodada 21 (12/11/2026) — frente sul da Av. Capitão Ene Garcez (x −91 a 332)

Relatório: `r21/RODADA21.md`. **Primeira versão, sem vídeo próprio; não está 100 % fiel; nada foi jogado.**

- 20 unidades modeladas (clínica, lojas, Roraima Energia, posto, Centro Espírita, entrada do hotel, lojas e prédios até a R. Cecília Brasil) no lugar dos volumes provisórios.
- Posição e largura pelo cadastro; o resto por consulta ao Street View (nada copiado); alturas estimadas.
- Verificação independente da primeira versão: 23 diferenças (7 graves); correções aplicadas; a versão corrigida só o autor conferiu.
- O gerador das fachadas serve aos dois lados (`BVC_LADO=sul`).
- Restos do mapa do parceiro soltos no ar na linha do antigo muro foram retirados pelo importador.


## Rodada 22 (12/11/2026) — calçada, ciclovia e canteiro do lado norte na largura real

Relatório: `r22/RODADA22.md`. **Não está 100 % fiel; nada foi jogado.**

- Autorizado pelo dono: o chão do mapa da praça entre a pista e o muro (x −100 a 333) sai na importação e entra o marco `BVC_PDA_FAIXA_NORTE`: calçada 2,76 m, ciclovia 2,80 m (10 cm acima da pista, entre guias), canteiro 1,90 m, guia da pista a 7,9 m do muro (antes 4,1 m).
- Postes, árvores, capim, placas e motos do antigo canteiro foram levados 3,15 m para o canteiro novo; o mapa original do parceiro não foi alterado.
- Verificação independente: nenhuma diferença grave; 8 médias (7 corrigidas) e 7 leves. A versão corrigida só o autor conferiu.
- Abertas: árvores no canteiro (no vídeo não há), mureta que avança sobre a calçada em x ≈ 125, placas que deveriam ficar na calçada.


## Rodada 23 (12/11/2026) — canteiro norte sem as árvores do mapa da praça

Relatório: `r23/RODADA23.md`. A pedido do dono, as 14 árvores de rua do mapa da praça que ficavam no canteiro norte são desligadas na importação (no vídeo não há árvore ali). A mureta da rampa da ESCON, que avançava sobre a calçada, termina na linha do lote. Nada foi jogado.


## Rodadas 24 e 25 (12/11/2026) — lado sul fora da praça, até o Cine Super K

Relatório: `r25/RODADA25.md`. **Não está 100 % fiel; nada foi jogado.**

- Marco novo `BVC_EG_SUL_OESTE` (x −1249 a −176): perímetro do 6º BEC, portaria com monumento (trator só em volume), anexo e Quartel do Comando Geral da PM, terreno murado, sobrado, muro rosa e Cine Super K.
- No marco `BVC_PDA_FRENTE_SUL`: muro azul a oeste da Av. Terêncio Lima e, a leste da R. Cecília Brasil, terreno cercado, escola, lanchonete, estacionamento e prédio cinza (até x 617).
- Fontes: cadastro + consulta ao Street View + aérea já baixada; alturas estimadas pela perspectiva (±10 %).
- Verificação independente da primeira versão: 3 graves (gradil do batalhão alto demais; escola e prédio cinza feitos paralelos à avenida, quando são girados ~19° e ~22°), 6 médias e 6 leves, todas tratadas. A versão corrigida só o autor conferiu.
- Sem vegetação; relevos e bonecos do cinema deixados de fora de propósito.


## Rodada 26 (12/11/2026) — árvores e sebes do lado sul

Relatório: `r26/RODADA26.md`. 129 itens de vegetação clonados dos modelos do mapa do Centro Cívico (raiz `BVC_Arvores_Sul`), nas posições de `06_unity/arvores_sul.json`: fileira do batalhão pelos troncos medidos no Street View de 2025, escola, muro azul e terreno murado; sebes no gerador. A primeira versão (só pela aérea antiga) saiu com árvores grandes demais e a escola errada; foi refeita com as medidas do revisor independente. Espécie não confirmada. Nada foi jogado.


## Rodada 27 (12/11/2026) — telhados dos volumes provisórios do mapa inteiro

Relatório: `r27/RODADA27.md`. 14.446 volumes provisórios ganharam telhado inclinado pela classe lida na aérea: telha de barro em quatro águas, fibrocimento em duas, cobertura clara plana; contornos encostados da mesma classe ganham um telhado só; a altura total do cadastro é mantida; os túmulos do cemitério viraram lápides. A forma é estimada por regra (a primeira versão, toda em quatro águas, foi refeita pelo laudo independente). Nada foi jogado.


## Rodada 28 (12/11/2026) — frente norte da avenida a oeste da praça

Relatório: `r28/RODADA28.md`. Marco novo `BVC_EG_NORTE_OESTE` (x −1216 a −143): laboratório, casa antiga, prédio de testeira azul, Câmara Municipal, CGU, muro branco, Ginásio da Guarnição, portaria, muro e quatro residências, sede administrativa de cinco pavimentos, muro de tijolo, cobertura metálica, casa branca e FUNASA. Fontes: cadastro + consulta ao Street View. O laudo independente achou 5 erros graves na primeira versão (planta da sede, FUNASA com um pavimento a mais, muro alto demais, caixa falsa na calçada, prédio azul perto demais da rua), todos tratados; a versão refeita só o autor conferiu. Nada foi jogado.


## Rodada 29 (13/11/2026) — Av. Jaime Brasil, x 1247 a 1541, os dois lados

Relatório: `r29/RODADA29.md`. Marcos novos `BVC_JB_NORTE` e `BVC_JB_SUL` (65 unidades, 17.698 triângulos). Método novo: levantamento medido antes de modelar (64 vistas), conversão automática, verificação nos 58 pares captura × cena tirados do mesmo ponto. O laudo achou 32 diferenças (9 graves), quase todas de plano de painel saliente; tratadas as que tinham medida. Faltam: canteiro central (quiosques, estacionamento, árvores), laterais das transversais, largo do mercado, declive da rua a leste. As pastas `game/_backup` (r11 a r28) e `game/_diag` sumiram em 13/11 por causa externa; recriados só a cena-base e o script de lote. Nada jogado.


## Rodada 30 (13/11/2026) — chão da rua, canteiro e transversais da Av. Jaime Brasil

Relatório: `r30/RODADA30.md`. Marco novo `BVC_JB_RUA` (17.892 triângulos): substitui o chão genérico da planta entre x 1247 e 1541 por um medido (guias retas, ilhas, vagas, faixas de pedestres, 8 quiosques, postes, árvores, 4 laterais de esquina) e segue pelas transversais até onde há medida. O laudo achou 24 diferenças (5 graves, de emenda e de esquina); nada espelhado, guias a 0,1–0,35 m do real. Pendente: ponta oeste (as guias do mapa do parceiro estão 2,1 a 2,6 m fora — decisão do dono), ponta leste, fim das transversais, largo do mercado. Nada jogado.


## Rodada 31 (13/11/2026) — emenda oeste da Av. Jaime Brasil

Relatório: `r31/RODADA31.md`. O importador passou a saber tirar o CHÃO do mapa do parceiro debaixo de um marco (`chao_fora` no `marcos_cena.json`; só na cena do BV City). A cunha do Centro Cívico que entrava em diagonal no trecho medido (x 1247,95–1256) saiu. A emenda ainda não bate: a quadra vizinha inteira (até a transversal em x ≈ 1150–1170) é diferente da real, com um prédio do lado sul 5 m para dentro da rua. Refazer a quadra depende de decisão do dono. Nada jogado.
