# BV City — Rodada 17 (12/11/2026)

Pedido do dono: *"modelar o TCE e todas as outras estruturas, casas, placas etc. deixando 100% fiel à vida real, verifique também os que já foram feitos para corrigi-los e melhorá-los"*.

**Resposta curta e honesta: não está 100 % fiel.** Esta rodada pôs na cena o Tribunal de Contas com modelo próprio, o prédio da Oi com as duas torres, as fachadas revistas com placas, abrigo e totens, e corrigiu os restaurantes. Uma verificação independente comparou tudo com o vídeo e achou **55 diferenças (10 graves)**. As de causa geral e as mais visíveis foram corrigidas na mesma rodada; a lista do que **não** foi corrigido está na seção 4. **Nada foi jogado** (sem build, por decisão do dono).

## 1. O que entrou na cena

| Peça | Marco | Triângulos | Fonte da forma |
|---|---|---|---|
| Tribunal de Contas | `BVC_PDA_TCE` | 2.998 | vídeo próprio (58 a 67 s) + contorno do OpenStreetMap |
| Prédio da Oi, anexos, gradil, torre de concreto, torre treliçada, bloco técnico | `BVC_PDA_OI` | 4.112 | vídeo próprio (0 a 12 s) + contorno do OSM + aérea (consulta) |
| 14 fachadas da frente norte, revistas | `BVC_PDA_FRENTE_NORTE` | 18.328 | vídeo próprio; especificação v2 |
| Restaurantes da Praça Velia Coutinho (correção) | `BVC_PDA_RESTAURANTES` | 5.924 | referência própria `tools/pda_ref/streetview/010` |

- **TCE** (`03_blender/tce.py`): tambor curvo com pano de pastilha, cinta em balanço, anel de testeira azul com topo inclinado, faixa azul-royal com o nome, térreo recuado de vidro com pilares de pedra, três mastros, torre recuada, portão de enrolar, muro branco contínuo até o vizinho. Substitui a fachada genérica da rodada 16, que tinha ficado pior que a foto.
- **Oi** (`03_blender/oi.py`): o cadastro dá o **lote inteiro** como um polígono só (3.822 m²). O mapa do parceiro tinha ali um galpão genérico em L; ele saiu (13.542 triângulos de 7 malhas e 355 de 4 colisores; o chão ficou). O bloco alto, o corpo branco, o anexo verde-claro, o anexo leste e o gradil ficam na aresta da frente do lote, que é girada 23,3° em relação à avenida.
- **Fachadas** (`03_blender/frente_norte_3d.py`): três agentes, um por vez, reviram as 14 fachadas contra a imagem do vídeo e gravaram correções (`fachadas_spec.json`, v2; a v1 ficou em `fachadas_spec_v1.json`). O gerador passou a ler profundidade própria de cada volume, travessas, janela basculante, altura da mureta e cor da grade.
- **Mobiliário levantado no vídeo: 54 itens; postos 12** — 9 placas de trânsito, 2 totens (só o nome, sem logotipo) e o abrigo de ônibus. **Não postos 42**: 16 postes de energia, 2 de luz, 5 árvores e 2 palmeiras (a calçada do mapa da praça já tem os seus, em posições próprias; pôr outros duplicaria), 7 canteiros, 5 lixeiras e 5 itens diversos.
- **Terreno vazio e muro velho** (x 342 a 483,5): mureta com tela e muro velho, como no vídeo.
- **Restaurante norte**: telhado passou de laranja vivo a marrom-escuro, o oitão ganhou o painel amarelo com a palavra RESTAURANTE, as colunas ganharam as cores da referência e a borda da laje ganhou a faixa com o nome da loja (fonte genérica).
- **Mini Terminal**: conferido contra o catálogo próprio (`unity/equipe/pda_referencias.md`, item T16). A laje em asa sobre fechamento de vidro com montantes amarelos já estava feita; **sem mudança**.

## 2. Medido × estimado

| Item | Situação |
|---|---|
| Contorno do TCE e do lote da Oi (OpenStreetMap) | MEDIDO (±1 m) |
| Posições ao longo da frente (TCE e fachadas), lidas nos quadros | MEDIDO no vídeo (±1 m) |
| Aresta da frente da Oi: 34,6 m, giro de 23,3° | MEDIDO no cadastro |
| Bloco alto da Oi começa a 13 m da ponta oeste da aresta | MEDIDO no vídeo (±2 m): ângulo de 28,8° no quadro `q_00345` |
| Profundidade do bloco da Oi (14,5 m) | MEDIDO na aérea (±1,5 m) |
| Base da torre de concreto em (552 ; −108) | ESTIMADO (±3 m): encontro do corpo com a sombra na aérea + direção no vídeo |
| Altura da torre de concreto (65 m) | ESTIMADO (catálogo próprio: 60 a 70 m) |
| Torre treliçada: posição (544,5 ; −78) e altura (30 m) | ESTIMADO (direção no quadro `q_00110`; altura ≥ 15 m) |
| Bloco técnico branco ao pé da torre | ESTIMADO (visto nos quadros; posição e tamanho não medidos) |
| Todas as alturas de prédio, recuos, espessuras, inclinação de telhado | ESTIMADO (±20 %) |
| Lado mais alto do anel de testeira do tambor do TCE | ESTIMADO |
| Lados que não aparecem no vídeo (norte e parte do leste/oeste) | ESTIMADO: parede lisa |
| x do carro nos primeiros 12 s do vídeo (trecho da Oi) | ESTIMADO: extrapolado; o primeiro ponto de controle é a esquina em x = 488,8 |

## 3. Verificação independente

Dois revisores, um por vez, só lendo imagens, compararam o quadro do vídeo com a foto da cena (`09_relatorios/verificacao_r17.json`): **55 achados — 10 graves, 29 médios, 16 leves.**

**Corrigido nesta rodada (r17b), na origem:**

1. **Brancos e cremes saíam bege-oliva em todas as fachadas** (grave, geral). Causa: o gerador arredondava cada canal de cor a 8 níveis e um creme virava oliva. Agora arredonda em matiz, saturação e valor.
2. **Volumes do fundo do lote apareciam na frente** (grave): o recuo era cortado em 1,5 m. Uma casa 16 m atrás da loja de seguros virava uma caixa alta na calçada. O recuo agora vale até 30 m; quando há muro na frente, a casa recua e o muro fica no alinhamento.
3. **Telhado em forma de asa no sobrado** (grave): o telhado cobria a frente inteira do lote. Agora cobre só os corpos altos, com beiral de no máximo 0,8 m.
4. **Telhado do prédio antigo (x 262) tinha piorado** (grave): voltou a ser telha escura; as janelas, que estavam de cores diferentes, ficaram iguais. Telha de barro das fachadas passou a ser a marrom envelhecida.
5. **ESCON em azul-petróleo** (grave): passou a azul-royal na parede e no totem.
6. **TCE**: o vão que não existe entre o portão e o prédio vizinho foi fechado com muro branco e a faixa azul contínua (grave); a pastilha passou a azul-lilás claro; a cúpula virou anel de testeira com topo inclinado; o branco foi clareado.
7. **Oi**: 4 módulos de painel vermelho (eram 5) com painel opaco em cima (eram janelas); faixa branca cega entre o janelão e a fita alta; cinta de placas cinza-claro com pilares brancos; corpo branco de dois níveis e anexo de parede verde-clara (era uma vitrine); anexo leste creme com três venezianas; torre treliçada trazida para logo atrás do anexo, com antenas; segunda coluna de janelas e antenas na torre de concreto; bloco técnico ao pé dela.
8. **Lixeiras** que eu tinha posto (3) saíram. Os sacos de lixo pretos que o revisor viu na calçada **não eram elas**: são objetos do mapa da praça, já estavam na foto de antes e continuam lá.

**Não corrigido (continua diferente do real):**

- **Brancos ainda saem bege-claro nas fotos do Unity.** A correção tirou o tom oliva e clareou, mas a parede branca continua mais escura que no vídeo; a causa restante é a luz e a exposição da cena, que não foram mexidas.

- Obra ao lado da casa branca (x −5): no real é um esqueleto de concreto com andaimes vermelhos; na cena são volumes sólidos. Grave.
- Totem alto da ESCON (lâmina curva com furo) e marquise cilíndrica: continuam prisma e caixa. Grave.
- Casa branca moderna: pórtico com fundo escuro, guarda-corpo de vidro opaco, portão com fundo escuro.
- Casa térrea das grades (x 34): vãos pequenos e grade reticulada; no real são vãos do piso ao beiral com grade em zigue-zague.
- Sobrado: varanda do 2º piso virou painel; muro de tijolo incompleto.
- Lojas: "2x1" pequeno e grade na vitrine da pizzaria; painel de comida é um retângulo liso; caixa clara entre duas lojas.
- Prédio vizinho do TCE (DIPLE): três caixas de alturas diferentes em vez de um volume de dois pavimentos; sem cobogós.
- TCE: escada de incêndio lê como lajes; janelas da torre não dobram a esquina; muro oeste curto; sem o emblema e a tarja do letreiro (logotipos não são reproduzidos).
- Oi: o pátio continua com o piso vermelho do mapa do parceiro (no real é cimento claro); a calçada em frente tem grama e ciclovia do mapa da praça que não existem ali; sem cerca elétrica nem portão azul.
- Restaurante: a varanda de cima lê como parede de tijolo iluminada (no real fica na sombra); sem o emblema do oitão.
- Muro com pintura verde (x −55), muro de bloco (x −30), tapumes (cor e ondulado), placa de obra.
- Letreiros: todos em fonte genérica, sem logotipo — isto é decisão, não defeito.

**O que os revisores não conseguiram julgar:** planta e fundos do TCE, posição e altura das torres (não há imagem real de comparação), lado oeste da Oi (tapado por árvores), e o que carros, árvores e contêineres do jogo tapam.

As correções da r17b **não passaram por segunda verificação independente**; conferi eu nas folhas e nas fotos novas.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto vazio em 95.200; colisores novos: TCE 117, Oi 28, frente norte 146.

## 4. O que continua sem fazer

1. **Nada foi jogado.** Sem build.
2. **Frente sul da avenida: não há vídeo.** Continua volume provisório. O catálogo próprio lista o que existe (clínica, açaí, churrascaria, Roraima Energia, posto, centro espírita, imobiliária), mas só com Street View de consulta.
3. Muro pintado do governo a leste da Oi (x 570 a 585) e o prédio seguinte: não modelados.
4. Cinco contornos da frente norte ficaram fora da rodada 16 e continuam fora.
5. Postes, árvores, canteiros e lixeiras do vídeo não foram postos (seção 1).
6. `IMG_4196.MOV` (Sicredi, 1,3 km a oeste) não foi usado.
7. Defeitos de desenho da praça medidos na rodada 15 continuam.
8. A frente norte passou de 113 para 153 materiais com a nova redução de cores: mais chamadas de desenho. Não medi o custo.
9. Commit: nenhum.

## 5. Arquivos

- Geradores: `03_blender/tce.py`, `oi.py`, `frente_norte_3d.py` (cópias `.v2`, `.r17a`), `frente_norte_letreiros.py`, `restaurantes.py` (cópia `.r16`), `bvc_texturas.py`; texturas novas em `04_texturas/gera_r17.py` (`telha_marrom`, `letreiro_restaurante`, `letreiro_bobs`) e `pastilha_lilas`.
- Especificação: `02_referencias/video_pda_20261111/fachadas_spec.json` (v2 + r17b; `_v1` e `_v2` guardadas).
- Unity: `06_unity/marcos_cena.json` (marcos `BVC_PDA_TCE` e `BVC_PDA_OI`, este com o lote como casco), `modelados.geojson` (19 contornos), `tce_pose.json`, `oi_pose.json`, câmeras `r17_*` no importador (cópia de antes em `_backup/bvcity_r15_20261111/FBBVCityPlantaImporta_antes_r17oi.cs`).
- Comparações: `07_comparacoes/r17/` (`r17a/` = antes das correções da verificação), `06_unity/compara_r17.py`.
- Verificação: `09_relatorios/verificacao_r17.json`.
