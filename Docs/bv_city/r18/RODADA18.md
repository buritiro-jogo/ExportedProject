# BV City — Rodada 18 (12/11/2026)

Pedido do dono: *"Sim, continuar"* — corrigir as diferenças que a verificação da rodada 17 deixou abertas na frente norte da Av. Capitão Ene Garcez.

**Resposta curta: melhorou bastante, mas ainda não está 100 % fiel.** As peças que estavam mais erradas (obra, totem e marquise da ESCON, casa das grades, lateral do TCE) foram refeitas com geometria própria. Uma **segunda verificação independente** achou **49 diferenças (2 graves)**; as duas graves e 11 das 20 médias foram corrigidas na mesma rodada (outras 4 só em parte, 5 não), e a lista do que continua diferente está na seção 4. **Nada foi jogado** (sem build) e as últimas correções **não passaram por terceira verificação**.

## 1. O que mudou na cena

| Peça | Antes (r17) | Agora (r18) |
|---|---|---|
| Obra ao lado da casa branca | volumes sólidos rosa com janelas pretas | esqueleto de concreto de dois pisos, laje de borda curva sobre a esquina, guarda-corpos de obra, escoras, tapume de chapa com a placa de obra; segue funda pela R. Pedrinho Filho |
| ESCON | totem em prisma reto, marquise em caixa, alturas infladas | pilone em lâmina com borda curva e furo vazado, marquise em meia-lua com forro branco, portão de barras em diagonal, aro hexagonal liso no lugar do emblema |
| Casa térrea das grades | janelas escuras quadriculadas | grades brancas em espinha de peixe, portões azul-claros, fundo de varanda |
| Sobrado | muro de reboco laranja, telhado em asa | muro e pilares de tijolo aparente claro, telha escura, tijolo de vidro, parede creme |
| Lojas | "2X1" pequeno, painel liso | cartaz de comida desenhado, "2X1" grande com o X vermelho, tela fina na vitrine, tijolo claro |
| Prédio branco vizinho do TCE | três caixas de alturas diferentes | bloco de uma altura só, faixa azul mais alta, cobogó, disco liso no totem, anexo leste mais baixo e recuado |
| Tribunal de Contas | "escada" em lajes maciças, painel de pastilha na torre | passarela metálica de um nível com mãos-francesas, fitas de vidro até a quina, faixa azul do muro mais alta que a do nome, portão no lugar medido |
| Oi | pátio vermelho do mapa de base | placa de cimento escuro sobre o lote, portão azul, fios de cerca, pilar na ponta do gradil |
| Restaurante norte | fundo do terraço de cima lendo como tijolo iluminado | fundo na sombra |

Triângulos: frente norte 23.438 (era 18.328), TCE 3.190 (2.998), Oi 4.356 (4.112), restaurantes 5.936 (5.924).

## 2. Achado da rodada: alturas infladas em três fachadas

Conferindo o quadro bruto contra peças de tamanho conhecido (porta de 2,1 m do prédio branco, guarda-corpo de vidro de cerca de 1 m, largura do carro atrás do portão) e contra o cadastro, as **alturas da especificação estavam grandes demais** em três fachadas, cada uma levantada com uma escala diferente:

| Fachada | Fator aplicado | Exemplo |
|---|---|---|
| ESCON (duas fachadas) | × 0,72 | volume azul 6,8 → 4,7 m; pilone 8,8 → 6,8 m |
| Casa branca 646 | × 0,80 | portão 2,9 → 2,3 m; pórtico 5,9 → 4,7 m |
| Obra | refeita | tapume 2,5 → 2,2 m; cobertura 8,6 → 7,0 m |

Todos esses valores continuam **ESTIMADOS (±15 %)**. O revisor conferiu depois as proporções entre peças vizinhas (obra × tapume × pórtico; volume azul × prédio branco) e elas batem com o vídeo. O muro da ESCON tinha ficado baixo demais com o fator e voltou a subir (2,7 m). **As outras 11 fachadas não foram reconferidas com este método.**

A largura da obra também estava inflada: a especificação dava o tapume até 12 m além do que cabe antes do eixo da R. Pedrinho Filho (OpenStreetMap). O tapume ficou com 11,4 m, de x = −1,6 a 9,8.

## 3. Segunda verificação independente

Dois revisores, um por vez, só lendo imagens (`09_relatorios/verificacao_r18.json`): **49 achados — 2 graves, 20 médios, 27 leves.** Eles confirmaram como corrigidos 19 pontos da rodada 17.

**As duas graves, corrigidas (r18b):**

1. **A "escada de incêndio" do TCE não é escada.** A primeira verificação descreveu uma escada em zigue-zague e eu a modelei assim. O segundo revisor e os quadros `q_01790`/`q_01760` mostram uma **passarela metálica azul de um nível só**, com guarda-corpo de três barras e mãos-francesas. Trocada.
2. **A obra parava na esquina.** No real ela segue funda pela rua lateral (`q_02320`). Estendida até o fundo do lote do cadastro (17 m), com pilares, cobertura recuada, guarda-corpo nos dois níveis e tapume em toda a lateral.

**Médias corrigidas (r18b e r18c):** grades da casa térrea em módulos estreitos com fundo escuro; cor do sobrado; tijolo do muro do sobrado e das lojas mais claro, sem pilar no meio do pano; tela fina na vitrine; fitas de vidro da torre do TCE; faixa azul do muro do TCE numa altura só até o prédio vizinho; totem do vizinho mais baixo; anexo leste do vizinho mais baixo; furo do pilone maior e mais perto do topo; muro e portão da ESCON mais altos; piso da Oi escurecido e estendido; cor dos guarda-corpos da obra; fresta entre as duas metades do prédio branco fechada.

## 4. O que continua diferente do real

- **Obra:** os guarda-corpos são só tubos, sem a tela laranja-rosada que cobre os panos; falta o lance de escada inclinado do lado oeste; o canto do tapume é vivo (no real é curvo) e não há a pichação nem o poste da esquina.
- **Casa branca:** o portão da garagem ocupa cerca de 46 % da frente (no real, 57 %); o pórtico tem parede lateral e laje grossas demais; o guarda-corpo de vidro lê como faixa leitosa, sem montantes.
- **Sobrado:** um telhado só, no lugar de três escalonados; os volumes (corpo central avançado e mais alto) estão achatados.
- **Casa térrea:** telhado lê como laje plana; sem cerca elétrica, concertina e número.
- **Lojas:** cartaz mais esquemático que o real; um retângulo cinza-claro solto no alto da vitrine, de origem não identificada; falta o volume de tijolo do vizinho entre a casa térrea e as lojas.
- **ESCON:** o letreiro ainda aparece sobre uma placa de tom diferente da parede; azul mais violeta que o cobalto do real; sem juntas na testeira, sem plantas na jardineira, sem concertina. Uma placa de proibido estacionar do mapa de base fica na frente do nome.
- **TCE:** coroamento da torre simplificado; trecho branco entre o vidro da entrada e o portão conferido só por mim; sem emblema e sem a placa amarela do banco.
- **Oi:** sobra uma faixa vermelha a leste, fora do contorno do cadastro; a leste do pilar o lote fica aberto (no real há portão de correr); cores das janelas e da base da torre.
- **Restaurante:** térreo todo de vidro (a referência mostra um trecho de parede azul-acinzentada); canteiro em arco onde a referência indica frente reta.
- **Brancos:** ainda saem cinza-bege nas fotos do Unity. A luz e a exposição da cena não foram mexidas.
- **Câmeras:** contêineres do jogo na pista tapam parte das fotos; duas câmeras foram movidas para a calçada do canteiro.

**O que os revisores não conseguiram julgar:** o térreo da obra (atrás do tapume), o topo da torre do TCE (sai do quadro), o lado oeste da Oi, e o que árvores, contêineres e postes do jogo tapam.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200; colisores novos: TCE 133, Oi 31, frente norte 144.

## 5. O que continua sem fazer

1. **Nada foi jogado.** Sem build.
2. **Frente sul da avenida: não há vídeo.** Continua volume provisório.
3. Muro pintado do governo a leste da Oi (x 570 a 585), cinco contornos da frente norte fora desde a rodada 16, `IMG_4196.MOV`: como estavam.
4. Postes, árvores e canteiros do vídeo (42 itens): não postos.
5. Reconferir as alturas das outras 11 fachadas pelo método da seção 2.
6. Terceira verificação independente das correções r18b/r18c.
7. Commit: nenhum.

## 6. Arquivos

- Peças próprias: `03_blender/frente_norte_especiais.py` (obra, pilone, marquise, emblema), executado por `frente_norte_3d.py`.
- Gerador: tipos novos de abertura (`grade_diagonal`, `cobogo`), muro de tijolo, tapume ondulado, portão em diagonal, quadro com imagem, disco no totem.
- Especificação: `fachadas_spec.json` (`patch_spec_r18.py`, `patch_spec_r18b.py`; versões anteriores `_r17b` e `_r18a`).
- Texturas desenhadas: `04_texturas/gera_r18.py` (`tapume_v`, `tapume_h`, `cartaz_comida`, `placa_obra`).
- `tce.py`, `oi.py`, `restaurantes.py` (cópias `.r17b` e `.r18a`).
- Unity: câmeras `r18_*` no importador.
- Comparações: `07_comparacoes/r18/` (`antes/` = fotos da r17; `r18a/` = antes das correções da segunda verificação), `06_unity/compara_r18.py`.
- Verificação: `09_relatorios/verificacao_r18.json`.
