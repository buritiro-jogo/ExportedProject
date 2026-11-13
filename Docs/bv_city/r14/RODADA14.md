# BV City — Rodada 14 (11/11/2026)

Continuação da consulta ao Google Maps / Street View: Catedral, Tribunal de Justiça e Palácio da Cultura. Dois modelos corrigidos, um confirmado.

**Nada foi jogado** (sem build). Foi consulta: nenhuma imagem, textura ou medida do Google entrou no jogo; o que saiu de lá foi entendimento, e os modelos foram ajustados à mão. Todo número é MEDIDO, salvo quando marcado ESTIMADO.

## 1. O que cada consulta mostrou

| Prédio | Ponto (corredor) e data | O que mostrou | Resultado |
|---|---|---|---|
| **Tribunal de Justiça** | (702; −161), fotosfera de abr/2017, no pátio da frente; e Av. Ville Roy, imagem recente | o prédio é **azul** (corpo e pórtico), não branco com pórtico verde-água; a frente é mesmo o lado do pátio | **cor corrigida**; frente confirmada |
| **Palácio da Cultura** | (931; −135), Street View de fev/2025, da praça | ala de vidro à esquerda e parede do letreiro à direita, como no modelo; **atrás da parede há um telhado escuro de quatro águas**, onde a rodada 5 supôs um pátio; a parede é marrom-acinzentada de dia; há tapume de obra na frente | **telhado acrescentado**, cor da parede ajustada |
| **Catedral** | (812; 114), Street View de fev/2025, do anel | campanário esguio de duas lâminas à frente, fachada curva côncava com brises verticais, marquise baixa, parede alta em curva à direita | **confere** com o modelo e com a pose; nada mudou |

Limite da consulta da Catedral: só consegui uma vista limpa, oblíqua (~43° fora do eixo da fachada); os outros pontos caíram atrás de árvores. Não é conferência de medidas.

## 2. O que mudou nos modelos

**Tribunal de Justiça** (`03_blender/tj.py`): corpo e pórtico em azul. A foto de domínio público usada na rodada 3 é anterior à pintura e mostra corpo branco com pórtico verde-água; a forma (pórtico saliente, letreiro em duas linhas, brasões, panos de vidro, rampa) confere com ela e com a imagem de 2017. O mapa do parceiro chamava o material de "azul royal": ele estava certo, e a rodada 3 errou ao "corrigir" para verde-água.

**Palácio da Cultura** (`03_blender/cultura.py`):

| Item | Antes | Agora | Fonte |
|---|---|---|---|
| Atrás da parede do letreiro | bloco de um pavimento (5,2 m) com terraço | bloco com **telhado de quatro águas**, da fachada até 22 m | Street View; a aérea corrigida mostra a mancha escura com as diagonais |
| Beiral e cume do telhado | — | 11,0 m e 14,6 m | **ESTIMADO** (o cume aparece ~2,5 m acima da parede) |
| Cor da parede do letreiro | azul-acinzentado (lido numa foto noturna) | marrom-acinzentado | **ESTIMADO** a olho numa imagem de dia |

Conferência independente da pose: na vista de cima da cena, o telhado de quatro águas do modelo cai em x 937..951 / y −185..−163, e a mancha escura da aérea está em x 936..950 / y −186..−162. Isso fecha a pose da Cultura a ~1 m, o que o encaixe de planta sozinho não conseguia (ver rodada 12).

A divergência entre a planta do modelo (38 × 33 m) e o contorno do OSM (35 × 43 m) **continua aberta**: a faixa norte do contorno (y −203..−195) fica fora do modelo.

## 3. Cena

Sem mudança de pose em nenhum marco. Os cinco continuam ativos; 100 de 100 pontos do `fb_data` no NavMesh; 1 ponto sem colisor em 95.200 na janela do corredor.

| Marco | Frente | Situação depois das rodadas 13 e 14 |
|---|---|---|
| Assembleia | Av. Ville Roy (rumo 312°); entrada coberta para a praça | conferido no Street View; refeito na r13 |
| Palácio do Governo | rumo 133° | conferido (fotosfera de 2020) |
| Tribunal de Justiça | rumo 207° | conferido (fotosfera de 2017); cor corrigida |
| Palácio da Cultura | rumo 223° | conferido (Street View 2025); telhado acrescentado |
| Catedral | rumo 105° | uma vista oblíqua confere; não medido |

Comparações (`07_comparacoes/unity_r14/`): `tj_antes_x_depois.jpg`, `tj_foto_x_render.jpg`, `cultura_antes_x_depois.jpg`, `marco_cultura.jpg`.

## 4. O que continua sem fazer ou sem verificar

1. **Nada foi jogado.** Sem build.
2. **Alturas de todos os marcos continuam estimadas.** A consulta resolveu lado, composição e cor, não medida.
3. Catedral: falta uma vista de frente; a área do modelo é 1,18 a 1,23 do contorno.
4. Cultura: divergência de planta com o contorno; o tapume de obra que está lá hoje não foi modelado (é temporário).
5. Assembleia: paredes norte e leste não vistas; anexo do recorte e treliças não modelados (rodada 13).
6. Materiais: cores chapadas, sem textura.
7. O resto da lista das rodadas 12 e 13 (carga dos setores em jogo, NavMesh fora das zonas, degrau, lotes, Orla).
8. Commit: nenhum.

## 5. Arquivos

- `03_blender/tj.py`, `cultura.py` (cópias de antes: `.r13`); GLBs em `05_exportacoes/GLB/` e no projeto; renders de antes em `08_renders/_r13/`.
- `06_unity/compara_r14.py`; cena, NavMesh e `planta/recortes/` regravados.
