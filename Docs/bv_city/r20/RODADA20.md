# BV City — Rodada 20 (12/11/2026)

Pedido do dono: *"Sim, baixar"* — baixar a imagem aérea em resolução maior do trecho do TCE para acertar as larguras e a ciclovia.

**Resposta curta: as posições do trecho TCE → prédio branco → ESCON foram recalibradas e conferidas por revisor independente; a ciclovia não foi mexida, porque o chão ali é do mapa do parceiro e a correção tira uma faixa de rolamento da rua dele — é decisão sua.** Continua não sendo 100 % fiel. **Nada foi jogado** (sem build) e as correções finais **só eu conferi**.

## 1. O que foi baixado

Aérea Bing no nível 19 (0,30 m por pixel, o dobro da que tínhamos) da frente norte inteira: 36 pedaços, `02_referencias/sat/bing_fn_z19.png`. O nível 20 não existe para Boa Vista. **Uso: só conferência**; não vira textura nem geometria.

## 2. O que mudou na cena

| Peça | Antes (r19) | Agora (r20) |
|---|---|---|
| Prédio branco de 2 pavimentos | x 120,5 a 128,3 | x 116,7 a 124,6 (cerca de 4 m a oeste) |
| Muro branco depois do portão do TCE | 8,3 m | 5,3 m; no mesmo plano do muro vizinho |
| Portão de enrolar do TCE | 3,8 m | 3,6 m |
| Cobogó do prédio branco | escondido atrás do muro | a leste da quina do muro, à vista |
| ESCON (volume azul, pilone, marquise, portão, muro) | volume azul em x 133,4; vão de 5 m até o prédio branco | volume azul em x 126,0; vão de 1,5 m; pilone em 132,0 (cerca de 7,5 m a oeste) |
| "Anexo branco" entre o prédio branco e a ESCON | parede e quina (inventadas por leitura errada) | retirado |

Todas as posições são **ESTIMADAS (±1,5 m)**.

## 3. Como foi medido

1. **Velocidade do carro no vídeo, pelas ruas transversais** (eixos do OpenStreetMap): entre a R. Coronel Mota (x 68,9) e a R. Pedrinho Filho (x 14,8) são 54,1 m em 318 quadros: **0,17 m por quadro**. A tabela antiga usava 0,202 constante. O revisor mostrou que a velocidade varia até dentro da quadra: cerca de 0,145 em frente ao prédio branco e ao muro, 0,2 a 0,3 mais a leste.
2. **Quadro em que cada quina passa pelo centro da imagem**, com correção da guinada da câmera.
3. **Contornos do cadastro** (Google Open Buildings): o prédio branco cabe no contorno `38P6` (113,9–124,0); o contorno `396C` (124,7–130,2) casa com o volume azul da ESCON.

## 4. Verificação independente

Um revisor, só leitura (`09_relatorios/verificacao_r20.json`), refez a calibração com um odômetro próprio (pelo movimento do chão entre quadros) e tentou refutar sete afirmações.

**Confirmou:** a velocidade média entre as ruas; as posições do muro do TCE, do portão, da quina e do prédio branco (os valores dele ficam 0,5 a 1,5 m a leste dos meus, dentro da tolerância); o muro de 5,6–5,8 m e o portão de 3,5–3,75 m; a falta de calçada (seção 5).

**Refutou, e eu corrigi (r20b):**

1. **Não existe "anexo branco" de 5 m entre o prédio branco e a ESCON.** No vídeo o vão tem 1,3–1,6 m. Eu tinha lido a aérea sem perceber que, do jeito que o meu script a desenha, ela sai cerca de 5 m a oeste do referencial dos contornos (as três ruas transversais aparecem 4,5–5,5 m deslocadas). A ESCON foi encostada no prédio branco.
2. **A guinada da câmera é de cerca de 10 graus, não 23.** Com o valor errado eu tinha empurrado as peças recuadas 1 m a oeste a mais. O cobogó e a borda oeste do prédio branco voltaram para leste da quina do muro.
3. **A velocidade não é constante dentro da quadra** (item 1 da seção 3). As peças a leste do prédio branco tinham sido datadas com a média.

**O que o revisor não conseguiu julgar:** a distância focal real do vídeo; a posição absoluta do conjunto (só as distâncias relativas são firmes); a ponta leste da ESCON e a marquise; o recuo do prédio branco (entre 4 e 9 m conforme a focal).

## 5. Calçada e ciclovia: decisão sua

Medi a cena por raio (`06_unity/perfil_calcada.txt`). No mapa do parceiro:

| Faixa | Na cena | No real (quadros do vídeo) |
|---|---|---|
| Canteiro de grama | 1,0 m | 1,9 a 2,3 m |
| Ciclovia | 2,0 m | 2,3 a 2,9 m |
| Calçada | 0,9 m | 2,3 a 2,9 m |
| **Guia da pista até o muro** | **4,1 m** | **6,9 a 8,3 m** |

A pista da cena avança cerca de 3,5 a 4 m (uma faixa de rolamento, de y −44,2 a −40,2) sobre o que no real é canteiro e ciclovia. O revisor confirmou a medida e acrescentou que **a ciclovia real é rebaixada, no nível da pista, entre duas guias pintadas**; na cena ela está no nível da calçada.

Na rodada 19 eu tinha escrito que as fachadas estavam no lugar e que só a ciclovia estava deslocada. **Estava errado**: comparei com a guia da planta que eu gero (y −40,3), e não com a do mapa do parceiro, que é a que aparece na cena (y −44,2).

Corrigir significa pôr um piso novo sobre uma faixa de rolamento da rua do parceiro, ao longo de cerca de 430 m, e resolver o que fazer com postes, placas e contêineres que ficam nessa faixa. **Não fiz.**

## 6. O que continua diferente do real

- Calçada, ciclovia e canteiro (seção 5).
- Posição absoluta do trecho: ±1,5 m.
- ESCON: ponta leste (muro, prédio atrás do muro) e marquise não reconferidas; volume azul ainda largo para a altura; azul mais violeta que o real.
- TCE: coroamento da torre simplificado; lado oeste (tambor) não reconferido nesta rodada.
- Prédio branco: falta o guarda-corpo do terraço, o gradil azul da rampa; totem com as cores do painel invertidas.
- Os itens da seção 4 do `RODADA19.md` que não são deste trecho (obra, casas, sobrado, Oi, prédio antigo do pórtico): como estavam.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200.

## 7. Arquivos

- `01_georef/scripts/sat_frente_norte.py` (download) e `sat_fn_corte.py` (recorte com grade; **somar ~5 m ao x lido**).
- `02_referencias/video_pda_20261111/patch_r20.py` e `patch_r20b.py`; especificação anterior em `fachadas_spec_r19e.json` e `fachadas_spec_r20.json`.
- Gerador: `dx_m` por fachada (posição recalibrada sem mexer na lista do cadastro).
- Unity: `BatchPerfil` (perfil do chão por raio) e câmeras `r20_*`.
- Comparações: `07_comparacoes/r20/` (`antes/` = fotos da r19), `06_unity/compara_r20.py`.
- Verificação: `09_relatorios/verificacao_r20.json`.
