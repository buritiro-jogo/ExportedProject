# BV City — Rodada 21 (12/11/2026)

Pedido do dono: *"enquanto está rodando em segundo plano, você deve ir fazendo as estruturas do outro lado da rua da Praça das Águas"*.

**Resposta curta: a frente sul da Av. Capitão Ene Garcez, de x −91 a 332 (da Av. Terêncio Lima à R. Cecília Brasil), deixou de ser volume provisório e tem 20 unidades modeladas. É uma primeira versão feita sem vídeo próprio: não está 100 % fiel.** Um revisor independente achou 23 diferenças (7 graves) na primeira versão; apliquei as correções dele, e **a versão corrigida só eu conferi**. **Nada foi jogado** (sem build).

## 1. De onde veio cada coisa

| Informação | Fonte | Situação |
|---|---|---|
| Posição e largura de cada construção | contornos do cadastro (Google Open Buildings e OpenStreetMap) | MEDIDO (±1,5 m) |
| O que é cada construção, cores, aberturas, nomes | consulta ao Google Street View em 12/11/2026 (imagens de 2024 e 2025; o revisor leu jan e fev/2025) | OBSERVADO |
| Alturas | proporção com carros (~1,5 m), portas (~2,1 m) e, na revisão, paralaxe entre pontos do Street View | ESTIMADO (±10 a 20 %) |
| Recuos | cadastro; em dois casos, a revisão | ESTIMADO (±1,5 m) |

Nada do Google foi copiado para o jogo: a geometria foi desenhada à mão a partir do que vi e as texturas são as do projeto. Os letreiros trazem só o nome, em fonte genérica, sem logotipo. As vistas de consulta guardadas em `02_referencias/frente_sul_20261112/consulta/` servem só para as comparações.

## 2. O que entrou (de oeste para leste)

| Trecho | Unidades |
|---|---|
| Av. Terêncio Lima → R. Cerejo Cruz | clínica médica de esquina (com a parede cinza recuada e o totem preto); loja de açaí; loja de decoração; espetaria; **prédio da Roraima Energia** (81 m de frente, 7,4 m de altura, atrás de gramado e grade); prédio branco térreo com jardim |
| R. Cerejo Cruz → rua transversal em x 192 | **posto de combustível** de esquina (cobertura sobre colunas, testeira branca, amarela e verde); loja de conveniência; bloco de placas claras com marquise bege; casa atrás de muro; prédio creme rente à calçada; loja verde; **Centro Espírita Lírio dos Vales** |
| Rua transversal → R. Cecília Brasil | loja de esquina e **entrada do hotel**; loja de dois pisos cor de cobre; casa atrás de muro com portão; **conjunto de três lojas** de testeira alta; lote de muro azul-marinho; prédio cinza e laranja atrás de pilares e grade; terreno vazio de esquina |

Triângulos: 31.714. Colisores novos: 87. Saíram os volumes provisórios de 30 contornos do cadastro que essas unidades substituem.

## 3. Como foi feito

- O gerador das fachadas do lado norte passou a servir também ao lado sul (`BVC_LADO=sul`): o lado sul é gerado num referencial girado 180 graus e o resultado sai na posição real.
- A especificação fica em `02_referencias/frente_sul_20261112/monta_spec_sul.py`, com as medidas a partir da ponta oeste de cada unidade.
- Os dois setores da planta que cobrem o lado sul foram reexportados sem os volumes provisórios.

## 4. Verificação independente da primeira versão

Um revisor, só leitura, percorreu o Street View de x −90 a 330 e mediu por paralaxe (`09_relatorios/verificacao_r21.json`): **23 achados — 7 graves, 12 médios, 4 leves.** Confirmou a ordem geral das unidades, as posições e larguras pelo cadastro e os recuos principais.

**Graves, todas corrigidas (r21b):**

1. A clínica estava esticada (16,5 m; o real tem ~10,5 m): o contorno do cadastro junta a clínica e uma parede cinza recuada.
2. O "bloco branco" estava no lugar da loja de açaí e com aparência errada.
3. A loja de açaí estava 4,5 m a leste do lugar.
4. A loja de decoração estava com metade da largura (5,8 m; o real tem ~10 m).
5. **A Roraima Energia tinha 5,0 m de altura; o real tem ~7,4 m.** Eu tinha comparado com os carros estacionados sem descontar que o prédio fica 18 m atrás deles.
6. Sobrava um volume provisório bege na frente do prédio creme (dois contornos pequenos do cadastro fora da minha lista), e o prédio estava 6,5 m recuado demais e 29 % alto demais.
7. O portal do hotel estava com a composição trocada e 4,5 m a oeste; faltava a loja da esquina.

**Médias, corrigidas:** espetaria (baixa, com testeira vermelha); detalhes da ponta leste da Roraima Energia e a grade da frente; prédio branco do jardim (mais comprido, portão a oeste); loja de conveniência (cega para a avenida); bloco de placas claras (16 m, marquise em caixa, ordem das aberturas); centro espírita; aberturas espelhadas da loja de dois pisos; divisão das testeiras das três lojas; frente e altura do prédio cinza e laranja.

## 5. O que continua diferente do real

- **Tudo o que a versão corrigida mudou não passou por nova verificação.**
- Formas curvas viraram retas: a parede branca com vazados redondos da entrada do hotel e a coroa do prédio cinza e laranja.
- Faltam: o abrigo de ônibus em frente à ponta leste da Roraima Energia; os painéis de cobogó e a cerca elétrica do muro da casa (x 134 a 140); a caixa de medidores do muro velho; o letreiro verde em arco na face lateral do centro espírita; as faces laterais para as ruas transversais em geral (não conferidas).
- Loja de decoração: em jan/2025 a frente estava fechada com chapa; pus vitrine, sem confirmação.
- Espetaria: o interior da passagem é escuro no Street View; não sei o que há embaixo da testeira.
- Recuo das quatro lojas da ponta oeste: as placas ficam 4 a 5 m à frente do contorno do cadastro, mas podem ser marquises avançadas; mantive o cadastro.
- Sem árvores, palmeiras, mastros, mesas, carros e postes próprios desse lado; o que aparece nas fotos é do mapa de base.
- O chão em frente (calçada, estacionamentos, gramados) é o do mapa de base e da planta; não foi redesenhado.
- Datas diferentes: a consulta é de 2024/2025; o lado norte foi filmado em 11/11/2026.

**Não feito desse lado:** a oeste da Av. Terêncio Lima (x < −95) e a leste da R. Cecília Brasil (x > 337: o conjunto grande de x 368 a 466, a Secretaria de Educação em x 553 a 604 e o que há entre eles).

**Achado à parte, corrigido:** sobravam no ar, na linha do antigo muro da arena, restos do mapa do parceiro (manchas escorridas, janelas pintadas e ar-condicionado dos prédios genéricos que já tinham saído). Apareciam na frente das fachadas novas dos dois lados; o "retângulo cinza solto" da rodada 18, no lado norte, provavelmente era um deles (não conferi). O importador passou a retirá-los (166 triângulos).

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200.

## 6. Arquivos

- `02_referencias/frente_sul_20261112/`: `monta_spec_sul.py` (versão anterior `.r21a`), `aplica_r21b.py`, `fachadas/lista.json`, `fachadas_spec.json`, `consulta/` (vistas de referência).
- `03_blender/patch_gerador_sul.py`; saída `05_exportacoes/GLB/BVC_PDA_FRENTE_SUL.glb`.
- `06_unity/registra_frente_sul.py` (marco e contornos que saem dos provisórios); câmeras `r21_sul_*` no importador; `BatchListaAltos`.
- Comparações: `07_comparacoes/r21/` (`r21a/` = primeira versão), `06_unity/compara_r21.py`.
- Verificação: `09_relatorios/verificacao_r21.json`.
