# BV City — Rodadas 24 e 25 (12/11/2026)

Pedidos do dono: *"Sim, continuar"* (lado sul além das duas pontas já feitas) e *"certifique de ir ate o cine superk"*.

**Resposta curta: o lado sul da Av. Capitão Ene Garcez está modelado de x −1249 (Cine Super K) até x 617 (esquina do prédio cinza de brises).** Um revisor independente conferiu a primeira versão com 21 vistas próprias do Street View: confirmou posição e altura do cinema, do quartel da PM, do sobrado e a linha do gradil do batalhão, e achou 15 diferenças (3 graves, 6 médias, 6 leves). Tratei as 15; **a versão corrigida só eu conferi**. Não é 100 % fiel. **Nada foi jogado** (sem build) e nada foi commitado.

## 1. O que entrou

Não há vídeo próprio desses trechos. Fontes: contornos do cadastro (posição e largura dos prédios, ±1,5 m — MEDIDO), consulta ao Street View (imagens de jan/fev 2025; nada copiado), aérea Bing z18 que já estava baixada (só distâncias relativas) e limite da área militar do OSM. **Todas as alturas são ESTIMADAS pela perspectiva das capturas (±10 %)**: a altura da câmera do Street View ficou entre 2,1 e 2,3 m nas três checagens do revisor.

**Rodada 25 — marco novo `BVC_EG_SUL_OESTE`, x −1249 a −176 (26 unidades, 14.282 triângulos):**

| Trecho | x | O que é | Como foi posto |
|---|---|---|---|
| 6º BEC | −945 a −296 | mureta azul com tela em postes | linha = limite militar do OSM, conferida em 7 pontos (até 2,5 m); mureta 0,7 a 1,0 m e tela até 1,15 a 1,65 m, por trecho (medidas do revisor) |
| 6º BEC, leste do igarapé | −273 a −176 | muro azul cheio com pilastras | 1,55 m (revisor: 1,45 a 1,6) |
| Portaria do batalhão | −584 a −544 | muretas laterais e monumento | largura medida pelo revisor (40 m); **o trator do monumento é só um volume em caixas**, girado 35°, com a inscrição |
| Anexo da PM | −992 a −945 | gradil azul, entrada e prédio térreo de cobertura metálica | gradil 1,85 m; entrada em x −955 a −949,5 |
| Quartel do Comando Geral da PM | −1028 a −992 | alas brancas, duas torres azuis, faixa com o nome, 9 janelas, marquise | alas 9,3 m, torres 10,8 m, centro 9,6 m; larguras pela proporção medida |
| Terreno murado | −1135 a −1042 | muro branco com pilastras escuras, entrada de terra, portão azul | 1,6 m, pilastras a cada 2,9 m |
| Sobrado da esquina | −1183 a −1158 | sobrado creme, telhado de barro, muro com pilastras bordô | muro e portões conferidos |
| Muro rosa | −1208 a −1183 | muro com portão escuro de ripas | 1,85 m |
| **Cine Super K** | −1249 a −1208 | corpo principal e ala "CINEMA 3D" | fachada triangulada em y 61,7 (modelo 62,4); painel do topo 8,6 a 12,0 m; letreiro 3,9 a 5,45 m |

Também entrou o **gramado** entre a calçada da planta e o gradil do batalhão (na aérea e na consulta há grama com árvores ali).

**Rodada 24 — no marco `BVC_PDA_FRENTE_SUL` (agora 29 unidades, 47.880 triângulos):** muro azul a oeste da Av. Terêncio Lima (x −175 a −116); a leste da R. Cecília Brasil, terreno cercado, escola, lanchonete, estacionamento e prédio cinza de brises (x 341 a 617).

## 2. Verificação independente da primeira versão

Arquivo: `09_relatorios/verificacao_r25.json` (um revisor, só leitura, 21 vistas próprias e 6 recortes da aérea).

**Confirmou:** cinema, quartel, sobrado, muro rosa e muro do terreno em posição (até 3 m) e altura (até 10 %); a linha do gradil do batalhão; a distância guia–gradil (10 a 12 m); o vão do igarapé (eu suspeitava que o canal da planta estava 8 m deslocado: o desvio era da aérea, não da planta).

**Graves, os três corrigidos:**
1. **Gradil do batalhão 50 a 65 % alto demais.** Eu pus mureta de 1,4 m e tela até 2,1 m; o revisor mediu 0,65 a 1,0 m e 1,1 a 1,65 m em cinco pontos.
2. **Prédio cinza de brises:** o real é girado ~22° em relação à avenida e tem ~16 m; eu tinha feito paralelo, com 12 m. Refeito girado; o bloco branco da frente e o gradil foram para o lugar medido.
3. **Escola:** é um prédio único de ~89 × 40 m girado ~19°; eu tinha feito dois blocos paralelos. Refeito.

A causa dos itens 2 e 3 foi minha: usei a caixa envolvente do contorno do cadastro como se fosse o prédio.

**Médias (6) e leves (6), todas tratadas:** portaria alargada de 30 para 40 m; a leste do igarapé o fechamento é muro cheio, sem tela; portão do anexo da PM 10 m a oeste de onde eu tinha posto; muro azul de 2,3 para 1,85 m, prédio creme de 3,4 m só no lado oeste e quadrante da piscina aberto; lanchonete levada para a calçada, com as cores de 2025; gradil branco de 1,6 para 1,95 m, portão no lugar e gradil estendido até x 617,5; ala do cinema com dois janelões e painel do topo com 22,3 m; monumento quase no chão e girado; portões no muro rosa e no muro do terreno; telhado do sobrado; marquise e alturas do quartel.

**Por minha conta:** o bloco creme na calçada em frente ao muro azul era um contorno falso do cadastro automático (20 m², sem construção na consulta) e saiu da planta; a marquise do quartel saía cinza e ficou branca; debaixo do prédio cinza havia um volume branco do mapa do Centro Cívico atravessando a fachada: o importador agora tira, só nessa área, o que fica sob o prédio (13 malhas, 5.618 triângulos e um poste; o mapa original do parceiro não foi alterado).

## 3. O que continua diferente do real

- **A versão corrigida não passou por nova verificação.**
- Vegetação: nenhuma árvore do gramado do batalhão, sebe florida, árvore de lote ou jardim foi posta. É o que mais muda a leitura do trecho do batalhão.
- Monumento da portaria: o trator é um volume de quatro caixas. Guarita, cancelas, obstáculos de pista e a tela sobre as muretas da entrada não foram postos; a pista de entrada da planta é mais estreita que a real.
- Cine Super K: relevos, bonecos e medalhão **não foram reproduzidos de propósito** (obra de terceiros); janelas em arco saíram retas; o canto oeste da ala desce em dois degraus, não em rampa; laterais na cor da frente (a lateral leste real é avermelhada); sem postes de globo, balizadores e piso desenhado.
- Quartel da PM: sem bustos, mastros, sebes e estacionamento pintado; os blocos do fundo continuam volume provisório.
- Anexo da PM: sem a varanda em volta; aberturas não vistas.
- Terreno murado: sem a piscina, o quiosque, a rampa de concreto, os painéis do muro e o abrigo de ônibus.
- Escola: número e ritmo das janelas e o segundo piso não foram conferidos (árvores na frente); portão do gradil não conferido.
- Prédio cinza: aletas verticais (as reais são inclinadas); profundidade deduzida do cadastro; sem o volume verde, o painel azul e a marquise laranja da ponta leste; o prédio de vidro ao fundo continua volume provisório.
- Lanchonete: paralela à avenida (a real fica em diagonal na esquina).
- Chão: fora o gramado do batalhão, o piso é o da planta (cores de uso do solo); a guia sul da planta fica ~3 m ao norte da real em parte do trecho.
- Concertina sobre a tela do batalhão não foi posta. Trechos do batalhão não visitados pelo revisor: x −780 a −700, −546 a −430 e −400 a −300.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200; erro do PhysX "cleaning the mesh failed": 0 ocorrência(s) (antes 2 por importação); 1 peça(s) sem volume deixadas sem colisor.

## 4. Arquivos

- Gerador: `03_blender/frente_norte_3d.py` com `BVC_LADO=sul_oeste` (pivô −700 / 60), unidade girada (`giro_oeste_para_sul_graus`), `na_calcada`, muro com pilastras, gramado; patches `patch_gerador_sul_oeste.py`, `_b.py`, `patch_gerador_r25c.py`.
- Especificação: `02_referencias/frente_sul_oeste_20261112/` (`bloco_1_bec.txt`, `bloco_2_pm.txt`, `bloco_3_cinema.txt`, `ajuste_linha.json`, `consulta/`); r24 refeita em `02_referencias/frente_sul_20261112/_bloco_r24c.txt`.
- Unity: marco `BVC_EG_SUL_OESTE` em `marcos_cena.json`; 8 contornos novos em `modelados.geojson`; setores `BVC_S_X-4_Y0`, `X-3_Y0`, `X-1_Y0`, `X0_Y0`, `X1_Y0` reexportados; importador com `BatchAlturas`, câmeras `r25_*` e peça sem volume sem colisor (era a origem do erro do PhysX no console: `BVC_ASSEMBLEIA_Montante_12`, 0 × 8 × 0 m).
- Ferramentas novas: `01_georef/scripts/sat_z18_corte.py`, `06_unity/alturas_planta.py` (altura do chão pelas malhas da planta), `06_unity/registra_sul_oeste.py`, `06_unity/compara_r25.py`, `06_unity/leva_r25c.sh`.
- Comparações: `07_comparacoes/r24/` e `07_comparacoes/r25/` (`r25a/` = primeira versão).
- Cópias de segurança: `_backup/bvcity_r24_20261112/`, `_backup/bvcity_r25_20261112/`.
