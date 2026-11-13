# BV City — Rodada 22 (12/11/2026)

Pedido do dono: *"Sim, refazer"* — cobrir uma faixa de rolamento da rua do mapa da praça para refazer a calçada e a ciclovia na largura real.

**Resposta curta: do lado norte da avenida, de x −100 a 333, o chão entre a pista e o muro foi refeito na largura do vídeo: a guia da pista foi de 4,1 m para 7,9 m do muro.** Um revisor independente não achou nada grave na primeira versão (8 diferenças médias, 7 leves); corrigi 7 das 8 médias e 3 das 7 leves (outras 2 em parte), e **a versão corrigida só eu conferi**. Continua não sendo 100 % fiel. **Nada foi jogado** (sem build).

## 1. O que mudou na cena

| Faixa (do muro para a pista) | Antes (mapa da praça) | Agora | No vídeo |
|---|---|---|---|
| Calçada | 0,9 m | 2,76 m (mais a guia) | 2,3 a 3,0 m |
| Ciclovia | 2,0 m, no nível da calçada | 2,80 m, 10 cm acima da pista e 10 cm abaixo da calçada, entre duas guias | 2,3 a 2,9 m |
| Canteiro de grama | 1,0 m | 1,90 m (mais as guias) | 1,9 a 2,3 m com as guias |
| **Guia da pista até o muro** | **4,1 m** | **7,9 m** | **7,3 a 8,0 m** |

Também entraram: guias pintadas de branco e cinza, sarjeta de concreto, 17 aberturas do canteiro (entradas de garagem e passagens), a travessia vermelha da ciclovia nas quatro ruas transversais, a plataforma do abrigo de ônibus à beira da pista (a ciclovia desvia por trás) e o piso da calçada por trecho (bloquete, concreto, pedra; terra batida em frente ao prédio antigo, onde não há calçada pavimentada). Triângulos do piso novo: 5.566.

**No mapa da praça (do parceiro), só dentro dessa faixa:**
- O chão antigo foi retirado (asfalto da faixa de rolamento, grama, ciclovia e calçada antigas, pintura e decalques do piso).
- 316 objetos (postes com a fiação, árvores de rua, tufos de capim, motos encostadas na guia) foram levados 3,15 m para o lado da pista, para o canteiro novo; nas malhas juntadas (placas de trânsito, mobiliário), 3.665 vértices foram deslocados do mesmo tanto. Chão recortado: 4 malhas, 2.907 triângulos a menos.
- O que ficava junto ao muro (sacos de lixo, tambores) e os contêineres na pista não foram mexidos.

Tudo isso é feito na importação; o mapa original do parceiro e a cena de base não foram alterados.

## 2. Verificação independente da primeira versão

Um revisor, só leitura (`09_relatorios/verificacao_r22.json`), mediu no quadro `q_01760` e conferiu o perfil da cena: **15 achados — nenhum grave, 8 médios, 7 leves.**

**Confirmou:** as larguras (cena 7,91 m; vídeo 7,3 a 8,0 m, com ±1 m de incerteza pela focal); que não sobrou chão antigo nem buraco na faixa; postes, placas e capim dentro do canteiro novo, fiação contínua, nada flutuando.

**Médias corrigidas (r22b), 7 de 8 (o item 4 cobre dois achados; a que ficou é a mureta em x ≈ 125, na seção 3):**
1. **Nível da ciclovia.** Eu a tinha posto no nível da pista, com guias de 20 cm dos dois lados; parecia uma vala. No vídeo o degrau da calçada para a ciclovia tem 9–10 cm e a guia da pista 16–17 cm. A ciclovia subiu para 10 cm (incerteza de ±4 cm; a diferença para a pista foi inferida, não medida direto).
2. **Pontas nas ruas transversais.** A ciclovia dava de frente num resto da calçada antiga dentro da boca de rua. O chão antigo sai também ali e entrou a travessia pintada.
3. **Entrada da ESCON** estava 4,4 m a leste do pilone; no real fica ~3 m a oeste dele e tem ~2 m. Corrigida.
4. **Aberturas que faltavam:** acrescentadas 12; a da casa 646 foi estreitada e a do pórtico virou passagem de pedestre.
5. **Abrigo de ônibus:** ficava na calçada, atrás da ciclovia. Foi para uma plataforma à beira da pista, com a ciclovia desviando por trás.
6. **Piso da calçada** era concreto cinza único; agora muda por trecho.

**Leves corrigidas:** proporção calçada/ciclovia; cinza da guia (saía quase preto) e sarjeta; linha tracejada da pista colada à guia nova. **Em parte:** pontas do canteiro (ganharam guia de topo e recuo de 2 m nas esquinas, sem arredondar) e vegetação (só a cor da grama, que estava saturada demais).

## 3. O que continua diferente do real

- **A versão corrigida não passou por nova verificação.**
- **Árvores:** as 14 árvores de rua do mapa da praça foram junto para o canteiro novo. No vídeo não há nenhuma árvore no canteiro norte (as reais ficam dentro dos lotes).
- **Mureta em x ≈ 125:** uma peça da fachada avança 2,3 m sobre a calçada e deixa 0,5 m de passagem. Já era assim; não mexi nesta rodada.
- **Placas:** a de regulamentação em frente ao muro do TCE e as das esquinas ficam na calçada no real; na cena foram para o canteiro com as outras.
- Pontas do canteiro em corte reto (no real são arredondadas); sem ilhas de plantas (agave, cica) e sem lixeiras; sem pictogramas na ciclovia.
- Posição das aberturas que não têm fachada de referência: ±3 m.
- A linha tracejada da pista que ficava a 25 cm da guia nova saiu junto com o chão recortado; não conferi em foto.
- As duas pontas da faixa (x −100 e x 333) e três das quatro bocas de rua não têm foto próxima.
- O lado sul não foi mexido: lá não medi o chão.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200.

**Perfil do chão medido por raio depois da correção:** em x = 112, sarjeta_concreto a 0,0 m; guia_pintada a 0,2 m; grama_seca a 0,2 m; ciclovia_concreto a 0,1 m; pintura_branca_ciclo a 0,1 m; pintura_amarela a 0,1 m; passeio_bloquete_bege a 0,2 m. Linhas sem chão dentro da faixa, nos 10 cortes: 0.

## 4. Arquivos

- `03_blender/faixa_norte_chao.py` → `05_exportacoes/GLB/BVC_PDA_FAIXA_NORTE.glb` (pivô 110 / −44, altura 0).
- Texturas próprias novas: `guia_pintada`, `grama_seca` (regras em `bvc_texturas.py`).
- Importador: passo "faixa norte (r22)" (recorte do chão da praça e deslocamento de 3,15 m), `BatchPerfil`, `BatchListaFaixa`, câmeras `r22_*`.
- `06_unity/marcos_cena.json` (cópia `.r21`), `06_unity/perfil_calcada.txt` (antes em `07_comparacoes/r22/antes/`).
- Comparações: `07_comparacoes/r22/` (`antes/` = antes da faixa; `r22a/` = primeira versão), `06_unity/compara_r22.py`.
- Verificação: `09_relatorios/verificacao_r22.json`. O revisor avisou que gravou por engano três recortes da aérea em `02_referencias/sat/r20/` (`sat_95_135.jpg`, `sat_20_65.jpg`, `sat_200_260.jpg`); são só referência e ficaram lá.
