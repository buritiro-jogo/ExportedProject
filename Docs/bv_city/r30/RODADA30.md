# BV City — Rodada 30 (13/11/2026)

Pedido do dono: *"Sim, seguir"* — canteiro central e ruas transversais do trecho da Av. Jaime Brasil (x 1247 a 1541), que a rodada 29 deixou com o chão genérico.

**Resposta curta: o chão da rua foi trocado por um medido.** Marco novo `BVC_JB_RUA` (17.892 triângulos, 10.647 m² de rua): guias das duas calçadas, asfalto, 6 ilhas do canteiro, 186 linhas de vaga, 12 faixas de pedestres, 8 quiosques, 25 postes, bancos, placas, 25 árvores e 4 laterais de esquina com letreiros. Um revisor independente conferiu a primeira versão em 40 folhas e achou 24 diferenças (5 graves, 11 médias, 8 leves); **não havia nada espelhado e as guias caem a 0,1–0,35 m do real**. Tratei as que tinham medida; **a versão corrigida só eu conferi**. Não é 100 % fiel: as emendas com o resto do mapa (ponta oeste, ponta leste, fim das transversais) continuam erradas e estão descritas abaixo. **Nada foi jogado** (sem build) e nada foi commitado.

## 1. O que o levantamento mostrou

A planta genérica tinha guia norte em y −29,2, um canteiro estreito de **grama** e guia sul em −10,5. O real (33 navegações: 6 de satélite calibradas por pontos de chão e 27 vistas do Street View de fev/2025; consulta, nada copiado):

- Guias **retas**: norte y = −26,85 − 0,0092·(x − 1249); sul y = −9,75 − 0,0093·(x − 1255). De guia a guia, 17,1 a 17,3 m.
- Seção: calçada norte de 4,0 a 5,1 m | uma faixa de ~3,6 m | vagas pintadas no asfalto (motos ou carros a 45–52°) | ilha pavimentada (5 a 8 m onde há quiosque; tira de ~0,8 m com árvores entre as duas fileiras de carros) | vagas | uma faixa de ~3,4 m | calçada sul de ~4,3 m.
- A leste da segunda transversal (x > 1466) a seção muda: espinha central de ~1,9 m com mangueiras e "dedos" de guia em cada árvore.
- **8 quiosques, não 6**: dois octogonais que ninguém tinha listado e dois que estavam com a posição errada em até 12 m. Beirais a 2,5–2,8 m e cumeeiras a ~3,2 m.
- Precisão declarada da planta do canteiro: ±0,7 m (os panoramas das duas pistas discordam entre si em até 0,8 m e o satélite é oblíquo).

## 2. O que entrou

| Peça | Quantidade | Fonte |
|---|---|---|
| Asfalto, calçadas com guia, ilhas (cimento, bloquete vermelho, bloquete cinza) | faixa de 10.647 m² | guias MEDIDAS; contorno das ilhas medido em parte (a ilha entre x 1335 e 1389 é esquemática, ±1,0 m) |
| Linhas de vaga | 186 | 7 fileiras com a primeira linha na posição MEDIDA; as demais pelo passo; 5 fileiras ESTIMADAS |
| Faixas de pedestres, linha de retenção, vaga azul, rampa azul, linhas de bordo | 12 + 4 | medidas nas bocas; travessias de pista pelo satélite (±0,8 m) |
| Quiosques | 8 | posição ±0,4 a 0,7 m; alturas ±0,25 m |
| Postes de concreto (com cruzeta, braço de luminária, transformador), postes metálicos | 25 | posição ±0,4 a 3 m; **altura de 9 m é estimativa** (o topo sai do quadro) |
| Árvores | 25 | 11 com tronco visto no Street View (1 triangulado), 14 só pela copa no satélite (±2,5 m) |
| Bancos, placas, hidrante, balizador | 12 | ±0,5 a 2 m |
| Laterais de esquina | 4 | L1 e L2 (Av. Sebastião Diniz, dois lados), L3 (lateral da agência), L5 (lateral da casa antiga): planos TRIANGULADOS |

O marco **substitui** o chão da planta na faixa da rua (zona `JB` na máscara) e segue pelas transversais até onde há medida: Sebastião Diniz até y −70 (com o canteiro próprio de bloquete), Bento Brasil de y −60 a +25, largo do mercado até y +4,3.

## 3. O que a verificação achou e o que fiz

Tratados:
- **Esquina noroeste da Bento Brasil norte** cobria 3–4 listras da faixa de pedestres: refeita pela guia medida pelo revisor.
- **Ponta da ilha oeste** chanfrada cedo demais (faltava um ombro de 1,3 m): agora em arco de raio 2,7 m.
- **Transversais**: a rua genérica continuava 4,5 a 6 m deslocada logo depois da boca; o marco foi prolongado. Canteiro de grama da Sebastião Diniz trocado pela tira de bloquete medida.
- Sorveteria: octógono girado (vértice para o norte), porta e nome nas faces certas. Quiosque branco e roxo: quadrado de cantos chanfrados. Letreiro do quiosque de joias estava dentro do telhado (a parede agora sobe até o beiral medido). Telha clara e fiadas lilás no quiosque branco.
- Linhas de vaga ancoradas na posição medida; uma fileira encurtada (as linhas param em y −18,6); três fileiras do lado sul que estavam só no texto do levantamento.
- Faixas de pedestres com o passo medido (0,92 m na boca norte; 5 listras nas travessias).
- Lateral da casa antiga refeita (fila de vãos estreitos, dois janelões). Postes mudados de lugar, cruzetas perpendiculares aos fios, chapas das placas de frente para a avenida, copas maiores no trecho leste.
- Frestas cor de céu nas emendas: o marco agora passa 0,25 m por cima da borda da planta.

**Não resolvidos:**
1. **Ponta oeste (grave).** Em x 1247 o mapa vizinho (Centro Cívico, do parceiro) tem as guias 2,1 a 2,6 m fora das medidas e canteiro de terra. A pista sul nova dá de frente para a calçada antiga. O importador só retira prédios do mapa do parceiro, não o chão; consertar exige mexer naquele mapa — **decisão do dono**.
2. **Ponta leste.** A leste de x 1541 continua a rua genérica (canteiro de grama de 4,2 m); nada foi medido no chão a leste de x ~1526.
3. **Fim das transversais** (y −70, −60 e +25): a emenda só foi afastada da avenida; a rua genérica continua deslocada 2 a 6 m dali em diante.
4. **Largo do mercado**: só a baía de asfalto; o galpão do mercado e as laterais não foram levantados (não há panorama dentro do largo).
5. Palavras pintadas (TAXI, IDOSO), símbolo da vaga azul, sinalização dentro da Bento Brasil; laterais L4 e L6 (plano estimado); plataforma elevada da calçada da Bento Brasil sul; pisos diferentes das calçadas; fiação; mesas, tendas, bancas e lixeiras (móveis).
6. Declive da avenida para leste e caimento das pistas: não medidos (o chão segue o nível da planta, que é plano a leste de x 1400).

## 4. Estado da cena

0 erros de compilação; 0 falhas de malha do PhysX; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200. Setores `BVC_S_X3_Y-1` e `BVC_S_X3_Y0` reexportados com a máscara nova. Sem build, sem commit.

## 5. Arquivos

- Levantamento: `02_referencias/jaime_brasil_20261112/rua/levantamento_rua.json` (+ 38 capturas `consulta/c30_*`).
- Montador: `rua/monta_rua_jb.py` (lista do que não foi feito em `rua/monta_rua_resumo.json`); peças em `03_blender/rua_lib.py`; Blender: `03_blender/rua_blender.py`.
- Registro: `06_unity/registra_rua_jb.py` (máscara `JB`, marco, árvores em `arvores_sul.json` grupo `jb_canteiro`, câmeras).
- Comparações: `07_comparacoes/r30/` (`compara_r30.py --versao v1`); verificação: `09_relatorios/verificacao_r30.json`.
- Cópia de segurança também fora do projeto: `F:\FreeBala_backups\bvcity_r30_20261113\` (cena-base, script de lote, estado antes da rodada).

## 6. Próximo passo sugerido

Decidir a ponta oeste (mexer ou não no trecho x 1230–1256 do mapa do parceiro). Depois: continuação a leste de x 1541 até o rio (fachadas + rua, mesmo método) e as duas transversais para além do que foi medido.
