# BV City — Rodada 13 (11/11/2026)

Consulta ao Google Maps / Street View, por orientação do usuário ("acesse, navegue e entenda"), para tirar as dúvidas que as fotos em disco não resolviam. Resultado: a Assembleia foi remodelada.

**Nada foi jogado** (sem build). Todo número é MEDIDO, salvo quando marcado ESTIMADO.

## 1. Como a consulta foi feita

Navegador embutido do app, Street View em cinco pontos em volta da Assembleia e uma fotosfera de usuário diante do Palácio. **Foi consulta:** nenhuma imagem, textura ou medida foi copiada do Google para o jogo. O que saiu de lá foi entendimento (de que lado fica o quê, quantos elementos, que cor), e a geometria foi modelada à mão. Esta é a minha leitura da regra "Street View só para consulta"; fica escrita aqui para poder ser contestada.

| Ponto (corredor) | Onde | Data da imagem | O que mostrou |
|---|---|---|---|
| (755; −187) | Av. Ville Roy, de frente | fev/2025 | pele de vidro curva, arcos e testeira estão nesta fachada |
| idem, olhando para o norte e para o sul | Av. Ville Roy | fev/2025 | ponta baixa da testeira ao norte; ao sul a cobertura sobe e passa da parede |
| (790; −125) e (815; −110) | lado da praça | fev/2025 | cobertura em balanço funda, pilares curvos, parede recuada com molduras brancas em arco e portal central com emblema |
| (755; −135) e (871; −134) | cantos sudoeste e sudeste | fev/2025 | a cobertura é uma casca que sobe de norte para sul; borda sul horizontal |
| (769; −5) | diante do Palácio do Governo | out/2020 (fotosfera de usuário) | entrada principal voltada para o gramado, com frontão, sacada de balaústres e seis pilares |

## 2. O que a consulta resolveu

**Assembleia.** A dúvida da rodada 12 estava certa em desconfiar: a fachada da testeira curva fica na **Av. Ville Roy** (borda oeste), não na praça, onde o modelo da rodada 1 a tinha posto. E apareceu o que o modelo não tinha: o lado da praça é uma **entrada coberta**, com a cobertura em balanço sobre pilares curvos.

**Palácio do Governo.** A entrada que está na cena (pórtico com frontão e sacada, do mapa do parceiro) confere com a foto de 2020. A frente para o gramado, que a rodada 12 deu como "observada em três imagens", está confirmada. Tirar o meu pórtico pequeno, baseado na foto antiga, foi a decisão certa.

## 3. Assembleia remodelada

Gerador `03_blender/assembleia.py` (cópias de antes: `.r11` e `.r12`).

| Item | Antes (rodadas 1 a 12) | Agora | Fonte |
|---|---|---|---|
| Fachada da testeira, vidro e arcos | borda da praça | **borda da Av. Ville Roy** | Street View |
| Cobertura | laje plana a 13,6 m + testeira em 60 % da frente | **casca que sobe de norte para sul**, sobre o contorno inteiro, com testeira na borda oeste toda | Street View; contorno pela aérea corrigida |
| Altura da cobertura no lado sul | 18,0 m (topo da testeira) | **23,9 m** | **ESTIMADO** pela proporção na foto e no Street View (1,6 a 1,7 vez o corpo) |
| Lado da praça | parede lisa com a fachada errada | **parede recuada 12 m, 8 molduras brancas em arco, portal central com emblema, 4 pilares curvos, coluna redonda no canto** | composição vista no Street View; **medidas ESTIMADAS** |
| Parede oeste | 81,3 m (o contorno inteiro) | **64,9 m** (o contorno é o da cobertura: 4 m de balanço ao norte, 12 m ao sul) | balanços **ESTIMADOS** |
| Composição da fachada oeste | relativa a 81,3 m | a mesma, relativa à parede de 64,9 m | foto CC BY (proporções) |
| Cores | arcos amarelos, embasamento ocre, gradil branco | arcos e embasamento cinza-claro, gradil escuro | Street View (a foto CC BY é anterior) |
| Posição | origem no vértice do contorno do OSM | origem no canto noroeste da parede; contorno deslocado (−5,75; +4,15) m para o telhado medido | verificação da rodada 11 |

Na cena: pivô (769,91; −222,99), giro 90°, pose **por construção**. O encaixe da planta do modelo no contorno deslocado dá 0,94, o que confere a conta da pose. 797 peças, 13.7 mil triângulos, topo a 23,8 m.

O que continua estimado ou não conferido na Assembleia:

- todas as alturas; a profundidade da entrada coberta (12 m) e o balanço norte (4 m);
- número e posição dos pilares curvos (pus 4) e a forma deles;
- a parede norte e a leste (não olhei); o anexo do recorte não está modelado;
- as treliças aparentes sob a cobertura não estão modeladas;
- o pano creme tem 3 janelas altas no modelo; a foto mostra 2 com clareza.

Comparações (`07_comparacoes/unity_r13/`): `assembleia_antes_x_depois.jpg`, `assembleia_foto_x_render.jpg`, `marco_assembleia.jpg`.

## 4. Cena depois da rodada

| Item | Valor |
|---|---|
| Marcos ativos | 5, todos nas poses da tabela abaixo |
| Colisor sob os pés (grade de 4 m, janela do corredor) | 1 ponto sem nada em 95.200 |
| NavMesh do corredor | refeito (3.475 colisores); 100 de 100 pontos do `fb_data` nele |
| Planta | sem mudança: 160 setores, 651.732 triângulos, 21.189 volumes provisórios |

| Marco | Pivô (corredor) | Giro | Frente |
|---|---|---|---|
| Assembleia | (769,91; −222,99) | 90,0° | testeira para o rumo 312° (Av. Ville Roy); entrada coberta para a praça — **conferido no Street View** |
| Palácio do Governo | (722,82; −4,77) | 270,9° | rumo 133° — **conferido** (fotosfera de 2020) |
| Tribunal de Justiça | (707,42; −191,71) | 345,4° | rumo 207° — MEDIDO |
| Catedral | (794,68; 150,92) | 243,1° | rumo 105° — MEDIDO |
| Palácio da Cultura | (933,41; −164,69) | 1,2° | rumo 223° — MEDIDO |

## 5. Correções ao relatório da rodada 12

- "Fachada da Assembleia em dúvida": resolvida (seção 2).
- "A entrada do Palácio não foi conferida com foto recente": conferida com imagem de 2020.
- A tabela de marcos da rodada 12 dava a Assembleia com giro 0° e frente para o rumo 222°: valia para o modelo antigo.

## 6. O que continua sem fazer ou sem verificar

1. **Nada foi jogado.** Sem build; `10x10 bv_city` e `paredes bv_city` precisam ser repetidos.
2. Carga dos setores dentro do jogo: não testada.
3. Catedral, Tribunal de Justiça e Palácio da Cultura: **não consultei o Street View para eles** nesta rodada. A Cultura tem a divergência de planta (38 × 33 m no modelo, 35 × 43 m no contorno) em aberto.
4. Altura do Palácio (13,70 m) continua estimada.
5. Materiais dos marcos: cores chapadas, sem textura.
6. O restante da lista da rodada 12 (NavMesh fora das zonas, degrau até 0,77 m, lotes, Orla).
7. Commit: nenhum.

## 7. Arquivos

- `03_blender/assembleia.py` e `palacio.py`; GLBs em `05_exportacoes/GLB/` e em `Assets/BURITIRO_Mapas/bv_city/marcos/`; renders em `08_renders/` (os de antes em `08_renders/_r12/`).
- `06_unity/assembleia_pose.json`, `marcos_cena.py`, `marcos_cena.json`, `compara_r13.py`.
- Para repetir a cena: restaurar de `game/_backup/bvcity_r11_20261111/` e rodar `game\_diag\roda_bvplanta.ps1 -Metodo BatchImporta -Extra "-fbsemprefab"`.
