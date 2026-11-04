# Morro da Sacola — mapa novo do BURITIRO (03/11/2026, revisado em 04/11/2026)

Mapa `morro_sacola` ("Morro da Sacola"), arena de 600 × 600 m no bairro real de Boa Vista (RR), feito a partir dos
13 vídeos de `Desktop\morro da sacola\`, do OpenStreetMap, do contorno das construções (Microsoft Buildings),
do relevo Copernicus e do satélite (só como referência de posição; nenhuma imagem de satélite vai para o jogo).

Regra seguida: **o que está no mapa saiu de um vídeo ou do satélite.** O que é dedução está marcado como aproximado
na lista abaixo e no próprio `tools/morro_sacola/locais.py` (cada ajuste tem `fonte` e `conf`).

## Estado

| Item | Situação |
|---|---|
| Cena | `Assets/FreeBala/Scenes/Map_morro_sacola.unity`, no build (EditorBuildSettings) |
| Montagem | 248 grupos de LOD (3 níveis), 344 colisores, NavMesh 630 × 630 m (341 colisores entram no cálculo) |
| fb_data | `maps.morro_sacola` em `StreamingAssets/fb_data.json` e na cópia `Resources/fb_data.json` |
| Teste automático (editor) | todos os spawns, bots, waypoints e pontos em cima do NavMesh; base A → base B completo (264 m a pé); 3 rotas e 6 pares de bases sem trecho quebrado |
| Menu | aparece na escolha de mapa ("Boa Vista · ruas de terra, muro pré-moldado, quintais · média distância") e na vitrine (`-fbvitrine`) |
| Personagens/veículos fixos | nenhum (`parked` vazio), como pedido |
| **Não testado** | Play no editor e .exe (`-fbtesteexe 10x10 morro_sacola`, `paredes morro_sacola`): falta gerar a build |

## Regiões (coordenadas no referencial dos vídeos: x leste, y norte, em metros; no jogo, mapa = vídeo − (70, 60))

1. **Beira oeste sul** (rua 5, trecho asfaltado): pista com faixa amarela, muro pré-moldado com arame farpado do lado oeste e muro alto de blocos do lado leste, placa PARE na junção da Canário com a Pedro Barros (−119, −227). Fonte: Y1 0–20 s (câmera veicular com GPS, noite de 06/11/2023; o T9 é o mesmo arquivo). A verificação recuou o começo do Y1 uns 40 m para o sul, até essa junção (ilha de meio-fio nos quadros de 1–3 s). Confiança alta na posição, média nos detalhes (noite).
2. **Beira oeste centro** (rua 5, terra): vala de erosão, campo aberto e muro pré-moldado pichado do lado oeste. Fonte: Y1 20–94 s, W1. Alta.
3. **Junção do quiosque / esquina dos postes** (58, 170): T da rua 5 com a rua ESE, postes juntos, quiosque "TEMOS CALABRESA" no canto NE (conferido no Y1 100–103 s, no T1 31–33 s e no T6 70–73 s). Fonte: Y1, W1, T1 30–36 s, T6 74–79 s, T7 12 s. Média.
4. **Beira oeste norte** (rua 5, de (26,78) até (88,272)): muro pré-moldado novo com concertina e a pichação "TUDO BASU" do lado oeste, com colonião alto e verde (1,7–2,6 m, quase no topo do muro) entre a rua e o muro; casas de tijolo, casa verde-água com concertina, casas rosa, mangueira com barraco de lona, bar ASSADÃO (vinho). Fonte: T1 30–56 s, T7 0–24 s, T4 30 s, W1, Y1. Média.
5. **T da beira da mata** (88, 272): prédio bege alto na esquina SE, sobrado bege com loja na esquina SW, muros de tijolo com portões brancos ("129"). Fonte: T4 33–43 s, T1 56–60 s, T7 24–31 s, W1. Média.
6. **Curva norte e saída para a mata**: quem chega pelo trecho SE vê de frente uma fileira geminada com pilastras azul-claras: a casa 471 em rosa desbotado (dois módulos, cor medida no T1 81 s) e, para o sudoeste, um módulo lilás-azulado de janelas brancas (T7 37–38 s). À direita dela fica o esqueleto de concreto vazio (2 vãos) com pilhas de tábua nova (clara), e a rua dobra para o norte na frente da estrutura. Casa branca de duas águas no canto. Campinho de traves brancas no campo a oeste (aproximado). Depois: estrada no campo e água parada com buritis (≈(30,432)). Fonte: T1 78–83 s, T4 58–67 s, T7 36–41 s, W1 64–71 s. Média na curva, baixa depois dela.
7. **Ruas do T1 no miolo**: rua NE (21), rua ESE (15) com mercearia rosa, sobrado bege de varanda, prédio branco de 2 andares, sobrado azul em obra; rua NNE (14) com montes de areia e sulco de chuva; sobrado de varanda branca no canto NO do T (121, 135). Fonte: T1 0–30 s, T6 42–74 s. Média.
8. **Rua de terra com barracos** (rua do Y2). Fonte: Y2. Média (o trecho de 0–26 s ficou incerto na verificação).
9. **Rua de cima, lado norte** (via 18, de x 252 a 382): muro corrido de tijolo colado na rua, portões de chapa clara, casas rosa, terracota e cinza no alinhamento, coqueiros e árvores atrás. Do lado sul, casas azul-claras e muros cinza. Fonte: T8 219–240 s e satélite. Baixa a média.
10. **Lagoa da ETE e campo**: água pelo material `MT_agua_lagoa` (vira água no `FBWater`). Fonte: satélite. Média.
11. **Resto do loteamento** (fora dos trajetos filmados): ruas do OSM, casas pelo contorno do satélite com fachadas, cores, muros e portões tirados do repertório dos vídeos. Não são casas reconhecíveis uma a uma.
12. **Horizonte** (além da arena, até 1,5 km): mata, campo e bairro baixo em cartões, só cenário, sem colisão e sem sombra.

## Vídeos: o que cada um deu

| Vídeo | O que é | Uso |
|---|---|---|
| Y1 | câmera veicular 70mai, noite 06/11/2023, GPS no carimbo | beira oeste inteira, de sul a norte (base do mapa) |
| Y2 | carro, dia | rua dos barracos |
| W1 | WhatsApp 03/11/2026, carro, dia | beira oeste centro/norte, vala, campo, mangueira; o fim (travessia da mata, y 400–445) tem confiança baixa |
| W2 | WhatsApp, 196 s | **não localizado** (casas rosa de pilastras azuis, "BIG FORMIGUEIRO", postes com trepadeira); nada dele foi inventado no mapa |
| T1 | celular, dia nublado, 95 s | ruas do miolo, junção, beira oeste norte, T da mata (o mais usado depois do Y1) |
| T2 | colagem de 8 clipes | **não localizado** |
| T3 | noite, janela direita | **não localizado** |
| T4 | carro, dia | localizado (12 trechos): repete o começo do T1, beira oeste, T da mata, curva norte, água |
| T5 | câmera veicular, 01/05/2024, GPS | **fora da arena**: estrada de acesso 500–800 m a leste (Mercadinho WY, Rua Rouxinol, Nova Vida). Fica para uma expansão |
| T6 | moto, fim de tarde | 42–82 s localizado (rua NNE → rua ESE → beira oeste); 34–42 s incerto; 0–34 s não localizado |
| T7 | moto, celular no guidão, dia nublado, 60 s | localizado de 0 a 41 s: beira oeste de (26,78) até o T, rua da beira da mata e curva norte. Depois de 42 s sai da arena para o norte (incerto) |
| T8 | câmera veicular dupla, 2025, 240 s | entra pela Rua Capitão Clóvis da Costa e pela Rua Colibri, fora da arena até 219 s; de 219 a 240 s anda na rua de cima (via 18), dentro da arena. A metade de cima do quadro mostra o motorista: nas comparações só entra a metade da rua |
| T9 | câmera veicular, noite 06/11/2023 | é o mesmo arquivo do Y1 (0–120 s, correlação ≥ 0,90): confirma o Y1, sem geometria nova |

## Partes aproximadas (precisam de mais referência)

1. **Fileira geminada e estrutura de concreto da curva norte**: posição e rumo da fachada (para SE) tirados dos quadros do T1, T4 e T7, ±5 m. O satélite, mais velho, mostra só um terreno limpo ali. O gerador tinha um lote só nesse canto (778 m²), por isso as casas entraram desenhadas à mão em `locais.py`. A divisão entre o rosa (casa 471, 4 vãos no T1 81 s) e o módulo lilás-azulado (T7 37–38 s) é estimada: nenhum quadro mostra a fileira inteira de uma vez.
2. **Muro pré-moldado novo da beira oeste**: o T7 mostra o muro já em (26,78) e o T1 da junção para o norte. O Y1 e o satélite, mais antigos, não têm esse muro (obra posterior). Ficou o estado mais novo. Confiança média.
3. **Beira oeste no extremo sul** (rua 5 antes de s 191): só o Y1 de noite. Falta vídeo de dia.
4. **Alturas dos prédios de laje** e recuos das casas: pelo satélite e pelas sombras, ±1–2 m.
5. **Largura das ruas**: OSM + satélite, ±1 m.
6. **Campinho**: não aparece no satélite. O W1 66–69 s mostra as traves finas ao longe, à esquerda da curva. Ficou no campo aberto a oeste (−30, 333), ±20 m. Antes estava na curva, no lugar da fileira geminada.
7. **Estrada no campo e água com buritis**: ±20–25 m. Nos vídeos, a travessia casas → mata → campo dura 4–6 s, contra ~75 m de mata no satélite (satélite antigo ou desmatamento).
8. **T1 aos 31 s**: a pose da câmera na comparação está uns 4 s atrasada (aparece mais a oeste do que no vídeo); a geometria da junção está certa.
9. **Miolo das quadras e becos** fora dos trajetos: gerados pelo contorno das construções; ninguém filmou por dentro.
10. **W1 aos 43 s**: o prédio bege alto do T da mata não aparece na mesma vista do Unity (a câmera ou a esquina estão
    alguns metros fora). Ver na comparação `img/cmp_w1.jpg`.
11. **Cor da terra** (refeita em 04/11): medindo só a faixa de rua dos quadros, o tom é 13–22° (T7 4/21 s, T1 81 s, T4 58 s), e o Unity
    dava 29–30° (bege) e ~25 % mais claro. A cor alvo das texturas de rua, terra e sulco mudou (`texturas.py`) e a rua passou a medir
    tom 21–22° no Unity, ~8 % menos clara que antes e ainda um pouco mais clara que o vídeo (o jogo é de sol, o T7 é nublado e molhado).
    Não há filtro de cor na tela. Antes × depois em `img/terra_antes_depois.jpg`.
12. **Trechos não localizados**: o W2 inteiro, o T2, o T3 e o T6 de 0 a 34 s. Não estão no mapa. Se alguém disser em que rua foram filmados, eles entram como os outros.
13. **Lado norte da rua de cima (T8)**: o gerador não tinha lote desse lado. O muro corrido e as 5 casas foram desenhados pelos telhados do satélite e pelo T8, que tem imagem ruim. O cobogó da casa terracota virou janela basculante, porque o gerador não tem cobogó.
14. **Número do portão branco junto ao T da beira da mata**: "272" no T1/T4 e "372" no T7 28 s. Não resolvido.
15. **T7 depois de 37,5 s**: não ficou provado que ele segue pela estrada norte, porque não aparecem as casas 460/480, a igreja IPUB e o barraco vermelho do lado leste.
16. **Tempo das câmeras do T7 (0–21 s) e das curvas**: as poses das comparações saem do caminho da análise, presas ao eixo da rua. No T7 a ordem das coisas bate: muro com colonião à esquerda, casas rosa à direita aos 9–10 s, pichação "TUDO BASU" no muro e mangueira à direita aos 14–15 s. A câmera do modelo, porém, fica uns 2–3 s adiantada, e por isso o canto do quiosque e o barraco de lona aparecem antes. Na curva norte o rumo do carro muda e a vista "na mesma pose" sai torta, por isso o teste do Unity tem também duas vistas apontadas para a fileira geminada (`curva_t1_81`, `curva_t4_64`).

O que mais ajudaria:
- fotos de dia da beira oeste sul;
- uma volta filmada pelas ruas de pássaros (Andorinha, Rouxinol…) e pelo miolo;
- o lado norte da rua de cima filmado de dia;
- saber onde fica o "BIG FORMIGUEIRO" do W2.

## Dados de jogo (10x10)

- Base A (−141, −120) e base B (−6, 99) no referencial do mapa: 257 m em linha reta, 264 m a pé, sem visada direta entre elas.
- Polícia no canto mais longe das duas (249, −273). 10 spawns por base, 5 da polícia.
- 3 rotas: rua 5 (beira oeste, 268 m), Rua Pedro Barros (460 m), Rua Andorinha (722 m).
- 6 pares de bases, de 264 a 444 m a pé. 24 pontos de bot, 40 waypoints.
- 6 pontos com nome: esquina dos postes, barraco de lona, curva das casas lilás, placa PARE, pista asfaltada, muro pichado.
- Névoa do mapa: `#b4c6d4`, 90–440 m. LOD: 90/140/380 m (casas), 200/300/570 m (fiação e grandes), 140/570 m (chão).

## Desempenho

- Cenário cortado em células de 96 m com 3 níveis de LOD; objetos repetidos (postes, árvores, capim, entulho) instanciados por célula.
- Chão em blocos com 2 níveis; colisão separada e simplificada (342 peças, ~1 milhão de triângulos no total).
- Horizonte em setores de 300 m, um nível só, sem sombra. Do chão a névoa esconde a emenda; do alto (aéreas sem névoa)
  dá para ver onde o chão detalhado acaba e o campo liso do horizonte começa.
- **Pesado**: o chão perto (LOD0) tem ~2,3 milhões de triângulos no total, mas só as células perto da câmera ficam no LOD0. Falta medir FPS no .exe.

## Arquivos

| O quê | Onde |
|---|---|
| Gerador (Python) | `game/tools/morro_sacola/` (cadeia abaixo) |
| Ajustes vindos dos vídeos | `game/tools/morro_sacola/locais.py` (editar aqui, não o json) |
| Saída do gerador | `F:/FreeBala_backups/ms_video/saida/` (`morro_sacola_unity.glb`, `morro_sacola_dados.json`, `dados_jogo.png`) |
| Análises dos vídeos | `F:/FreeBala_backups/ms_video/analise/G1…G8.json` e `verificacao.json` (verificação adversarial de 04/11), quadros-chave em `.../chave/` |
| Mapa no Unity | `Assets/BURITIRO_Mapas/morro_sacola/` (GLB, dados, NavMesh, trecho do fb_data) |
| Cena | `Assets/FreeBala/Scenes/Map_morro_sacola.unity` |
| Capa do menu | `Assets/FreeBala/Resources/MapShots/morro_sacola.jpg` |
| Montagem | `Editor/Parceiro/BuritiroMontaMapa.cs`: `META["morro_sacola"]`, `MontaMorroSacolaAuto()`, `AplicaTrechoAuto(id)`, menu *BURITIRO/Mapas/Montar Map_morro_sacola* |
| Conferência e fotos | `Editor/MorroSacola/FBMorroSacolaTeste.cs` (`MontaETesta`, `Batch`; menu *BURITIRO/Mapas/Morro da Sacola: conferir NavMesh e tirar fotos*) |
| Registro do mapa | `GameData.AllowedMaps`, `FBMenu.MAP_INFO`, `FBVitrine.TodosMapas`, `FBWorld` (limites 612 × 612), `FBUnificacao` (montar + build) |
| Cópias de segurança | `game/_backup/morro_sacola_20261103/` (fb_data antes da mudança, EditorBuildSettings, texturas); `game/_backup/morro_sacola_20261104/` (scripts e relatório antes da revisão) |

## Como refazer

```
cd game/tools/morro_sacola
python ruas_osm.py; python ajusta_ruas.py; python quadras.py; python lotes.py; python casas.py
python texturas.py; python locais.py; python chao.py; python plano.py; python monta.py; python dados.py
```

Os outros scripts da pasta (relevo, satélite, extração de texturas, OCR do GPS) preparam dados que já estão em
`F:/FreeBala_backups/ms_video/` (`dem/`, `sat/`, `texlib/`, `stamp/`).

Depois copie `morro_sacola_unity.glb` e `morro_sacola_dados.json` para `Assets/BURITIRO_Mapas/morro_sacola/` e rode, com o Unity fechado:

```
Unity.exe -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBMorroSacolaTeste.MontaETesta -fbvistas <vistas.json> -fbsaida <pasta das fotos>
```

Ele monta a cena, grava o fb_data (StreamingAssets e Resources), tira a cópia `.bak` de dentro do StreamingAssets
(senão ela iria para o build), confere o NavMesh e tira as fotos. O relatório sai em `<pasta das fotos>/teste.txt`.

Para mudar só um detalhe vindo de vídeo: edite `locais.py` e rode de `locais.py` em diante. `monta.py --rapido x y raio`
gera só um pedaço para conferir no Blender (`blender/render.py`).

Para mudar a cor de um piso em `texturas.py`: apague antes `tex_ms/borda_<classe>_c.png`, que o `monta.py` guarda em cache e
não refaz sozinho. Cores novas de pintura entram no fim do `PINT` (a semente de cada cor é a posição dela na lista).
Meça a cor só na faixa de rua ou de parede do quadro e na foto do Unity: o URP com o céu clareia e azula a textura
(~×1,14 no vermelho, ×1,22 no verde e ×1,40 no azul) e satura as cores fortes.

## Créditos dos dados

OpenStreetMap (ODbL), Microsoft Building Footprints (ODbL), Copernicus DEM GLO-30, texturas Poly Haven (CC0).
Os vídeos são de terceiros e serviram só de referência; nenhum quadro de vídeo vai para o jogo.

## Imagens

As fotos foram tiradas no editor pelo `FBMorroSacolaTeste` (URP da cena, sem o `FBWeather`, que só roda em Play):
sempre de dia, mesmo quando o vídeo é de noite (Y1). As aéreas estão sem névoa.

- `img/aereas.jpg`: capa e vistas aéreas tiradas no Unity (04/11), incluindo a curva norte e a rua de cima.
- `img/cmp_t7.jpg`: T7 de 0 a 40,5 s, quadro × mesma pose no Unity (04/11).
- `img/cmp_t8_rua.jpg`: T8 na rua de cima. Só a metade de baixo do quadro (câmera da rua): a de cima mostra o motorista e não entra.
- `img/cmp_curva.jpg`: curva norte, "mesma pose" e vista apontada para a fileira geminada (T1 81 s, T4 64,5 s).
- `img/cmp_t4_t1_t6.jpg`: T4 58 s, T1 73 s, T6 58,5 e 71 s.
- `img/terra_antes_depois.jpg`: vídeo × terra antiga × terra nova no Unity.
- `img/cmp_t1.jpg`, `img/cmp_w1.jpg`, `img/cmp_y1.jpg`: quadro do vídeo × mesma vista no Unity (montagem de 03/11, terra antiga).
- `img/dados_jogo.png`: bases, spawns, rotas e pontos sobre a planta.
