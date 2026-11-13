# BVC_CCV_CULTURA — Palácio da Cultura Nenê Macaggi

| | |
|---|---|
| ID | `BVC_CCV_CULTURA` |
| Local | centro **x 926,0 / y −176,5** do corredor (≈ 2,82111 N, −60,67047 W) |
| Planta | **traçada na ortoimagem aérea da Bing z19** (0,298 m/px): retângulo **38,0 × 33,0 m** |
| Fachada | Wikimedia Commons, *"Palácio da Cultura"* (panoramio), **Rogério Regis, CC BY-SA 3.0** — foto noturna da entrada |
| Arquivos | `03_blender/cultura.py`, `BVC_CCV_CULTURA.blend`, `05_exportacoes/GLB/BVC_CCV_CULTURA.glb` |
| Comparação | `07_comparacoes/cultura_01_foto_x_novo.jpg`, `cultura_02_planta.jpg` |

## 1. Por que a planta não veio do OSM

O OSM tem o `way 1332740171` (`amenity=townhall`, `building=yes`, nome "Palácio da Cultura"), 7 vértices,
**1.329 m²**. Tentei o mesmo caminho que destravou a Catedral — registrar o contorno pelo gradiente de
borda — e **não funcionou aqui**:

- o ajuste de um prédio só deu dE −11,5 / dN −4,0 / giro −3,0°, mas o pico é fraco (0,307 contra 0,144)
  e o telhado é **nervurado**, o que cria uma porção de falsos picos paralelos;
- ajustando os **nove prédios da quadra de uma vez**, o deslocamento comum ficou em dE −9,5 / dN −1,0,
  mas prédio a prédio os melhores individuais variaram de dE +15 a −12 — ou seja, **o critério não é
  confiável nesta quadra**;
- a prova visual mostrou o porquê: os **Correios**, ao lado, encaixam **sem deslocamento nenhum**, logo
  o OSM daqui não tem deslocamento sistemático; o polígono do Palácio é que foi **desenhado largo**, e
  o entalhe que ele traz está no canto **oposto** ao que a imagem mostra.

Então a planta foi **traçada diretamente na ortoimagem**, no referencial do corredor — onde o prédio
aparece alinhado à malha da cidade e os limites podem ser lidos na grade de 1 m.

| | Valor |
|---|---|
| Retângulo | **x 907..945 / y −193..−160** do corredor = **38,0 × 33,0 m = 1.254 m²** |
| Concordância com o OSM | área **6 %** menor que os 1.329 m² do OSM; posição dentro de **~3 m** depois do registro do conjunto |
| Rumo da fachada | **221,8°** (sudoeste) — alinhada à malha da Ene Garcez |

Precisão: **±2 m**, limitada pela resolução da imagem e pelas árvores que encostam no prédio.

## 2. Qual lado é a fachada

A foto mostra a entrada diante de uma **praça pavimentada com palmeiras**. Na ortoimagem, a área aberta
e clara está do lado **y = −160, para sudoeste**; nos outros três lados há árvores (noroeste e nordeste)
e o bloco vizinho dos Correios (noroeste). A fachada é, portanto, a face sudoeste.

Com a fachada aí, a leitura da foto fecha: quem olha de fora tem a **ala envidraçada à esquerda**
(x 907..929) e a **parede cega de pastilha à direita** (x 929..945) — e esse trecho da direita é
justamente onde a ortoimagem mostra uma **área escura**, isto é, pátio ou terraço de um pavimento
atrás de uma parede-biombo. É o que a foto mostra: uma parede alta, sem janelas de verdade, só com
três frestas horizontais, servindo de suporte ao letreiro.

## 3. Dimensões

| Grandeza | Valor | Origem | Confiança |
|---|---|---|---|
| Planta | 38,0 × 33,0 m | ortoimagem Bing z19 | **MEDIDA** |
| Rumo da fachada | 221,8° | idem | **MEDIDO** |
| Pé-direito do térreo | 4,60 m | proporção na foto | **ESTIMADO** |
| 2º e 3º pavimentos | 4,00 m cada | idem | **ESTIMADO** |
| Altura do bloco principal | 12,60 m (+0,75 de platibanda) | idem | **ESTIMADO** |
| Altura da parede de pastilha | 11,80 m | idem | **ESTIMADO** |
| Laje saliente sobre a entrada | cota 9,00 m, 0,95 de espessura, 3,40 de balanço | idem | **ESTIMADO** |
| Letras "Nenê Macaggi" | 1,05 m | idem | **ESTIMADO** |
| Letras "Palácio da Cultura" | 0,42 m | idem | **ESTIMADO** |
| Frestas de vidro | 0,90 × 4,40 m, três | **contadas** na foto | **CONTADAS** |

## 4. Elementos levantados e modelados

1. **Parede cega revestida de pastilha** cinza-azulada, com remate branco no topo.
2. **Letreiro em duas linhas**: "Palácio da Cultura" (fina, em cima) e **"Nenê Macaggi"** (grande, embaixo),
   em letras metálicas salientes.
3. **Três frestas horizontais** de vidro escuro com moldura alaranjada, nas posições da foto.
4. **Ala envidraçada recuada** sob a laje: pano de vidro escuro em grelha fina, montantes a cada 1,45 m
   e três travessas.
5. **Laje saliente** branca sobre a entrada, com **forro vermelho**.
6. **Duas faixas vermelhas** com brises verticais — uma alta, sobre a laje, e uma no meio do pano de vidro.
7. **Faixa de vidro iluminada** na base da parede de pastilha (o térreo ocupado atrás dela).
8. **Escadaria e rampa** de acesso em lajota vermelha, com guarda-corpo metálico e corrimão contínuo.
9. **Jardineiras** com arbustos ao pé da parede, **três palmeiras** e o **poste ornamental** escuro.
10. **Pátio/terraço** no quadrante sudeste, com guarda-corpo, atrás da parede-biombo.
11. Pano de vidro também na face noroeste do bloco principal.

## 5. Modelo

| Camada | Triângulos | Conteúdo |
|---|---|---|
| C1 — volumetria | 84 | bloco principal, bloco do fundo, bloco baixo, parede-biombo, platibanda |
| C2 — arquitetura | 240 | laje saliente e forro, panos de vidro, faixas vermelhas, frestas, escada, rampa, guarda-corpo do terraço |
| C3 — microdetalhe | 1.997 | montantes e travessas, 58 brises vermelhos, 30 letras, prumos e corrimão, jardineiras, palmeiras, poste |
| **total** | **2.321** | |

## 6. Contra o que está no jogo

| | Foto | Modelo novo | No jogo |
|---|---|---|---|
| Triângulos | — | **2.321** | **≈ 1.220** |
| Parede de pastilha | sim, com letreiro | modelada, 11,8 m | volume sem revestimento próprio |
| Letreiro | duas linhas | 30 letras modeladas | não existe |
| Frestas | três | três, com moldura | não existem |
| Ala envidraçada | grelha fina, dois panos | modelada com montantes | não existe |
| Faixas vermelhas | duas, com brises | 58 brises | não existem |
| Rampa e escada | sim | modeladas | não existem |

## 7. Não verificado

1. **Todas as alturas** são proporção de foto noturna, sem régua confiável no quadro (não há pessoa nem
   objeto de escala conhecida). São as medidas mais frágeis deste levantamento.
2. **O que existe no quadrante sudeste**: a ortoimagem mostra área escura com caminhos cruzados. Adotei
   **pátio sobre bloco de um pavimento**. Pode ser laje escura, vegetação ou pátio aberto no térreo.
3. **As três outras fachadas** — sem foto. O modelo as trata como pano liso com uma faixa de vidro na
   face noroeste.
4. **A tipografia do letreiro**: as letras são barras de proporção aproximada, não o desenho real.
5. **O tom exato da pastilha**: tirado de foto noturna com luz artificial; a cor de dia pode diferir.
6. **O número de pavimentos**: adotei três pela proporção da foto; o OSM não traz `building:levels`.
7. **A posição e o número das palmeiras e jardineiras** são composição a partir da foto, não levantamento.

## 8. Créditos obrigatórios

- Foto de fachada: **Rogério Regis**, CC BY-SA 3.0, via Wikimedia Commons.
- Imagem aérea de referência: **aérea da Bing / Microsoft**, usada para traçar a planta. Nenhum pixel
  dela entra no jogo.
- Contorno de conferência: **© colaboradores do OpenStreetMap**, ODbL 1.0.
- O Palácio da Cultura é obra permanentemente situada em logradouro público (Lei 9.610/98, art. 48).
