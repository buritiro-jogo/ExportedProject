# BV City — Rodada 29 (13/11/2026)

Pedido do dono: *"ao concluir já prossiga para todo o restante de casas, ruas etc de todo o mapa"* e *"Sim, seguir"* — trecho leste da avenida: **Av. Jaime Brasil, de x 1247 a 1541, os dois lados** (o comércio a leste do Centro Cívico; até x ≈ 1243 o mapa do parceiro já tem lojas e não foi tocado).

**Resposta curta: as duas frentes estão na cena em dois marcos novos, `BVC_JB_NORTE` e `BVC_JB_SUL`: 65 unidades, 17.698 triângulos, 32 contornos do cadastro deixaram de ser volume provisório (mais 1 falso prédio retirado).** Um revisor independente conferiu a primeira versão nos 58 pares captura × cena e achou 32 diferenças (9 graves, 14 médias, 9 leves); tratei as que tinham medida, e **a versão corrigida só eu conferi**. Não é 100 % fiel: o canteiro central (quiosques, estacionamento, árvores) não foi feito, as laterais para as ruas transversais também não, e o chão da planta é plano onde a rua real desce. **Nada foi jogado** (sem build) e nada foi commitado.

## 0. Aviso: pastas de apoio que sumiram

Ao rodar a importação descobri que **`game\_backup` (cópias das rodadas 11 a 28) e `game\_diag` não existiam mais**. Conferi o histórico: nenhum comando desta sessão nem dos agentes apagou essas pastas; as pastas `favela\`, `game\unity\` e `Assets\` foram mexidas por fora entre 01h24 e 01h26 de 13/11, quando nenhuma sessão estava agindo, e o disco C: estava com 4,9 GB livres. Não sei quem ou o que removeu.

- **Recriado** para poder importar: a cena-base (`_backup\bvcity_r11_20261111\Map_bv_city.unity`, copiada de `F:\FreeBala_backups\bvcity_20261111_pre_reconstrucao`, mesmo tamanho do original: 21.478.895 bytes) e o script de lote (`_diag\roda_bvplanta.ps1`, conteúdo recuperado do histórico).
- **Não recuperado**: `NavMesh_bv_city.asset`, `fb_data*.json` e `marcos_antes` do backup r11; os backups das rodadas 25, 26, 27 e 28.
- Dados de trabalho (`tools\bv_city`) e o projeto Unity estão inteiros. Há uma nota em `_backup\LEIA-ME_recriado_20261113.txt`.

## 1. Método desta rodada (mudou)

Nas rodadas 25 a 28 eu modelava e o revisor achava 15 a 20 erros de medida. Desta vez inverti: **um levantador mediu primeiro** (64 vistas do Street View de fev/2025, posição da câmera lida na URL, triangulação entre vistas vizinhas, câmera calibrada a 2,3 m) e entregou 58 unidades com x, plano da parede, alturas, faixas e aberturas; **um conversor** (`monta_spec_jb.py`) transformou isso na especificação do gerador sem redigitar medidas; **um revisor** conferiu a cena contra as capturas e levantou os trechos que o levantador não tinha visto de perto (10 vistas novas).

Achado principal do levantamento: dos dois lados **as lojas ficam recuadas 2 a 3 m atrás de uma linha de testeiras e marquises**. A parede norte está em y −32,0 a −33,8 e a sul em y −4,9 a −7,8 (26 m de parede a parede); o cadastro automático põe a frente 2,1 a 2,7 m ao sul disso nos dois lados. Em x o cadastro bate em 0,5 m em oito prédios conferidos.

A comparação agora é feita **do mesmo ponto**: para cada captura há uma foto da cena com a câmera na posição lida na URL (`06_unity/cams_extra.txt`, 58 câmeras).

## 2. O que entrou

**Lado NORTE** (32 unidades)

| x | Unidade | Altura | Fundo | Confiança |
|---|---|---|---|---|
| 1247,0 a 1279,8 | shopping center hum | 5,6 m | 26,6 m (cadastro) | media |
| 1279,8 a 1283,8 | capas e acessorios | 8,8 m | 23,5 m (cadastro) | media |
| 1283,8 a 1297,0 | top calcados esquina | 8,8 m | 26,6 m (cadastro) | media |
| 1319,6 a 1327,3 | importadora dinelly | 5,7 m | 5,5 m (cadastro) | media |
| 1327,3 a 1338,0 | oticas carol | 11,8 m | 15,5 m (cadastro) | baixa |
| 1338,1 a 1344,0 | rei das novidades norte | 11,8 m | 24,8 m (cadastro) | media |
| 1344,1 a 1347,8 | ys jeans | 6,0 m | 18,7 m (cadastro) | media |
| 1347,9 a 1353,0 | baratao dos oculos | 6,8 m | 18,7 m (cadastro) | media |
| 1353,2 a 1358,8 | foto roraima | 5,7 m | 8,2 m (cadastro) | media |
| 1358,9 a 1362,2 | claro revenda | 6,3 m | 8,2 m (cadastro) | media |
| 1362,3 a 1369,6 | loja boa vista | 5,6 m | 8,2 m (cadastro) | media |
| 1369,7 a 1374,0 | otica caixa azul | 5,5 m | 10,9 m (cadastro) | media |
| 1374,0 a 1380,3 | terreno gradeado v01 | 3,4 m | — | baixa |
| 1380,3 a 1386,4 | fama | 5,4 m | 12,0 m (estimado) | media |
| 1386,5 a 1397,6 | shopping fortaleza | 8,5 m | 12,0 m (estimado) | media |
| 1397,6 a 1404,9 | claro | 5,9 m | 12,0 m (estimado) | media |
| 1405,0 a 1412,5 | tim | 7,5 m | 18,0 m (cadastro) | baixa |
| 1412,8 a 1414,5 | casa estreita entre tim e iclass | 6,6 m | 8,5 m (cadastro) | media |
| 1415,4 a 1419,7 | iclass | 5,5 m | 8,5 m (cadastro) | baixa |
| 1419,7 a 1425,9 | on tech | 5,6 m | 8,5 m (cadastro) | media |
| 1425,9 a 1432,5 | agibank | 7,0 m | 6,0 m (cadastro) | alta |
| 1432,6 a 1440,6 | neo | 6,5 m | 6,6 m (cadastro) | alta |
| 1440,6 a 1452,7 | predio concreto janela oval | 6,4 m | 10,7 m (cadastro) | alta |
| 1470,1 a 1477,6 | mrtur | 7,0 m | 17,1 m (cadastro) | media |
| 1477,6 a 1483,5 | rei dos oculos | 7,0 m | 17,2 m (cadastro) | media |
| 1483,5 a 1488,9 | live | 6,3 m | 17,2 m (cadastro) | media |
| 1488,9 a 1491,0 | casa estreita branca | 4,8 m | 20,5 m (cadastro) | media |
| 1491,1 a 1495,4 | vip cell | 5,2 m | 20,5 m (cadastro) | media |
| 1495,4 a 1499,5 | foco solucoes | 5,5 m | 20,5 m (cadastro) | media |
| 1499,5 a 1500,9 | frente estreita cinza | 5,1 m | 20,5 m (cadastro) | media |
| 1501,0 a 1518,0 | academico news | 8,1 m | 16,9 m (cadastro) | media |
| 1518,0 a 1541,0 | academico anexo | 6,0 m | 23,1 m (cadastro) | media |

**Lado SUL** (33 unidades)

| x | Unidade | Altura | Fundo | Confiança |
|---|---|---|---|---|
| 1246,5 a 1252,3 | otica mido | 6,0 m | 12,0 m (estimado) | media |
| 1252,3 a 1259,2 | explosao modas | 6,6 m | 23,3 m (cadastro) | media |
| 1259,2 a 1263,9 | grifestock | 6,5 m | 23,4 m (cadastro) | media |
| 1264,0 a 1267,5 | lojas best | 5,8 m | 12,0 m (estimado) | baixa |
| 1267,6 a 1276,0 | milano | 6,4 m | 12,0 m (estimado) | media |
| 1276,6 a 1285,2 | homem street | 14,0 m | 35,0 m (cadastro) | media |
| 1285,2 a 1295,2 | rei das novidades sul | 8,6 m | 35,0 m (cadastro) | media |
| 1324,7 a 1335,3 | jardim sobrado w01 | 0,8 m | — | baixa |
| 1332,8 a 1335,3 | varanda sobrado | 7,2 m | 8,0 m (estimado) | baixa |
| 1335,3 a 1344,1 | sobrado historico | 7,5 m | 18,9 m (cadastro) | media |
| 1344,1 a 1345,2 | passagem sobrado w02 | 2,2 m | — | baixa |
| 1345,2 a 1355,2 | freire | 6,0 m | 12,0 m (estimado) | media |
| 1355,2 a 1365,8 | predio frontao escalonado | 6,1 m | 22,0 m (cadastro) | media |
| 1365,8 a 1367,7 | portal verde | 7,0 m | 21,9 m (cadastro) | media |
| 1367,7 a 1383,9 | associacao comercial | 9,3 m | 22,2 m (cadastro) | media |
| 1384,5 a 1390,6 | oticas diniz | 5,8 m | 13,1 m (cadastro) | media |
| 1390,6 a 1396,3 | otica veja | 6,5 m | 12,9 m (cadastro) | media |
| 1396,3 a 1400,6 | svk cell | 8,4 m | 12,5 m (cadastro) | media |
| 1400,8 a 1414,0 | junta comercial | 13,8 m | 12,9 m (cadastro) | media |
| 1414,0 a 1418,4 | o boticario | 8,6 m | 18,9 m (cadastro) | media |
| 1418,4 a 1424,2 | design celulares | 6,0 m | 19,1 m (cadastro) | media |
| 1424,3 a 1428,7 | loja brasil | 5,2 m | 19,1 m (cadastro) | media |
| 1428,7 a 1434,6 | maria flor | 4,5 m | 12,0 m (estimado) | media |
| 1434,6 a 1441,1 | big cell | 6,8 m | 7,8 m (cadastro) | media |
| 1441,1 a 1447,3 | loja de esportes | 5,7 m | 7,8 m (cadastro) | baixa |
| 1447,3 a 1455,1 | aracati lanches | 4,7 m | 8,2 m (estimado) | media |
| 1470,0 a 1476,8 | casa antiga primetech | 7,4 m | 18,6 m (cadastro) | media |
| 1475,0 a 1479,2 | click cell | 5,3 m | 20,7 m (cadastro) | media |
| 1479,2 a 1480,2 | passagem bege w03 | 2,2 m | — | baixa |
| 1480,2 a 1492,7 | predio bege antigo | 5,7 m | 12,0 m (estimado) | media |
| 1492,7 a 1494,6 | muro com portinha | 2,5 m | 4,0 m (estimado) | alta |
| 1494,8 a 1536,9 | lojas americanas | 10,0 m | 20,7 m (cadastro) | media |
| 1536,9 a 1553,3 | fundo loja departamentos | 4,0 m | 15,2 m (estimado) | baixa |

Peças próprias: galeria de testeira azul com letras soltas nas posições medidas e dois volumes amarelos por cima; prédio de concreto com vitrine oval e lateral laranja; casa antiga de empena para a rua sobre plataforma com três degraus; prédio branco de frontão em degraus; Associação Comercial com nove aletas brancas; edifício público de brises azuis; sobrado histórico com varanda (estimada) e mureta do jardim; loja de departamentos com barra vermelha, toldo e placa; lanchonete aberta de esquina. Nomes só em fonte genérica; nenhum logotipo, brasão, foto de painel ou marca de fabricante.

## 3. O que a verificação achou e o que fiz

Erros **graves** (todos tratados, salvo o canteiro):
1. Painel preto da loja de roupa masculina no plano errado: triangulado em y −7,2 → avança 2,2 m e vai só até 6,75 m (estava colado na parede, até 9 m).
2. A "loja alta de ACM cinza" não é prédio de 12 m: é uma tela grafite pendurada 2,8 m à frente, de 2,95 a 8,0 m. Saiu o bloco de 12 × 15 m que não existe.
3. Três peças trocadas de lugar perto de x 1425: é uma loja só (x 1425,9 a 1432,5, painel azul); o painel cinza liso fica em 1423,8 a 1425,8.
4. Letreiro da galeria comprimido (as pontas erravam ~6 m): agora palavra por palavra, de 1251,7 a 1278,6. *Erro da minha conversão.*
5. Sobrado amarelo de cima 3,5 m a leste do lugar e 0,7 m baixo.
6. Edifício público sem os brises dos pavimentos de cima (agora em toda a largura acima de 7,7 m; altura 13,8 m).
7. Toldo da loja de departamentos com metade do comprimento (agora 1499,3 a 1512,5, inclinado).
8. Sobrado histórico sem varanda: entrou uma varanda de dois pavimentos **estimada** (só o guarda-corpo, 4,2 a 5,1 m, foi lido).
9. **Canteiro central vazio — NÃO tratado.** No real é pavimentado, com estacionamento a 45°, cinco ou seis quiosques, árvores grandes e postes; na cena é grama. As posições que tenho são grosseiras (±1,5 m).

Médios e leves tratados: letreiro escondido atrás do portal, pano de vidro só até 6,3 m com moldura de tubos, parede de tijolo 1,2 m alta demais, prédio marrom por cima de duas lojas, painéis azuis do curso em degraus, reentrância e patamar do anexo, lateral laranja do prédio de concreto, aletas da Associação, casa antiga mais larga (cumeeira medida em 1473,4), prédio bege terminando em 1492,7 com muro e portinha, duas lojas novas no lugar do trecho não visto do sul (divisa 2,7 m errada e nome errado), casa estreita entre duas lojas do norte, cor própria do térreo, saliência medida de dois painéis, porta sem observação = vão aberto.

**Não tratados** (ficam para a próxima etapa, com os dados já guardados em `verificacao_r29.json`): canteiro central; laterais das esquinas ao longo das duas ruas transversais (levantadas com plano incerto); largo do mercado e o galpão ao fundo (só pontos de chão, ±1,5 m); lateral da loja de esquina para o largo; três painéis da lateral laranja; lado oeste da mureta do jardim; declive da calçada (a rua real desce ~1,4 m de x 1502 a 1537 e o chão da planta é plano — as alturas da loja de departamentos são as do meio do prédio); caractere grande a oeste do S da galeria (não lido); nome de uma loja de celulares lido só em parte; arcos do prédio marrom, platibanda curva e quina arredondada do sobrado.

## 4. Medido, estimado, observado

- **MEDIDO** (levantador ou revisor, por triangulação ou vista de frente): x das divisas (±0,3 a 0,5 m), plano das paredes (±0,4 m), alturas das fachadas vistas de perto (±0,3 a 0,6 m; a altura da câmera carrega ±0,2 m), plano de sete painéis salientes.
- **ESTIMADO**: plano e altura dos demais painéis salientes (até 0,6 m de erro, dito em cada unidade); alturas de três prédios cujo topo sai da imagem (14 m, 13,8 m, 11,8 m); profundidade de **todos** os prédios (vem do contorno do cadastro, com teto de 15 m na altura da fachada e corpo baixo dali para trás; onde não há contorno, até 12 m); varanda do sobrado; recuo do balcão da lanchonete; passo dos brises.
- **OBSERVADO** (lido por mim numa captura, sem triangulação): larguras dos degraus do frontão, número e passo das aletas da Associação, altura do beiral da casa antiga.
- Coberturas: quase nenhuma é visível da rua; ficaram platibanda, salvo a casa antiga (duas águas de barro) e a loja de operadora com telhado de barro atrás.
- Cores: leitura visual das capturas, não amostragem calibrada. A foto da cena escurece e dessatura.

## 5. Estado da cena

0 erros de compilação; 0 falhas de malha do PhysX; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200. Importação em lote com o Unity fechado; setores `BVC_S_X3_Y-1` e `BVC_S_X3_Y0` reexportados. Sem build, sem commit.

## 6. Arquivos

- Levantamento: `02_referencias/jaime_brasil_20261112/levantamento.json` (+ 63 capturas do levantador e 10 do revisor em `consulta/`).
- Conversor e correções: `monta_spec_jb.py`, `correcoes_r29b.py`; saídas em `norte/` e `sul/`; contornos cobertos em `modelados_jb.json`.
- Gerador: `03_blender/frente_norte_3d.py` com `BVC_REF_DIR` (marco descrito só pela pasta), toldo inclinado, empena para a rua, vitrine oval, peça fina afastada da parede; letreiro de letras espaçadas em `frente_norte_letreiros.py`.
- Registro: `06_unity/registra_jb.py`; câmeras: `06_unity/cams_extra.txt`; comparações: `07_comparacoes/r29/` (`compara_r29.py --versao r29a`).
- Verificação: `09_relatorios/verificacao_r29.json`.

## 7. Próximo passo sugerido

Canteiro central e ruas transversais deste trecho (com vistas tiradas de dentro do canteiro, para medir quiosques e vagas), depois o trecho x 600 a 1247 fora do anel do Centro Cívico e a continuação a leste de x 1541 até o rio.
