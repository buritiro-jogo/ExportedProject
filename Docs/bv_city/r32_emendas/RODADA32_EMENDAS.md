# BV City — Auditoria das emendas do mapa unificado (13/11/2026) — 1ª passada

Pedido do dono: *"na parte de unificação dos mapas, algumas partes ficaram de fora; faça essa verificação nas partes de cada unificação, para entender melhor o que tá faltando e o que tá errado, usando o maps e o street view"*.

**Estado desta passada:** feita com a cena (31 fotos de cima ao longo de todas as emendas), o cadastro de prédios reais, leitura por raio do que a cena tem em cada prédio e a aérea real que já estava baixada. **A conferência no Google Maps e no Street View ainda não foi feita**: o navegador está ocupado com o levantamento da quadra oeste da Av. Jaime Brasil; ela entra em seguida, emenda por emenda, sobre a lista abaixo. Nada foi alterado no mapa nesta auditoria.

## 1. Quais são as emendas

O BV City junta três coisas: o mapa da **Praça das Águas** (faixa x −100 a 530, |y| < 49), o mapa do **Centro Cívico** (um quadrado de 600 × 600 m **girado ~42°**, com pontas em (1256; −14), (854; −460), (530; −169), (530; 76) e (810; 387)) e a **planta genérica** (ruas do OpenStreetMap + volumes provisórios de 21.586 prédios do cadastro), que preenche o resto. São 3,7 km de emenda.

## 2. O que está faltando — a causa é uma só

A zona do Centro Cívico é um quadrado girado que **corta as quadras em diagonal**. Para não haver prédio da planta atravessando a borda, o exportador tira **inteiro** todo volume provisório que encosta na zona (regra da rodada 12). Só que o mapa do Centro Cívico acaba na diagonal. Resultado: do lado de fora da linha fica uma **faixa vazia**, e do lado de dentro ficam **prédios cortados ao meio**.

Conta feita prédio a prédio (cadastro × leitura por raio da cena), só ao longo das bordas do Centro Cívico:

| Lado da zona | Prédios reais cortados (só a metade de dentro existe) | Faltam inteiros | Área que falta |
|---|---|---|---|
| Nordeste, de (1256; −14) a (854; −460) | 16 | 4 | 4.546 m² |
| Sudeste, de (810; 387) a (1256; −14) | 12 | 6 | 2.885 m² |
| Sudoeste, de (530; 76) a (810; 387) | 7 | 1 | 1.283 m² |
| Noroeste, de (854; −460) a (530; −169) | 5 | 0 | 484 m² |
| **Total nas bordas** | **40** | **11** | **~9.200 m²** |

- Dentro da zona, o mapa do Centro Cívico tem construção em 209 dos 231 prédios do cadastro (90 %); os 22 que faltam são pequenos (1.870 m² no total; o maior, 881 m², em x 781–816, y 142–190).
- **Praça das Águas:** as bordas norte, sul e oeste estão limpas (ruas contínuas, fachadas modeladas nas rodadas 15 a 28). Dentro dela há 11 construções pequenas do cadastro (20 a 80 m², 375 m² no total) sem volume — parecem quiosques e bancas; a conferir.
- **Costura x 530** (Praça das Águas × Centro Cívico): sem prédio faltando. O contorno grande que a conta acusou (3.822 m²) é o conjunto da torre de telefonia, que já tem modelo próprio — alarme falso.

As fotos com cada prédio que falta pintado (laranja = metade de fora cortada; vermelho = falta inteiro; rosa = falta dentro da zona) estão em `07_comparacoes/r32_emendas/`; os pares "aérea real × cena" em `07_comparacoes/r32_emendas/par/`.

## 3. O que está errado — ruas e prédios que não batem na emenda

Lido nas fotos e na aérea antiga (que pode estar 5 a 9 m deslocada); **cada item vai ser conferido no Maps/Street View**:

1. **Rotatória duplicada no lado nordeste (x ≈ 1140–1180, y ≈ −175 a −105).** O real é um canteiro em forma de amendoim com as pistas em volta; a planta reproduz isso. O mapa do Centro Cívico tem, colada ali, uma rotatória redonda pequena em (1150; −120), cortada pela diagonal. Ficam duas versões do mesmo cruzamento lado a lado.
2. **Avenida deslocada no lado noroeste (x ≈ 725–790, y ≈ −400 a −330).** A avenida de canteiro arborizado está, no mapa do Centro Cívico, uns 15 a 20 m a leste de onde a planta e a aérea a põem: há um degrau na emenda.
3. **Avenida diagonal que morre na emenda (x ≈ 560–620, y ≈ −330 a −240, lado noroeste).** A planta traz a avenida de duas pistas; do outro lado da linha o mapa do Centro Cívico tem ruas estreitas em outro traçado e uma praça de terra; a aérea mostra uma praça arborizada.
4. **Ponta sul (x ≈ 780–870, y ≈ 340–400).** O prédio grande em T existe de verdade e foi modelado, mas a ponta do quadrado o corta; a malha dele passa da linha e fica por cima de um volume da planta.
5. **Canto sudoeste (x ≈ 530–600, y ≈ 60–130).** A planta tem ali uma área lilás de estacionamento e o mapa do Centro Cívico um galpão grande; a aérea mostra terreno de terra e um prédio em L uns 30 m a leste. A conferir.
6. **Ponta leste (x ≈ 1165–1256, Av. Jaime Brasil).** A quadra do mapa do Centro Cívico é inventada (guias 2,2 a 2,6 m fora, prédio 5 m dentro da rua). Já em conserto: rodadas 29 a 32.
7. **Ruas que batem** (diferença pequena nas fotos): transversal em (1085–1100; −178), avenida com canteiro em (1000–1012; −290 e −180), rua em (640–700; 230–270), as duas avenidas da ponta sul (x ≈ 760 e x ≈ 880) e a ponta norte (854; −460).
8. **Costura x 530:** as pistas da avenida continuam, mas a pista do lado do Centro Cívico é mais estreita; a área pavimentada lisa em x 530–560 é, na aérea, parte da praça com árvores.

## 4. O que proponho (nada disso foi aplicado)

1. **Devolver o que foi cortado** — conserto mecânico, sem inventar: o volume provisório que encosta na zona deixa de sair inteiro; fica a parte de fora da linha (40 prédios) e fica inteiro onde o mapa do Centro Cívico não tem nada no lugar (11 prédios).
2. **Trocar a borda em diagonal por uma borda que segue rua e quadra**, para nenhuma quadra ficar metade de um mapa e metade de outro. Onde o mapa do Centro Cívico contradiz o real (itens 1, 2, 3 e 6), encolher a zona dele e usar a planta + trechos medidos, como está sendo feito na Av. Jaime Brasil.
3. **Conferência no Maps/Street View** dos itens da seção 3 e dos 22 + 11 prédios pequenos, antes de mexer.

## 5. Arquivos

- Fotos e pares: `07_comparacoes/r32_emendas/` (31 fotos anotadas) e `.../par/` (31 pares aérea × cena).
- Dados: `06_unity/emendas_cams.json` (câmeras e zonas), `emendas_cadastro.json`, `emendas_classes.json` (classe de cada prédio), `emendas_resumo.json`, `emendas_tabela.json`.
- Método: câmera a 115 m sobre a linha de cada emenda; para cada prédio do cadastro a até 70 m de uma zona, um raio de cima para baixo na parte de dentro e na parte de fora da zona; "tem construção" = primeiro obstáculo mais de 1,8 m acima do chão. Limite: construção sem colisor não é vista pelo raio.
