# BV City — Rodada 16 (11/11/2026)

Três frentes: o muro da arena saiu e as ruas transversais abriram; o Mini Terminal foi refeito; e o vídeo que vocês filmaram hoje virou fachada no jogo.

**Nada foi jogado** (sem build). Google Maps / Street View continuam só consulta: nada deles entrou no jogo. O vídeo é material nosso e entrou como textura e como medida. Todo número é MEDIDO, salvo quando marcado ESTIMADO ou OBSERVADO.

## 1. Muro da arena e bocas de rua (15d)

- O muro de perímetro (`PA_Muro_Perimetro`, contínuo em y −48,5 e +47,5) foi desligado. Ele não tinha colisor à parte: a colisão era a própria malha, que saiu junto.
- Dez ruas reais cruzam a borda da praça. Sete já coincidiam com as aberturas da calçada do mapa; três estavam deslocadas (Alfredo Cruz 9 m, Cerejo Cruz 7 m, Ajuricaba 3 a 5 m). Agora cada uma tem um recorte na zona, no x real da rua, da borda até 0,7 m para dentro do meio-fio da avenida: ali sai a calçada do mapa e entra a rua da planta.
- Nas bocas saíram 50 objetos que ficariam no meio da rua (poste, árvore, placa) e 6 malhas grandes foram recortadas.
- A planta ganhou uma face lateral ("saia") onde a calçada é cortada pela borda da zona: sem ela via-se o céu por baixo da laje. 3.656 triângulos.
- A fileira de prédios da frente: eu tinha dito que ficaria com buracos. **Eram só 6 volumes** na borda da praça (os outros 310 retirados são da borda do Centro Cívico). Os 6 voltaram.
- Os decalques de desgaste do muro e dos prédios genéricos que saíram ficavam **flutuando no ar** (defeito antigo, visto agora nas fotos). Foram recortados para dentro da linha do muro.

**A verificação independente desta parte não terminou**: a sessão caiu com o primeiro agente no meio. O que está dito aqui foi conferido por mim nas fotos de cima e do chão, não por terceiro.

## 2. Mini Terminal Luiz Canuto Chaves (15e)

Na cena havia, no lugar dele, um bloco alto azul-claro com base de blocos cinza, do mapa antigo. Não tinha nada do prédio real.

| Peça | Planta | Composição | Alturas |
|---|---|---|---|
| Corpo branco com faixa laranja | contorno do OpenStreetMap (1.833 m²) conferido na aérea: MEDIDO ±1 m | faixa laranja larga, janelas em fita de caixilho branco, cobertura branca: OBSERVADO no Street View (fev/2025), 4 lados | parede 3,30 m, topo 4,50 m: ESTIMADAS |
| Lanternim laranja com placas solares | lido na aérea: MEDIDO ±1,5 m | dois campos de placas | topo 6,10 m: ESTIMADO |
| Bloco cinza do letreiro | contorno: MEDIDO | letreiro laranja "TERMINAL LUIZ CANUTO CHAVES" nas faces norte e sul (fonte genérica) | 4,70 m: ESTIMADO |
| Saguão sob a "asa" | contorno e aérea: MEDIDO | laje cinza inclinada que sobe para o anel, vidro com montantes amarelos e brises brancos, paredes cinza nas pontas | 4,25 a 6,00 m: ESTIMADO |
| Ponto de táxi, marquise da entrada sul, floreira | contornos do cadastro e aérea | OBSERVADO | ESTIMADAS |

1.240 triângulos. O bloco antigo, o ponto de táxi antigo e uma casinha antiga sob a marquise foram retirados por baixo do modelo novo.

Erro do gerador antigo da praça, registrado: ele punha o terminal em x 572..624 / y ±19, isto é, 4 a 6 m fora do contorno real, e com a asa até x 631.

Não modelado: interior (guichê, catracas, bancos), ônibus, placas de ponto.

## 3. Vídeo de 11/11 (nosso): quadros, faixa da frente norte e fachadas no jogo

| Arquivo | O que mostra | Uso |
|---|---|---|
| `IMG_4194.MOV` (100 s, 1920×1080, 30 qps) | **toda a frente norte da avenida**, filmada do carro indo para oeste, do Mini Terminal até a ponta oeste da praça | usado |
| `IMG_4196.MOV` (19,5 s) | Sicredi e um prédio branco e vermelho; pela marca de GPS do arquivo fica 1,3 km a oeste da praça | quadros extraídos; **ainda não usado** |

**Quadros:** os 3.588 quadros dos dois vídeos estão em `E:\BURITIRO IMAGENS\PRAÇA DAS AGUAS\quadros\` (1,1 GB, com a cor convertida do HDR do iPhone para a cor normal). Ficaram no E: porque o C: está com 18 GB livres.

**Posição de cada quadro:** achei o instante em que o carro cruza o eixo de cada uma das seis ruas transversais e liguei ao x da rua na planta. Entre os controles a posição é linear (erro de alguns metros no meio de cada trecho). Conferência: a borda oeste do Tribunal de Contas sai em x 78,5 na faixa e está em x 78,2 no cadastro.

| Rua | Instante | x na planta |
|---|---|---|
| R. Cel. Pinto | 12,9 s | 488,8 m |
| R. Alfredo Cruz | 31,5 s | 337,3 m |
| R. Barão do Rio Branco | 46,9 s | 189,9 m |
| R. Cel. Mota | 67,0 s | 68,0 m |
| R. Pedrinho Filho | 78,8 s | 13,5 m |
| R. José Ribeiro da Silva | 87,4 s | −42,1 m |

**Faixa contínua:** `02_referencias/video_pda_20261111/faixa_norte.jpg`, 719 m de frente em 40 px por metro (28.760 × 700 px). Serve de levantamento: mostra, em ordem, o que há em cada lote.

**Fachadas no jogo, primeira versão (substituída, ver 3b):** 15 prédios da frente norte ganharam a fachada tirada do vídeo, num plano 10 cm à frente do volume de cada um.

| Trecho (x) | O que é | Altura lida |
|---|---|---|
| −88..−55 | prédio branco comprido | 4,3 m |
| −60..−46 | muro com a pintura da Lojita e prédio atrás | 3,7 m |
| −36..−14 | muro cinza e banco | 4,8 m |
| −15..−4 | casa branca moderna | 6,1 m |
| −5..8 | obra com tapume | 8,5 m |
| 22..34 | sobrado | 5,1 m |
| 34..44 | casa térrea com grades | 3,6 m |
| 44..57 | lojas (China in Box, Spoleto, Domino's) | 5,2 m |
| 78..110 | Tribunal de Contas de Roraima | 12,0 m |
| 114..139 | três prédios brancos e azuis, entre eles a ESCON | 9,1 a 9,6 m |
| 139..155 | entrada da ESCON e muro baixo | 6,9 m |
| 200..308 | prédio antigo amarelo atrás do tapume (dois contornos) | 3,8 e 7,1 m |

As alturas são ESTIMADAS (±20 %): lidas numa grade de 1 m sobre a imagem, e a escala vale para o plano que domina a imagem, que às vezes é a grade ou o tapume da frente, não a parede do prédio.

**O que a primeira versão era (não está mais na cena; os arquivos ficaram em `03_blender/frente_norte_foto.py` e `05_exportacoes/GLB/BVC_PDA_FRENTE_NORTE_foto.glb`):**

- São uma foto num plano. De longe e da avenida ficam como o real; de perto são chapadas. Não é modelagem de fachada.
- A foto traz o que estava na frente no dia: poste, árvore, grade, tapume, lixeira, ponto de ônibus, carros.
- Trazem **marcas reais de lojas e placas** (China in Box, Spoleto, Domino's, banco) e, em alguns pontos, **pessoa e carro com placa**. Convém vocês decidirem se isso pode ir a público.
- No Tribunal de Contas havia um carro-forte na frente. A fachada foi composta de dois instantes do vídeo para tirá-lo; ficou uma emenda visível em x 88 m.
- 5 dos 20 contornos visíveis ficaram de fora: dois são terreno vazio, um saiu borrado, um está tapado por ônibus e árvore, e o prédio institucional (x 502..573) fica em cima da costura com o Centro Cívico.

## 3b. Fachadas RECRIADAS em 3D (pedido do dono: não colar a foto, recriar em qualidade melhor)

A primeira versão colava a foto do vídeo num plano. Ela foi **substituída**: agora cada uma das 15 fachadas é geometria nova com textura limpa do projeto, e o vídeo ficou só como referência de medida.

- **Levantamento:** três agentes, um por vez, mediram cada fachada na imagem retificada do vídeo (paredes, janelas, portas, letreiros, telhado, muro ou grade da frente) e gravaram em `02_referencias/video_pda_20261111/fachadas_spec.json`. Confiança declarada: 2 altas, 11 médias, 2 baixas; cada fachada tem de 5 a 11 dúvidas anotadas.
- **Modelo:** `03_blender/frente_norte_3d.py` gera, por fachada, as paredes com a profundidade do cadastro, cada abertura com vidro e caixilho (e grade, quando há), letreiros, telhado e o muro, grade ou tapume do alinhamento. 23.536 triângulos, 113 materiais.
- **Letreiros:** só o nome, em fonte genérica (20 textos). **Nenhum logotipo é reproduzido.** Pessoas, carros e placas de veículo do vídeo não entram mais no jogo.
- **Posição:** a parede fica no alinhamento do lote (y −48,4) ou atrás dele; antes o volume do cadastro avançava 1 a 2 m sobre a calçada. O volume provisório desses 15 contornos saiu.

**Limites, vistos na comparação com o quadro do vídeo:**

- O que está bom: prédios de forma simples (bloco das lojas, casas, ESCON) ficaram reconhecíveis e limpos.
- O que ficou pior que a foto: o **Tribunal de Contas**. As fachadas curvas viraram retângulos, o revestimento de pastilha azul virou cor lisa e a grade da frente saiu escura e pesada demais. Precisa de modelo próprio, como os marcos.
- Profundidade das saliências, espessuras, inclinação dos telhados e tudo o que estava tapado por carro ou árvore são ESTIMADOS.
- As cores saem um pouco mais escuras que no vídeo.
- Esta parte **não passou por verificação independente**; conferi eu, nas folhas vídeo × recriada e nas fotos do Unity.

## 4. O que continua sem fazer ou sem verificar

1. **Nada foi jogado.** Sem build.
2. A verificação independente do muro e das bocas não terminou.
3. Do vídeo ainda não saíram: a ciclofaixa vermelha e a faixa de grama da calçada norte, o ponto de ônibus de vidro com cobertura vermelha, a mureta com alambrado do terreno vazio (x 340..485), o tapume do prédio antigo como peça à parte, e o prédio institucional da costura.
4. `IMG_4196.MOV` não foi usado.
5. **Frente sul da avenida: não há vídeo.** Continua volume provisório.
6. Os defeitos de desenho da praça medidos na rodada 15 (inclinação de 0,47°, deslocamento a leste, canteiro central estreito, lote x 350..470) continuam.
7. Alturas do terminal, dos restaurantes e dos marcos: ESTIMADAS.
8. Commit: nenhum.

## 5. Arquivos

- Importador: `Editor/FBBVCityPlantaImporta.cs` (cópia de antes da 15d em `_backup/bvcity_r15_20261111/`).
- Muro e bocas: `06_unity/ancora.py`, `bocas_pa.txt`, `exporta_unity.py`, `compara_muro_r15.py`.
- Terminal: `03_blender/terminal.py`, `06_unity/terminal_pose.json`, `compara_terminal_r15.py`; texturas `letreiro_terminal` e `placa_solar` em `04_texturas/gera_texturas.py`.
- Vídeo: `02_referencias/video_pda_20261111/` (`faixa_fachadas.py`, `fachadas_norte.py`, `fachadas_cfg.json`, `faixa_norte.jpg`, `faixa_norte_quadros.csv`, `fachadas/`, `tex/`, `folhas/`); `03_blender/frente_norte.py`; `06_unity/alturas_video.json`, `compara_video_r16.py`.
- Comparações: `07_comparacoes/pda_r15/` (muro e terminal) e `07_comparacoes/video_r16/`.
