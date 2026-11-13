# BV City — Rodada 12 (11/11/2026)

Verificação independente da rodada 11, correção do que ela e os geradores mostraram, Palácio do Governo refeito, leito debaixo da água.

**Estado:** a cena `Map_bv_city.unity` tem os cinco marcos do Centro Cívico no lugar, a planta em 160 setores e chão com colisor em toda a janela do corredor. **Nada foi jogado** (decisão do usuário: seguir sem build). O lado da fachada da Assembleia está em dúvida e depende de quem conhece o prédio.

Todo número é MEDIDO, salvo quando marcado ESTIMADO.

---

## 1. Erros meus na rodada 11, corrigidos aqui

A errata completa está no topo do `RODADA11.md`. Em resumo:

| O que eu disse | O que é |
|---|---|
| Assembleia 23 % maior, medida herdada do parceiro, refazer a planta | O corpo segue o contorno do OSM. O excesso era a laje de cobertura e o rufo, que tapavam o recorte do anexo (945 m²). Corrigido: razão modelo/contorno 1,23 → 1,04; encaixe 0,81 → 0,94 |
| Marco do Palácio era a planta de outro prédio | Era o prédio certo. Erros reais: coordenada errada na tabela da rodada 7; planta do volume do parceiro, maior que o real; fachada longa no eixo errado; pátio tampado por laje |
| Degrau planta × zona: máx. 0,07 m (praça) e 0,62 m (Centro Cívico) | Medi o relevo nu. Na superfície: praça mediana 0,15 / máx. 0,22 m; Centro Cívico mediana 0,12 / p90 0,39 / máx. 0,77 m; 196 de 1.012 amostras acima de 0,30 m |
| Sem triângulo degenerado; volumes fora das zonas | 900 triângulos sem área e 48 volumes atravessando o muro da praça e a borda da arena |

## 2. Verificação independente (2 agentes em série, ~345 mil tokens, 18 min)

Pasta `09_relatorios/verificacao_r11/` (`marcos/` e `planta/`, cada uma com scripts próprios e `resultado.md`). Os dois deram **PARCIAL**.

**Confirmado com código próprio:**

- Palácio do Governo dentro do anel: o OSM bruto tem a relação "Palácio Hélio Campos / Palácio do Governo" com centro em (724; −5) e anel interno (pátio); "Palácio da Cultura" em (930..936; −181..−183). Nenhuma fonte no disco diz o contrário.
- Poses gravadas na cena iguais às do arquivo; encaixes refeitos com outra rasterização: 0,85 / 0,78 / 0,95 / 0,81 (meus: 0,82 / 0,77 / 0,97 / 0,81).
- 160 GLBs, 650.854 triângulos; chão da planta dentro das zonas: 0,012 m²; fresta em 20,29 km²: 62,8 m², só lascas; vértices criados pelo recorte no plano do triângulo de origem (diferença 0,00000 m); 320 pontos de conferência com erro 0.
- `fb_data`: fora do bloco `bv_city` é byte a byte igual; 51 pontos × 2 desceram 1,53 m, todos com x > 535.
- Carregador: simulação própria deu 111 cargas, 111 descargas, máximo de 22 setores vivos.

**Derrubado, e o que fiz:**

| Achado | Situação |
|---|---|
| Degrau na borda subestimado (relevo nu em vez da superfície) | números corrigidos (seção 1); o degrau em si continua |
| 48 volumes provisórios entram nas zonas mantidas (até 11,5 m para dentro do muro; até 30 m na arena) | corrigido: sai o volume cuja planta entra mais de 2 m² na zona |
| 900 triângulos sem área, que iam para os colisores | corrigido: filtrados na exportação |
| Carregador trava se o objeto for desativado no meio de uma carga | corrigido: a fila é limpa ao desativar |
| Retirada do prédio antigo usava o casco convexo: cobria o recorte da Assembleia e levava árvores, bancos e postes em volta | corrigido: usa o contorno do cadastro |
| Explicação errada do "23 %" da Assembleia | corrigida (seção 1) |
| **O contorno do OSM da Assembleia está ~7 m deslocado do telhado** na aérea corrigida (telhado: x 771,6..849,6 e y −226,6..−146,6; contorno: x 775,7..857,0 e y −231,0..−150,5). No TJ e na Cultura o contorno cai no telhado | conferi a leitura; o marco foi para o telhado (−5,75; +4,15 m). O modelo fica 3,3 m mais largo que o telhado em x (4 %) |
| Palácio da Cultura: o melhor encaixe é a 268°, não a 1,2° | a pose fica a 1,2° pela frente (praça pavimentada no lado +y; a 268° a fachada daria no vizinho). O encaixe sozinho não prova a pose; registrado |
| Frente do TJ marcada ESTIMADO | passou a MEDIDO: pórtico saliente e adro de bloquete rosado no lado +y na aérea, iguais aos da foto |
| **Frente da Assembleia: indecidível, com indício contrário** | ver seção 4 |

**Não verificado por ninguém:** carga assíncrona e engasgo em jogo; custo do `UnloadUnusedAssets`; se os materiais do mapa usam canais que o recorte de malha não guarda (uv3 em diante).

## 3. Palácio Senador Hélio Campos refeito

Gerador: `03_blender/palacio.py` (cópia de antes: `palacio.py.r11`).

| Item | Rodada 2 | Rodada 12 | Fonte |
|---|---|---|---|
| Planta | 53,7 × 47,9 m | **46,0 × 36,5 m** | telhado medido na aérea corrigida com grade de 5 m (±1 m); contorno do OSM 44,7 × 36,6 m |
| Fachada longa | ao longo do x do corredor | **ao longo do y**, frente para +x (rumo 133°) | pórtico a leste na aérea; gramado e canteiro na frente na foto de solo e na aérea oblíqua |
| Pátio interno | tampado por laje, com caixas em cima | **aberto**, 24 × 16 m | aérea corrigida e aérea oblíqua |
| Vãos da fachada | 11 | **10** (pilar no centro, sob o brasão) | foto de solo ampliada com grade |
| Platibanda | 0,17 da altura | **0,30 da altura** | foto de solo (62 de 207 px) |
| Pilastra | 1,15 m | 0,80 m | foto de solo (~0,16 do passo) |
| Painéis da platibanda e brasão | dentro da platibanda, invisíveis | à frente dela; 3 painéis largos por lado | foto de solo |
| Persianas claras nos panos, mastro inclinado | havia | saíram | a foto não mostra |
| Altura total | 13,70 m | 13,70 m | **ESTIMADO** (vem do volume do parceiro; não há medida própria) |
| Vãos das faces curtas | 10 | 8 | **ESTIMADO** (mesmo passo da frente; sem foto) |

Na cena: pivô (722,82; −4,77), giro 270,9°, encaixe 0,91 no contorno, 460 peças, 5.654 triângulos.

O que **não** é meu ali: o pórtico de entrada com cobertura, o canteiro redondo, as palmeiras, a guarita e a cerca são do mapa do parceiro e ficaram. A aérea atual mostra uma estrutura de 8 × 10 m na entrada, compatível com o pórtico do parceiro; a foto de solo, mais antiga, mostra um pórtico pequeno. Tirei o meu pórtico pequeno do modelo do jogo para não sobrepor. **A entrada não foi conferida com foto recente.** Os anexos a oeste também são do parceiro, não conferidos.

Comparações: `palacio_foto_x_render.jpg`, `palacio_aerea_x_render.jpg`, `marco_palacio.jpg`.

## 4. Assembleia: o que está certo e o que está em dúvida

Certo agora: corpo pelo contorno, cobertura com o recorte do anexo, posição pelo telhado medido.

**Em dúvida: de que lado fica a fachada da testeira curva.** O modelo da rodada 1 pôs essa fachada na borda voltada para a praça (lado +y, rumo 222°). Contra isso:

- a foto de referência mostra a testeira subindo ao longo da fachada, e o telhado na aérea curva ao longo de y, o que casa com uma fachada no lado oeste (Av. Ville Roy), subindo para o lado da praça;
- o endereço no cadastro tem as duas vias ("Praça do Centro Cívico, s/n" e "Av. Ville Roy, 6007"); a via mais próxima da frente é a Ville Roy;
- a sombra larga na borda sul da aérea indica que o lado alto é o da praça, coerente com a foto se a fachada for a oeste.

A favor do modelo atual: só o endereço na praça. Se a fachada for a oeste, a volumetria continua certa, mas os elementos da fachada (pele de vidro, arcos, brise) têm de passar para a outra borda e o modelo fica espelhado. Tentei consultar o Street View para decidir; a página não renderizou com a janela do app minimizada. Não mexi.

## 5. Planta e cena depois da rodada

| Item | Valor |
|---|---|
| Setores | 160 prefabs; 1.288 malhas; 1.205 com colisor |
| Triângulos | 651.732, dos quais 2.153 de leito |
| Volumes provisórios | 21.189 |
| Posição | 320 pontos de conferência, erro 0,0000 m |
| Colisor sob os pés (grade de 4 m, janela do corredor) | **1 ponto sem nada em 95.200** (eram 64, todos em água) |
| NavMesh do corredor | refeito (3.528 colisores); 100 de 100 pontos do `fb_data` nele |
| Carregador (Editor, sem Play, rota de 12,6 km a 80 m/s) | 113 cargas, 113 descargas, máximo 22 setores vivos, 0 vivo além do raio de saída; na partida 4 setores entram na hora; carga média 12 ms, pior 41 ms; memória 553 → 633 → 561 MB |

Outras mudanças:

- **Leito debaixo de toda água**, com colisor: Rio Branco 3,0 m abaixo do espelho; lagoa 1,5; igarapé 0,8; canal 0,6; espelho d'água 0,4; piscina 1,4. **Profundidades ESTIMADAS** (não há batimetria); servem para existir chão.
- **A Praça das Águas termina em x = 530.** O muro, o tapume e o trecho Portal–Terminal iam até x = 778 e atravessavam o anel do Centro Cívico; ficou visível quando o mapa do parceiro desceu. Foram cortados na costura. A cópia do Mini Terminal que a praça tinha além de x = 530 saiu; vale a do parceiro.
- **Retirada do prédio antigo por componente ligado:** quando 30 % ou mais de uma peça do parceiro cai dentro do contorno, a peça sai inteira (o Palácio dele era maior que o real e sobrava um toco). O piso fica sempre.
- **Marcos reinstanciados dos GLBs** a cada montagem (o modelo regerado tem peças com outros nomes).
- **Carregador:** na partida, o que está a menos de 300 m entra de uma vez.

Marcos na cena:

| Marco | Pivô (corredor) | Giro | Encaixe | Modelo / contorno | Frente |
|---|---|---|---|---|---|
| Catedral | (794,68; 150,92) | 243,1° | 0,82 | 1,23 | rumo 105° — MEDIDO |
| Palácio da Cultura | (933,41; −164,69) | 1,2° | 0,77 | 1,08 | rumo 223° — MEDIDO |
| Tribunal de Justiça | (707,42; −191,71) | 345,4° | 0,97 | 1,06 | rumo 207° — MEDIDO |
| Assembleia | (769,91; −146,05) | 0,0° | 0,94 | 1,04 | rumo 222° — **EM DÚVIDA** |
| Palácio do Governo | (722,82; −4,77) | 270,9° | 0,91 | 1,11 | rumo 133° — observado em três imagens |

Debaixo de cada marco (malhas do parceiro / triângulos retirados; colisores / triângulos): Assembleia 11 / 5.480 e 8 / 782; Palácio 16 / 8.097 e 4 / 2.480; TJ 10 / 6.469 e 4 / 1.693; Catedral 7 / 4.162 e 5 / 1.205; Cultura 8 / 2.523 e 5 / 937. São 111 malhas derivadas em `planta/recortes/`.

Comparações (`07_comparacoes/unity_r12/`): `corredor_real_antes_depois.jpg`, `perimetro_real_x_unity.jpg`, `marco_catedral.jpg`, `marco_cultura.jpg`, `marco_tj.jpg`, `marco_assembleia.jpg`, `marco_palacio.jpg`, `palacio_foto_x_render.jpg`, `palacio_aerea_x_render.jpg`.

## 6. Relatório do setor (atualização)

```
SETOR: Centro Cívico (arena do parceiro com x >= 530; 34,5 ha)

Planta geográfica:      feita; posição conferida na aérea corrigida
Ruas catalogadas:       72 trechos do OSM, 18 nomes
Lotes identificados:    0
Edificações no cadastro: 263 contornos (112 OSM, 151 Google); 13 conferem, 40 divergem, 210 sem medida.
                        O contorno da Assembleia está ~7 m deslocado (medido nesta rodada)
Edificações modeladas:  5 marcos levantados em cena; o restante é o mapa do parceiro
Edificações verificadas: 5 em posição e eixo (encaixe 0,77 a 0,97); frente medida em 3, observada em 1,
                        em dúvida em 1; alturas todas estimadas
Fachadas pendentes:     258 contornos sem levantamento; fachada da Assembleia pode estar na borda errada;
                        entrada e anexos do Palácio não conferidos
Comparações:            unity_r12/marco_*.jpg, palacio_*.jpg, corredor_real_antes_depois.jpg
Problemas encontrados:  os da rodada 11 mais: contorno do OSM da Assembleia deslocado; toco do prédio
                        antigo do Palácio; muro da praça atravessando o anel
Correções aplicadas:    Palácio refeito e reposto; cobertura da Assembleia; retirada por contorno e por
                        componente; praça cortada em x = 530
Próximas tarefas:       decidir a fachada da Assembleia; foto recente da entrada do Palácio; texturas
                        dos marcos (o vidro do Palácio sai claro no Unity); comparar os 258 contornos
                        com o que o mapa do parceiro tem
```

O setor Praça das Águas está como no `RODADA11.md`, com duas mudanças: termina em x = 530 e nenhum volume provisório atravessa o muro.

## 7. O que continua sem fazer ou sem verificar

1. **Nada foi jogado.** Sem build; `10x10 bv_city` e `paredes bv_city` precisam ser repetidos na cena nova.
2. Carga dos setores dentro do jogo: não testada. O teste em Play não avança no Unity sem janela.
3. NavMesh fora das duas zonas: não existe. Limites do mapa no código, minimapa e capa: como estavam.
4. Degrau planta × Centro Cívico: até 0,77 m na superfície; a transição de 150 m em volta dele é artificial.
5. Do lado do parceiro, objetos que cruzam x = 530 continuam como a rodada 7 deixou (os de centro abaixo de 530 desligados inteiros).
6. Catedral: planta do modelo 1,18 a 1,23 do contorno (parte é beiral; não medi). Cultura: modelo 38 × 33 m contra contorno 35 × 43 m, não resolvido.
7. Materiais dos marcos: cores chapadas do Blender; o vidro escuro do Palácio aparece claro no Unity.
8. Orla Taumanan sem a laje elevada. Lotes: 0. Fachadas novas além do Palácio: 0. Street View: não consultado.
9. Commit: nenhum.

## 8. Arquivos

- Geradores: `03_blender/palacio.py`, `assembleia.py` (cópias `.r11`); GLBs em `05_exportacoes/GLB/` e em `Assets/BURITIRO_Mapas/bv_city/marcos/` (os de antes em `game/_backup/bvcity_r11_20261111/marcos_antes/`).
- `06_unity/`: `marcos_cena.py` → `marcos_cena.json` (substitui `marcos_r11.json`), `exporta_unity.py` (leito, volumes na borda, triângulos sem área), `compara_r12.py`, `_palacio_foto_zoom.png`, `_mede_palacio.png`, `importa_resumo.txt`, `stream_teste_editor.txt`.
- Unity: `FBBVCitySetores.cs`, `FBBVCityPlantaImporta.cs`, cena, NavMesh, prefabs dos setores, `planta/recortes/`.
- Para repetir: restaurar a cena de `game/_backup/bvcity_r11_20261111/` e rodar `game\_diag\roda_bvplanta.ps1 -Metodo BatchImporta` (17 min; com `-Extra "-fbsemprefab"`, 10 min).
