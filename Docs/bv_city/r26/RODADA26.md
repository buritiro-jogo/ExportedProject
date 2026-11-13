# BV City — Rodada 26 (12/11/2026)

Pedido do dono: *"Sim, árvores"* — pôr as árvores e sebes do lado sul, que era o que mais faltava.

**Resposta curta: há 129 itens de vegetação no lado sul (110 árvores e palmeiras e 19 arbustos), mais as sebes.** A primeira versão, que fiz só pela aérea antiga, estava errada em porte e posição: um revisor independente (26 vistas próprias do Street View de 2025) mediu os troncos um a um e eu refiz a lista com as medidas dele. **A versão refeita só eu conferi.** A espécie não foi confirmada e o modelo usado é outro (oiti). **Nada foi jogado** (sem build) e nada foi commitado.

## 1. O que entrou

| Onde | Quantos | Fonte |
|---|---|---|
| Fileira do batalhão (x −945 a −176) | 96 árvores e 1 coqueiro | troncos medidos no Street View de 2025 (125 itens no total, x ±1 a 3 m); 3 só com imagem de 2019; 4 pela aérea, nos trechos que o revisor não visitou |
| Arbustos na frente da mureta (x −514 a −372) | 19 | x medidos no Street View |
| Margem do igarapé | 1 | medido |
| Em frente ao muro azul | 3 | medido (alturas 5,7 a 7,4 m) |
| Em frente ao terreno murado | 3 | medido (5,5 m) |
| Escola | 6 (mangueira grande, 3 árvores, 2 palmeiras pequenas) | só o que se vê da rua |

- **Sebes:** contínua a leste do igarapé (x −273 a −172, 1,3 m) e, a oeste, só de x −365 a −299,5 (1,05 m); dois canteiros de sebe na entrada do quartel da PM.
- **Alturas:** medidas pelo revisor quando havia (33 árvores); nas demais, a altura típica do trecho: 8 m a oeste de x −800, 5,6 m até −600, 5,1 m até −340, 4 m a leste de −240.
- **Posição transversal:** todas numa linha reta 1,15 m atrás da calçada da cena (o revisor mediu 2,2 a 3,0 m da guia).
- **Modelos:** clones do oiti, da mangueira, do coqueiro e do arbusto que já existem no mapa do Centro Cívico (o mapa original não foi alterado). O modelo só tinha o nível de detalhe de perto e sumia a ~30 m: os clones ficam visíveis até ~250 m. Árvores ganharam colisor de tronco.

## 2. Verificação independente da primeira versão

Arquivo: `09_relatorios/verificacao_r26.json`. 11 achados: na vegetação, 3 graves, 4 médios e 2 leves; nas correções da rodada 25, 2 leves.

**Graves, os três corrigidos:**
1. **Árvores 60 a 90 % grandes demais de x −793 a −340.** Eu tinha usado a copa da aérea com um fator de crescimento; as reais são jovens (4,2 a 7,2 m). Só o trecho oeste tem árvores do tamanho do modelo.
2. **A leste do igarapé (x −235 a −176), 100 a 150 % grandes demais:** as reais são mudas de 2,8 a 5,4 m.
3. **Escola errada:** eu tinha 14 árvores espalhadas pelo pátio, 7 a 34 m atrás do gradil. Da rua vê-se outra coisa: uma mangueira muito grande junto ao gradil, três árvores na ponta oeste e gramado com palmeiras pequenas. A causa foi minha: desloquei as copas da aérea em y para o lado errado.

**Médios, tratados:** fileira em linha reta junto à calçada (eu as tinha espalhado pelo gramado); fileira mais cheia que a minha (94 troncos contra 72 em 634 m): agora entram os troncos medidos; sebe a oeste do igarapé só é contínua de x −365 em diante, o resto são arbustos isolados; árvores que faltavam em frente ao muro azul, ao terreno murado e na margem do igarapé.

**Leves:** mureta do batalhão entre o igarapé e a portaria baixada para 0,95 / 1,5 m; o gradil branco do prédio cinza atravessava uma casa do mapa do Centro Cívico e voltou a terminar em x 604.

**O revisor também reconferiu as 15 correções da rodada 25** nas fotos e com 11 medidas novas da mureta: todas aparecem tratadas, sem peça flutuando.

## 3. O que continua diferente do real

- **A versão refeita não passou por nova verificação.**
- **Espécie:** provável munguba (confiança ~75 % do revisor); o modelo é um oiti, mais alto e aberto. No trecho oeste ele foi achatado (copa mais larga que alta).
- A mangueira da escola usa o modelo de mangueira ampliado para 15 m; a real tem copa de ~20 m.
- Na calçada em frente à escola o real tem uma árvore em x ~400; a cena tem uma do mapa da praça em x ~410 (não mexi).
- Em frente ao muro azul há só ~2 m entre a calçada e o muro na cena (no real, ~8 m): as árvores ficaram a 1,1 m do muro.
- Sebe: caixa verde lisa, sem as flores amarelas; os canteiros da PM são blocos cheios (os reais são anéis em volta de dois bustos).
- Não postos: árvores de dentro do batalhão e do terreno murado, capim das margens do igarapé, abrigos de ônibus, concertina da tela.
- Trechos da fileira sem vista do revisor (x −806 a −789, −618 a −612, −466 a −452, −412 a −402, −240 a −227): árvores pela aérea antiga.
- Em frente ao prédio cinza há uma faixa de gramado da planta entre a calçada e o gradil que no real não existe.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200; 129 de 129 itens de vegetação postos, 110 com colisor de tronco.

## 4. Arquivos

- `06_unity/monta_arvores_sul.py` → `06_unity/arvores_sul.json`; `01_georef/scripts/arvores_aerea_sul.py` → `02_referencias/arvores_sul_20261112/`.
- Importador: passo "árvores do lado sul" (`PlantaArvoresSul`, raiz `BVC_Arvores_Sul`), câmeras `r26_*`.
- Gerador: sebe ao longo do gradil (`ajuste_linha.json`, chave `sebe`), material `cerca_viva`.
- Comparações: `07_comparacoes/r26/` (`antes/` = rodada 25; `r26a/` = primeira versão das árvores), `06_unity/compara_r26.py`.
- Cópia de segurança: `_backup/bvcity_r26_20261112/`.
