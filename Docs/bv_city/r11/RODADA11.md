# BV City — Rodada 11 (11/11/2026)

Planta r10 no Unity por setor, e início da Fase 4 no corredor (Praça das Águas + Centro Cívico).

**Estado em uma frase:** a cena `Map_bv_city.unity` passou a ter a planta georreferenciada dos 20,29 km² em volta das duas zonas já modeladas, carregada por setor; no caminho apareceram e foram corrigidos cinco defeitos antigos da própria cena. **Nada foi jogado**: sem build, sem partida, sem teste dentro do jogo.

> **ERRATA (rodada 12, mesma data).** A verificação independente e a leitura dos geradores derrubaram quatro afirmações deste relatório. O texto abaixo fica como foi escrito; o que vale é isto e o `RODADA12.md`:
>
> 1. **Assembleia "23 % maior, herdou a medida do parceiro, refazer a planta": ERRADO.** O corpo do modelo segue o contorno do OSM (81,3 × 80,9 m, com o recorte do anexo). O excesso era a laje de cobertura e o rufo, que seguiam a caixa envolvente e tapavam o recorte (945 m²), mais o pórtico. Corrigido na rodada 12. O que está deslocado é o **contorno do OSM deste prédio**, ~7 m em relação ao telhado na aérea corrigida.
> 2. **Palácio Hélio Campos "planta de outro prédio, medida num colisor de outra quadra": ERRADO.** O modelo da rodada 2 era do prédio certo (o gerador dizia centro em (731,4; −5,9), dentro do anel). Os erros eram outros: a tabela da rodada 7 pôs o marco em (935,9; −183,2); a planta veio do volume do parceiro, que é maior que o real (53,7 × 47,9 m contra 46 × 36,5 m medidos); a fachada longa estava no eixo errado; e o pátio interno estava tampado por laje. Refeito na rodada 12.
> 3. **Degrau planta × zona (seção 2.4): subestimado.** Medi o relevo nu, não a superfície. Com quadra e calçada (0,15 m acima do relevo), a superfície da planta fica: Praça das Águas mediana 0,15 m, máximo 0,22 m; Centro Cívico mediana 0,12 m, p90 0,39 m, máximo 0,77 m, 196 de 1.012 amostras acima de 0,30 m.
> 4. **"Sem triângulo degenerado" e volumes fora das zonas: não valiam.** Havia 900 triângulos sem área e 48 volumes provisórios atravessando o muro da praça e a borda da arena. Corrigidos na rodada 12.
>
> Também: a frente do Tribunal de Justiça passou de ESTIMADO a MEDIDO (pórtico e adro na aérea, iguais aos da foto); o lado da fachada da Assembleia está **em dúvida** (ver `RODADA12.md`).

Perímetro: mantido (decisão do usuário em 11/11: "Manter e seguir").

Todo número abaixo é MEDIDO, salvo quando marcado ESTIMADO.

---

## 1. Defeitos antigos achados nesta rodada (todos meus, das rodadas 2 a 7)

Nenhum destes tinha sido conferido de cima contra a aérea. A conferência que faltava é a de `07_comparacoes/unity_r11/`.

| # | Defeito | Como foi medido | Situação |
|---|---|---|---|
| 1 | **O Centro Cívico estava 1,5 m acima da Praça das Águas.** Na costura (x = 530) o chão do mapa do parceiro ficava entre +1,38 e +1,98 m (mediana +1,53) e o da praça em 0. | altura do grupo `Chao` do GLB do parceiro (`chao_cc.py`, 522.704 pontos) contra os colisores da cena | corrigido: o mapa do parceiro desce 1,53 m na cena do BV City |
| 2 | **Os cinco marcos estavam enterrados ~1,6 m**, porque foram postos em h = 0 sobre esse chão alto. | mesma medida | corrigido: cada marco fica na altura do chão do lugar |
| 3 | **Pivô e giro dos marcos errados** (tabela `PoeMarcos` da rodada 7). A Assembleia estava ~50 m fora do lote, em cima da avenida; a Catedral, girada ~145°; o Palácio da Cultura, girado 90°. | encaixe da planta de cada modelo no contorno do cadastro (`pose_marcos.py`) | corrigido (seção 4) |
| 4 | **O Tribunal de Justiça nunca apareceu na cena.** A regra da costura desligava tudo com 530 < x < 760 que não fosse do parceiro, e o marco estava em x = 704. | contagem na cena: 0 renderizadores ativos | corrigido: 188 objetos religados |
| 5 | **O marco "Palácio Hélio Campos" (rodada 2) não é o Palácio.** Foi posto em (935,9; −183,2), que é o lote do **Palácio da Cultura**. O Palácio do Governo fica **dentro do anel**, em torno de (724; −4), a ~210 m dali; e a planta do modelo (3.586 m²) não é a do prédio (1.267 m², IoU 0,35). A planta tinha sido "medida" num colisor do parceiro que era de outra quadra. | cadastro (contorno do OSM + dois pontos de lugar) e aérea corrigida: prédio quadrado com pátio interno em (724; −4) | **desligado.** Precisa ser refeito pelo contorno certo. A memória dizia "no jogo não há prédio aqui": havia, eu olhei o lugar errado |

Além desses, dois que eram da composição, não de um objeto:

- O mapa do parceiro traz um **pano de fundo de 7 km** (`Paisagem`: relevo próprio e quarteirões cinzas, 232.675 triângulos) que cobria a área inteira, inclusive por baixo da Praça das Águas.
- O `praca_aguas` traz **recheio genérico** em volta da praça (`PA_Cidade_Entorno`, `PA_Entorno` = o falso anel de r = 109 m, `PA_Lotes_Gerados`, `PA_Lotes_Trecho_Novo`, `PA_Predios_Muros`, árvores de fundo) e um chão único (`PA_Chao`, 1.554 × 624 m) que passava por baixo do Centro Cívico inteiro.

---

## 2. O que entrou no Unity

### 2.1 Planta por setor

| Item | Valor |
|---|---|
| Setores | 160 prefabs em `Assets/FreeBala/Resources/BVCity/Setores/` |
| Malhas | 1.216 (uma por material por setor), 1.133 com colisor |
| Triângulos | 650.854 (a planta inteira tem 686.220; a diferença é o que caiu dentro das zonas mantidas) |
| Volumes provisórios | 21.239 fora das zonas (21.545 na planta) |
| Tamanho | GLBs 41 MB; prefabs 4,6 MB |
| Posição | 320 vazios de conferência (dois por setor): erro máximo contra `(−x, h, +y)` = **0,0000 m** |
| Escala | 1 unidade = 1 metro (vem direto das coordenadas; não há fator) |

Os volumes cor de telha (`M_PROV`) são **provisórios**: contorno do cadastro extrudado com altura estimada. Não são prédios.

Exportação nova, direta do Python para GLB (`06_unity/exporta_unity.py`), sem passar pelo Blender: uma malha por material, normais chapadas, vértices soldados. A exportação de conferência pelo Blender (`05_exportacoes/planta/`) foi refeita também.

### 2.2 Carregamento por setor

`Assets/FreeBala/Runtime/World/FBBVCitySetores.cs`, na raiz `BVC_Planta` da cena. O setor entra quando o quadrado dele fica a menos de 700 m da câmera (`Resources.LoadAsync`) e é destruído a mais de 900 m. A névoa do mapa acaba em 512 m.

**Teste feito** (`stream_teste_editor.txt`): no Editor, **sem Play**, chamando o mesmo `Passo()` que o jogo chama, numa rota de 12,6 km a 80 m/s:

| Medida | Valor |
|---|---|
| Cargas / descargas | 111 / 111 |
| Máximo de setores vivos ao mesmo tempo | 22 de 160 |
| Setor vivo além do raio de saída | 0 |
| Setor a menos de 550 m ainda não carregado | 0 ao longo da rota; 11 na partida, por até 2,0 s (carrega um por passo de 0,25 s) |
| Tempo de uma carga | média 7,9 ms; pior 227 ms (a primeira) |
| Memória do Unity | 553 MB no início, 631 MB no máximo, 560 MB depois de descarregar tudo |

**O que esse teste não mede:** a carga assíncrona dentro do jogo e o engasgo de quadro. Tentei o teste em Play no Unity em lote (`BatchStream`); o Play entrou e não avançou os quadros, encerrei o processo. O teste em Play existe (`FBBVCityStreamTeste`) e precisa ser rodado com o Editor aberto ou num exe.

### 2.3 Zonas mantidas e o que saiu de cena

A planta não entra onde há mapa modelado (`06_unity/mascara_existente.geojson`):

| Zona | Limite (corredor) | Área | Chão |
|---|---|---|---|
| Praça das Águas | x −180..530, \|y\| ≤ 59 (dentro do muro de perímetro) | 8,4 ha | 0 m |
| Centro Cívico | arena de 600 × 600 m do parceiro, parte com x ≥ 530 | 34,5 ha | relevo do parceiro, −1,8 a +0,7 m depois da descida |

Saiu de cena (objeto desligado, nada apagado): `Paisagem` do parceiro (40 renderizadores); do `praca_aguas`, o fundo e os lotes gerados, mais 233 árvores de fundo, 97 capins, 84 postes, 35 caixas d'água, 26 + 14 + 3 + 2 árvores e 8 antenas que ficavam fora do muro. Recortados no muro: `PA_Desgaste_Decalques`, `PA_Sinalizacao`, `PA_Tapumes`, `PA_Vida_Detalhes`, 2 de vidro. `PA_Chao`: 1.184 → 1.032 triângulos (e o colisor dele).

Na costura x = 530: 8 ladrilhos de chão do parceiro recortados e 4 religados; 11 colisores de piso religados e recortados.

**Atrás do muro da Praça das Águas agora há volume provisório no lugar das fachadas geradas.** As fachadas eram inventadas; os volumes têm o contorno real. Nenhum dos dois é o prédio.

### 2.4 Relevo: a planta encosta nas zonas

`03_planta/relevo.py` passou a ancorar a altura nas zonas mantidas (0 na praça; chão do parceiro − 1,53 m na arena), com transição de 150 m para o relevo do modelo. A planta foi regerada.

Degrau entre a planta e a zona, ao longo dos 3,8 km de borda (`confere_costura.json`, uma amostra a cada 2 m):

| Borda | Mediana | p90 | Máximo | Acima de 0,30 m |
|---|---|---|---|---|
| Praça das Águas | 0,00 m | 0,00 m | 0,07 m | 0 de 769 |
| Centro Cívico | 0,10 m | 0,28 m | 0,62 m | 82 de 1.008 |

O degrau no Centro Cívico existe porque a malha do relevo tem nós a cada 50 m e o chão do parceiro varia mais fino. **A transição de 150 m em volta do Centro Cívico é artificial** (o relevo do parceiro não é o meu); some quando o setor for refeito sobre a planta.

### 2.5 Chão, colisão e NavMesh

- Colisor sob os pés, grade de 4 m na janela do corredor (95.200 pontos, com todos os setores carregados): **64 pontos sem nada embaixo (0,07 %)**. Todos em água: três piscinas, um lago e a linha de um igarapé. **Água não tem colisor nem leito** em lugar nenhum da planta, o Rio Branco inclusive.
- NavMesh do corredor assado de novo (3.068 colisores). Os **100 pontos do `fb_data`** (nascimentos, bots, pontos, rotas) ficam a menos de 1,5 m dele. Os 51 pontos do lado do Centro Cívico desceram 1,53 m no `fb_data` (só o bloco `bv_city` mudou; conferido chave a chave).
- **O NavMesh cobre só as duas zonas.** A planta não tem NavMesh: bot não sai das zonas.

---

## 3. Conferência de cima: real × antes × depois

Em `07_comparacoes/unity_r11/` (aérea só para consulta; mesma janela e mesma orientação nos painéis):

- `corredor_real_antes_depois.jpg` — o corredor, 1.600 × 950 m.
- `perimetro_real_x_unity.jpg` — a área inteira, 160 setores carregados.
- `marco_catedral.jpg`, `marco_cultura.jpg`, `marco_tj.jpg`, `marco_assembleia.jpg` — cada marco com o contorno do cadastro.
- `palacio_governo_lugar_certo.jpg` — onde o Palácio do Governo fica.

O que as imagens mostram: a malha radial e os anéis da planta caem onde estão na aérea; os quatro marcos ativos caem no contorno do cadastro; a costura da praça com o Centro Cívico ficou contínua (`06_unity/r11_costura_pa_cc.png`).

---

## 4. Marcos: pose por encaixe

A planta de cada modelo (peças com mais de 2,5 m) foi encaixada no contorno do cadastro por giro + translação. Em planta retangular o encaixe não diz para que lado é a frente; essa escolha está na última coluna.

| Marco | Pivô antes → depois (corredor) | Giro antes → depois | IoU | Área modelo / contorno | Frente |
|---|---|---|---|---|---|
| Catedral | (797,3; 163,6) → (794,68; 150,92) | 28,0° → 243,1° | 0,82 | 1,23 | rumo 105°. MEDIDO: adro a leste na aérea; a rodada 4 tinha medido 103,8° |
| Palácio da Cultura | (926,0; −176,5) → (933,41; −164,69) | 270,0° → 1,2° | 0,77 | 1,08 | rumo 223°. MEDIDO: a rodada 5 tinha medido 221,8°; endereço "Praça do Centro Cívico, 84" |
| Tribunal de Justiça | (704,4; −203,9) → (707,42; −191,71) | 326,7° → 345,4° | 0,97 | 1,06 | rumo 207°, voltado para a praça. **ESTIMADO** (a outra opção é 27°) |
| Assembleia | (814,0; −193,0) → (774,24; −151,65) | 83,3° → 2,1° | 0,81 | **1,23** | rumo 224°, voltado para a praça. **ESTIMADO** (endereço "Praça do Centro Cívico, s/n"; planta quase quadrada, quatro opções com o mesmo IoU) |
| Palácio Hélio Campos | — | — | 0,35 | 2,83 | **desligado** (seção 1, item 5) |

Problemas que ficam:

- **A Assembleia é 23 % maior em planta que o contorno real** (6.914 m² contra 5.607 m²) e não tem o recorte do anexo no canto. O modelo da rodada 1 herdou a medida do prédio do parceiro, que já era +27 %. Está no lote certo, mas transborda ~7 m. **Refazer a planta.**
- **A Catedral também dá 1,23** contra o contorno do OSM; aqui parte é beiral e marquise, não medi quanto.
- O valor da correção da Catedral e da Cultura pela aérea (rodada 10: ~6,9 e ~6,3 m para leste) ficou confirmado por outro caminho: o Palácio da Cultura corrigido cai a 1 m do centro do contorno do OSM.

Debaixo de cada marco, a versão antiga do prédio, que o parceiro tem fundida nas malhas de quadra, foi retirada (o piso ficou):

| Marco | Objetos desligados | Malhas / triângulos retirados | Colisores / triângulos |
|---|---|---|---|
| Assembleia | 1 | 23 / 7.225 | 12 / 1.091 |
| Tribunal de Justiça | 8 | 18 / 19.754 | 10 / 3.176 |
| Catedral | 7 | 9 / 7.265 | 10 / 1.980 |
| Palácio da Cultura | 1 | 8 / 2.771 | 5 / 1.084 |

São 121 malhas derivadas em `Assets/BURITIRO_Mapas/bv_city/planta/recortes/`; os GLBs dos mapas originais não foram tocados. Sobrou um ou outro triângulo solto perto dos marcos (visível na foto da Catedral).

---

## 5. Relatório por setor (os dois da Fase 4)

Os 160 setores da planta continuam em `RELATORIO_SETORES.md` (regerado). Abaixo, as duas zonas já modeladas.

```
SETOR: Praça das Águas (zona mantida: x −180..530, |y| ≤ 59; 8,4 ha)

Planta geográfica:      feita (r10/r11); posição conferida na aérea corrigida
Ruas catalogadas:       35 trechos do OSM, 13 nomes (Av. Cap. Ene Garcez, Av. Major Williams,
                        Av. Terêncio Lima, R. Ajuricaba, R. Alfredo Cruz, R. Araújo Filho,
                        R. Barão do Rio Branco, R. Cecília Brasil, R. Cerejo Cruz, R. Coronel Mota,
                        R. Coronel Pinto, R. José Ribeiro da Silva, R. Pedrinho Filho)
Lotes identificados:    0 (não há fonte aberta de lotes)
Edificações no cadastro: 45 contornos (23 OSM, 21 Google, 1 Microsoft); 6 conferem na aérea,
                        1 diverge, 38 sem medida
Edificações modeladas:  as do mapa praca_aguas (rodadas de outubro); nenhuma nova nesta rodada
Edificações verificadas contra a planta: 0
Fachadas pendentes:     todas. As fachadas geradas atrás do muro saíram; no lugar há volume provisório
Comparações:            corredor_real_antes_depois.jpg
Problemas encontrados:  recheio genérico em volta; chão único por baixo do Centro Cívico;
                        entre x 530 e 757 objetos da praça (trecho Portal–Terminal) ainda se
                        sobrepõem ao mapa do parceiro; largura das vias do jogo × planta não
                        refeita (no corredor inteiro a planta dá em média 2,4 m a menos que o jogo)
Correções aplicadas:    recheio desligado; chão e malhas grandes recortados no muro; planta em volta
Próximas tarefas:       conferir objeto a objeto o que há dentro do muro contra os 45 contornos e as
                        vias medidas; resolver a faixa 530–757; decidir o muro de perímetro
```

```
SETOR: Centro Cívico (arena do parceiro com x ≥ 530; 34,5 ha)

Planta geográfica:      feita; posição conferida na aérea corrigida
Ruas catalogadas:       72 trechos do OSM, 18 nomes (Praça do Centro Cívico, Av. Ville Roy,
                        Av. Cap. Júlio Bezerra, Av. N. Sra. da Consolata, Av. Getúlio Vargas,
                        Av. Benjamin Constant, Av. Jaime Brasil, Av. Glaycon de Paiva e outras)
Lotes identificados:    0
Edificações no cadastro: 263 contornos (112 OSM, 151 Google); 13 conferem, 40 divergem, 210 sem medida
Edificações modeladas:  4 marcos levantados em cena (Catedral, Palácio da Cultura, Tribunal de
                        Justiça, Assembleia); o restante é o mapa do parceiro
Edificações verificadas: 4 em POSIÇÃO e eixo (IoU 0,77 a 0,97); 0 em fachada e altura nesta rodada
Fachadas pendentes:     259 contornos sem levantamento; Palácio do Governo a refazer;
                        Assembleia com planta 23 % maior
Comparações:            marco_*.jpg, palacio_governo_lugar_certo.jpg, corredor_real_antes_depois.jpg
Problemas encontrados:  mapa 1,5 m alto; marcos enterrados, fora de lugar e um desligado sem querer;
                        marco do Palácio no lote errado; pano de fundo de 7 km
Correções aplicadas:    descida de 1,53 m; pose dos 4 marcos por encaixe; versão antiga retirada
                        debaixo deles; pano de fundo desligado; chão e colisor na costura
Próximas tarefas:       refazer o Palácio do Governo em (724; −4) pelo contorno; refazer a planta da
                        Assembleia; confirmar o lado da frente do TJ e da Assembleia; conferir o
                        prédio do parceiro que hoje ocupa o lugar do Palácio; comparar os 263
                        contornos com o que o mapa do parceiro tem
```

---

## 6. O que NÃO foi feito ou não foi verificado

1. **Nada foi jogado.** Sem build (não foi pedida) e sem partida. Os testes `10x10 bv_city` e `paredes bv_city` da rodada 8 valem para a cena antiga; a cena mudou e eles precisam ser repetidos.
2. Carregamento por setor **dentro do jogo** (assíncrono, com engasgo medido): não testado.
3. Limites do mapa no código (`FBWorld.GetPlayableBounds`), minimapa e capa: como estavam. O mapa "jogável" continua sendo o corredor.
4. NavMesh fora das duas zonas: não existe.
5. Água sem leito e sem colisor. Orla Taumanan continua sem a laje elevada.
6. Faixa x 530–757: objetos da Praça das Águas e do parceiro sobrepostos (Mini Terminal). Não estudei.
7. `Vegetacao` (grama/árvore instanciada do `praca_aguas`): não conferi se desenha algo fora do muro.
8. Lotes: 0. Fachadas novas: 0. Street View: não consultado nesta rodada.
9. Os 1.345 contornos que divergem da aérea e a taxa de telhado sem contorno: como na rodada 10.
10. Commit: nenhum. Tudo do BV City segue fora do git do projeto.

## 7. Arquivos

Unity (projeto `unity/FreeBala`):

- `Assets/FreeBala/Runtime/World/FBBVCitySetores.cs` — carregador por setor.
- `Assets/FreeBala/Runtime/Teste/FBBVCityStreamTeste.cs` — teste em Play (não rodou em lote).
- `Assets/FreeBala/Editor/FBBVCityPlanta.cs`, `FBBVCityPlantaImporta.cs` — cobertura, importação, cena, fotos, teste.
- `Assets/FreeBala/Resources/BVCity/Setores/*.prefab` (160), `Assets/BURITIRO_Mapas/bv_city/planta/{glb,mats,recortes}`.
- `Assets/FreeBala/Scenes/Map_bv_city.unity`, `Assets/BURITIRO_Mapas/bv_city/NavMesh_bv_city.asset`, os dois `fb_data.json` — alterados.
- Cópia de antes: `game/_backup/bvcity_r11_20261111/` (cena, NavMesh, os dois `fb_data`). A de antes da rodada 10 continua em `F:\FreeBala_backups\bvcity_20261111_pre_reconstrucao\`.

Ferramentas (`tools/bv_city/06_unity/`): `exporta_unity.py`, `ancora.py`, `chao_cc.py`, `pose_marcos.py`, `pose_frente.py`, `marcos_r11.py`, `fbdata_r11.py`, `confere_costura.py`, `compara_r11.py`, `nos_caixas.py`; resultados `importa_resumo.txt`, `stream_teste_editor.txt`, `confere_costura.json`, `marcos_r11.json`, `zonas_inventario.json`.

Para repetir: restaurar a cena da cópia, depois
`powershell -File game\_diag\roda_bvplanta.ps1 -Metodo BatchImporta` (17 min; com `-Extra "-fbsemprefab"` reaproveita os prefabs).
