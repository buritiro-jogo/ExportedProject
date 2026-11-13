# BVC_CCV_CATEDRAL — Catedral Cristo Redentor

> **Estado: DESTRAVADA E MODELADA (10/11/2026).** A planta, que era o bloqueio desde a rodada 2,
> foi resolvida — e **sem usar imagem do Google**. O caminho está na seção 2.

| | |
|---|---|
| ID | `BVC_CCV_CATEDRAL` |
| Local | centro **x 797,3 / y 163,6** do corredor (≈ 2,819624 N, −60,673373 W) |
| Planta | OpenStreetMap way 549581863 `building=church` (ODbL), **53 vértices**, registrado sobre ortoimagem |
| Fachada | Wikimedia Commons: 4 de **Jason R Nascimento** (CC BY-SA 3.0), 1 de **Rogério Regis** (CC BY-SA 3.0), 1 de **MiltonRamon** (CC BY-SA 4.0); aérea oblíqua **CC0** de Dieny Portinanni Araujo Cavalcante |
| Arquivos | `03_blender/catedral.py`, `BVC_CCV_CATEDRAL.blend`, `05_exportacoes/GLB/BVC_CCV_CATEDRAL.glb` |
| Comparação | `07_comparacoes/catedral_01_foto_x_novo.jpg`, `catedral_02_volume_e_planta.jpg` |

## 1. O que estava travado

Na rodada 3 eu disse que o `building=church` do OSM "não é o prédio": um contorno em "C" de 53 vértices
que não batia com nenhuma foto. **Estava errado pela metade.** O contorno *é* o prédio — ele estava
apenas **deslocado 6,5 m para oeste**, e eu não tinha ortoimagem contra a qual medir esse deslocamento.
A reentrância em "C", que me pareceu inexplicável, é a **fachada côncava** — exatamente o que a foto
frontal mostra.

## 2. Como a planta foi resolvida, sem imagem restrita

A autorização era para medir em captura de satélite do Google. **Não precisei dela.** Antes de abrir
imagem restrita procurei fonte reutilizável de alta resolução:

| Fonte testada | Resultado |
|---|---|
| **Esri World Imagery** (REST público) | só cobre Boa Vista até **z17 = 1,19 m/px**; acima disso devolve a imagem "Map data not yet available" |
| OpenAerialMap | **sem cobertura** de Boa Vista (já verificado na rodada 3) |
| Sentinel-2 / CBERS | 10 m/px e 2 m/px — grosseiro demais para planta de 35 m |
| **Aérea da Bing (Microsoft)** | **z19 = 0,298 m/px, imagem real e nítida** sobre a quadra |

A aérea da Bing é a mesma camada que os editores do OpenStreetMap usam: a **Microsoft concedeu ao
OpenStreetMap permissão expressa para digitalizar construções a partir dela**, que é o tipo de
"levantamento autorizado" que o briefing pede. Baixei **25 tiles** (a quadra e o entorno imediato),
montei o mosaico e o georreferenciei pela própria malha Web Mercator — escala exata, sem barra de
escala para ler; só a resolução da imagem (0,298 m/px) limita a precisão.

**A geometria do asset continua vindo do OSM (ODbL).** A ortoimagem serviu para medir três números —
o deslocamento e o giro que colocam esse contorno no lugar certo.

## 3. O registro, medido

O contorno foi alinhado maximizando o **gradiente da imagem ao longo das arestas do polígono**
(o critério por área não serve: a mancha clara inclui o adro, então o IoU satura em 0,51 e não
discrimina).

| | |
|---|---|
| Deslocamento | **dE −6,5 m, dN −0,5 m**, giro **+1,0°** |
| Gradiente médio sob as arestas | **0,362** contra **0,115** sem corrigir (**+215 %**) |
| Nitidez do pico | cai para 0,294 a 1 m, 0,230 a 2 m, 0,180 a 4 m |
| Área resultante | **881 m²**, 53 vértices |
| Caixa orientada | **47,8 × 34,8 m**, eixo no rumo 228,7° |

Script: `01_georef/scripts/cat_registro2.py`. Prancha: `02_referencias/sat/osm_registrado.png`.

**Conferência independente da ortoimagem:** sobrepus os eixos de via do OSM à aérea. Um otimizador
automático de "cara de asfalto" pediu deslocar as vias em −7,0/+7,5 m, mas a prova visual
(`conf_rua.png`) mostra que ele estava errado — deslocadas, as vias caem sobre os carros estacionados
e o canteiro; sem deslocar, os dois eixos da rua entre a praça e a Catedral caem na rua. **Adotado:
sem deslocar.** O critério tinha sido puxado pelas áreas pavimentadas grandes, que também são cinza.

## 4. Qual lado é a fachada

Essa era a segunda incerteza, e ela se resolve por três evidências que concordam:

1. **A reentrância em "C" do contorno abre para leste-sudeste** (normal no rumo **103,8°**).
2. **O tambor cilíndrico** (Ø 8,8 m medido na ortoimagem, centro E 493,5 / N −659,9) fica **ao sul**
   da reentrância. Na foto frontal ele aparece **à esquerda** de quem olha — o que só fecha se o
   observador estiver a leste, olhando para oeste.
3. **O adro largo pavimentado** está a leste (E 490–520 na ortoimagem). Os degraus e a marquise da
   foto dão para esse lado.

A corda da fachada tem **19,71 m** entre as pontas, com **4,0 m de flecha** para dentro.

## 5. Dimensões

| Grandeza | Valor | Origem | Confiança |
|---|---|---|---|
| Contorno (53 vértices) | 881 m², caixa orientada 47,8 × 34,8 m | OSM + registro na ortoimagem | **MEDIDO** |
| Corda da fachada | 19,71 m, flecha 4,0 m | idem | **MEDIDO** |
| Tambor (capela) | Ø 8,80 m | idem | **MEDIDO** |
| Rumo da fachada | 103,8° | idem | **MEDIDO** |
| Topo da base envidraçada | 4,20 m | proporção na foto noturna | **ESTIMADO** |
| Travessa entre as bandas de brise | 8,80 m | idem | **ESTIMADO** |
| Borda do telhado sobre a fachada | 14,80 m no meio, 18,60 m nas pontas | idem | **ESTIMADO** |
| Altura do campanário | 37,30 m | valor que já consta no mapa do parceiro | **ESTIMADO** |
| Altura do tambor | 9,80 m | proporção na foto | **ESTIMADO** |
| Altura da vela | 20,20 m | proporção na foto | **ESTIMADO** |

Régua das fotos: a **pessoa em pé** na entrada, na foto noturna de Jason R Nascimento — 1,70 m.

## 6. Elementos levantados e modelados

1. **Fachada côncava** com a borda superior em curva — baixa no meio, subindo nas duas pontas.
2. **Brise-soleil vertical em duas bandas**, separadas por travessa; a banda alta **acompanha a curva
   do telhado**, e é isso que dá a identidade do prédio.
3. **Vitrais** azul, amarelo e branco atrás dos brises e na base.
4. **Base envidraçada contínua** com montantes.
5. **Marquise elíptica** (14,2 × 6,6 m) sobre a entrada, em três pilares cilíndricos.
6. **Campanário** de 37,3 m: lâmina estreita de frente (2,4 m) e profunda de lado (5,2 m), com
   **vazio em cruz** no alto e a caixa saliente que aparece na foto de Rogério Regis.
7. **Tambor cilíndrico** com vitral na base e cornija.
8. **Vela** — lâmina curva de concreto na ponta direita da fachada, subindo acima do telhado.
9. **Ala baixa** ligando a fachada ao tambor, envidraçada.
10. **Casca do telhado em leque**, com fáscia branca saliente, caindo da fachada para a ponta distante.
11. Escadaria de três degraus, jardineiras, guarda-corpo nas pontas do patamar.

## 7. Modelo

| Camada | Triângulos | Conteúdo |
|---|---|---|
| C1 — volumetria | 488 | paredes da nave, fáscia, casca do telhado, ala, tambor |
| C2 — arquitetura | 1.174 | vitrais, pano do topo, vela, marquise, campanário, cornijas, escadaria |
| C3 — microdetalhe | 2.760 | 144 brises, travessa, montantes, cruz do campanário, balizas, jardineiras |
| **total** | **4.422** | |

## 8. Contra o que está no jogo

| | Foto | Modelo novo | No jogo |
|---|---|---|---|
| Triângulos | — | **4.422** | **2.048** |
| Campanário | lâmina de 37 m com cruz vazada | 37,3 m, duas lâminas, cruz | `A_concreto_torre_bege`, **10 triângulos** (prisma de 1,8 × 1,8 m) |
| Brises | duas bandas seguindo a curva | 144 peças | 40 triângulos |
| Marquise elíptica | sim | modelada | **não existe** |
| Tambor | sim | Ø 8,8 m medido | **não existe** |
| Vela | sim | modelada | **não existe** |
| Vitral | azul/amarelo/branco | 72 painéis | `A_vitral_azul`, 208 tri |

## 9. Não verificado

1. **Todas as alturas** são estimadas em proporção de foto. Nenhuma foi medida em campo.
2. **As fachadas norte, oeste e sul** — nenhuma foto. O modelo as trata como pano branco liso.
3. **O interior** não foi modelado.
4. **A posição exata do campanário** foi adotada no entalhe do contorno junto à ponta sul da fachada,
   coerente com as fotos, mas o entalhe é pequeno e a ortoimagem não separa a torre da sombra.
5. **A cobertura** é modelada como casca única caindo da fachada; a aérea oblíqua mostra uma
   superfície levemente curva, e a curvatura real não foi medida.
6. **O desenho dos vitrais** é padrão geométrico aproximado, não o desenho real.
7. **Precisão da planta:** limitada pela resolução da ortoimagem (0,298 m/px) e pela nitidez do pico
   de registro — da ordem de **±1 m**.

## 10. Créditos obrigatórios

Entram em `docs/CREDITOS_TERCEIROS.md`:

- Contorno: **© colaboradores do OpenStreetMap**, ODbL 1.0.
- Fotos de fachada: Jason R Nascimento (CC BY-SA 3.0), Rogério Regis (CC BY-SA 3.0),
  MiltonRamon (CC BY-SA 4.0), Dieny Portinanni Araujo Cavalcante (CC0).
- Imagem aérea de referência: **aérea da Bing / Microsoft**, usada para medir o registro do contorno.
  Nenhum pixel dela entra no jogo.
- A Catedral é obra permanentemente situada em logradouro público (Lei 9.610/98, art. 48).
