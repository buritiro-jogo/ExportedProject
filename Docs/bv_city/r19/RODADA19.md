# BV City — Rodada 19 (12/11/2026)

Pedido do dono: *"Sim, continuar"* — corrigir as diferenças que sobraram da rodada 18 e reconferir as alturas das outras fachadas da frente norte da Av. Capitão Ene Garcez.

**Resposta curta: ficou mais fiel, e continua não sendo 100 %.** A conferência de alturas mostrou que **um erro grande era meu, da rodada 18**: eu tinha subido o muro e a faixa azul ao lado do Tribunal de Contas para 4,2–5,5 m; o real tem cerca de 2,6–3,5 m. Isso foi corrigido e confirmado por revisor independente. A **terceira verificação independente** achou **30 diferenças (4 graves)**: duas graves foram corrigidas, uma era do mapa de base e não das fachadas, e **uma continua aberta** (larguras do trecho do TCE). **Nada foi jogado** (sem build) e as últimas correções **só eu conferi**.

## 1. O que mudou na cena

| Peça | Antes (r18) | Agora (r19) |
|---|---|---|
| Muro branco entre o TCE e o prédio branco | 4,2 m | 2,6 m |
| Faixa azul sobre esse muro | 4,2 a 5,5 m | 2,6 a 3,5 m |
| Faixa azul com o nome do TCE | 3,40 a 4,75 m | 2,55 a 3,50 m |
| Portão de enrolar do TCE | 3,1 × 3,05 m | 3,8 × 2,45 m, com trecho de muro mais baixo e fresta a oeste |
| Prédio branco vizinho | platibanda 7,7 m, testeira 2,9–5,1 | platibanda 7,0 m, testeira 2,9–4,35, janelas de cima 4,4–5,25, quina do muro e totem 1,2 m mais a oeste |
| Corretora (esquina oeste) | muro cinza 2,7 m; janela quadriculada no plano da mureta | muro 2,3 m; testeira 2,5–3,42; as duas janelas quadriculadas na parede da agência; totem em lâmina |
| Prédio antigo do pórtico | tapume colado na calçada; pórtico no plano da ala oeste | tapume 8,7 m atrás da linha do lote; pórtico e alas do meio 6,2 m atrás da ala oeste; pórtico mais alto (7,4 m), com a caixa de vidro; um telhado por ala |
| Obra | guarda-corpos só de tubo, canto vivo, tapume 2,2 m | tela nos guarda-corpos, canto curvo, tapume 2,0 m, laje inclinada comprida pela divisa oeste |
| Casa branca 646 | portão da garagem estreito e baixo | portão com 56 % da frente e 2,47 m, pano de frestas estreito, fundo do pórtico grafite, montantes e corrimão no vidro |
| Sobrado | um telhado | três telhados escalonados |
| ESCON | letreiro sobre placa saliente | letreiro rente à parede |
| Oi | faixa vermelha a leste, lote aberto | piso estendido, portão de correr (ainda curto) |

Triângulos: frente norte 24.344 (era 23.438), TCE 3.214 (3.190), Oi 4.880 (4.356).

## 2. Conferência das alturas (10 fachadas, dois medidores)

Arquivo: `09_relatorios/alturas_r19.json`. Método: quadro bruto, referências de tamanho conhecido (porta de 2,1 m, telha de 177 mm, guia de 1 m, fiadas de tijolo, placa de trânsito), duas referências por fachada.

| Fachada | Resultado | O que fiz |
|---|---|---|
| Previdência (GRJX), muro da esquina (GVG5), sobrado (73W6), casa das grades (746F), lojas (64X6), casa atrás do tapume (WH45) | conferem (0,95 a 1,03) | nada, só o oval do muro |
| Corretora (CWG4) | 9 % alta; o muro cinza tinha 2,3 m e não 2,7 | corrigida |
| Prédio branco (38P6 e 396C) | corpo 9 % alto; **as peças que eu subi na r18 estavam ~45 % altas** | corrigido; conferi eu mesmo nos quadros `q_01760` e `q_01820` |
| Prédio antigo do pórtico (QQ3V) | alturas conferem; **profundidades erradas** | corrigido (ver abaixo) |

**De onde veio o erro da r18.** O segundo revisor escreveu que a faixa do muro era "mais alta" que a do nome. Era efeito de perspectiva (ela fica 0,2–0,3 m acima, não 0,75 m) e eu modelei como altura real, em cima de uma escala que já estava esticada. A escala certa foi fechada por quatro referências que concordam: placa de bicicleta (0,5 m de diâmetro, borda de baixo a 2,1 m), portão de enrolar, porta de vidro da entrada e andares da torre (~3 m).

**Prédio antigo do pórtico.** A medição por paralaxe deu que as alas do meio e o pórtico se deslocam 0,67 vezes o tapume, contra 0,88 da ala oeste. O contorno do cadastro (Google Open Buildings) mostra o mesmo degrau: frente em y −59,6 na ala oeste e −65,8 no meio (6,2 m atrás). Fechando as duas contas, o tapume fica a ~2,5 m da ala oeste, ou seja, **8,7 m atrás da linha onde o gerador o punha**. O quadro `q_01128` mostra a faixa larga de terra com árvores entre a ciclovia e o tapume. Profundidades **ESTIMADAS (±1,5 m)**.

Todas as alturas continuam **ESTIMADAS (±10 %)**.

## 3. Terceira verificação independente

Dois revisores, um por vez, só lendo imagens (`09_relatorios/verificacao_r19.json`): **30 achados — 4 graves, 17 médios, 9 leves.** Eles confirmaram 12 pontos como resolvidos desde a rodada 18 e **não conseguiram refutar** as alturas baixadas nesta rodada (muro 2,5–2,7 m; faixa do nome; prédio branco com diferenças de 1–4 %; pórtico recuado e mais alto).

**Graves:**

1. **Obra: a peça inclinada não é um lance de escada curto.** É uma laje inclinada comprida que corre pela divisa oeste, baixa nos fundos e alta na frente, com guarda-corpo inclinado. Conferi nos quadros `q_02466` e `q_02500` e refiz. Nenhum quadro mostra degrau: rampa ou escada longa, **em dúvida**. Comprimento e largura estimados.
2. **Corretora: uma janela quadriculada estava no alinhamento, colada na mureta.** Era a janela oeste da agência, que o gerador punha no plano errado. Corrigido: as duas janelas ficam na parede da agência, uma de cada lado da porta.
3. **Calçada estreita entre a ciclovia e as fachadas (TCE, prédio branco, ESCON).** Medi no quadro `q_01760`: da guia da pista ao muro são ~7,5 m no real e 8,0 m na cena, então **as fachadas estão no lugar**. O que está fora é a **ciclovia do mapa de base**, cerca de 2,5 m perto demais do muro (a faixa de grama entre a pista e a ciclovia tem ~1,9 m no real e 4,4 m na cena). **Não corrigido**: é da planta, não das fachadas.
4. **Comprimento do muro ao lado do TCE: continua aberto.** O trecho pilar + portão + muro mede ~11,5 m no real e ~14,7 m na cena. As larguras do TCE vieram de uma escala de vídeo que neste trecho está ~40 % esticada (a torre parece ter ~6 m de frente, não 8,5). A imagem aérea que temos em disco (0,6 m por pixel) não resolve qual ponta está fora do lugar.

**Médias (17):** 5 corrigidas (tapume da obra, portão da casa branca, totem da corretora, portão e trecho baixo do muro do TCE), 5 em parte (guarda-corpo de vidro, interior do pórtico da casa branca, casa das grades, totem do prédio branco, pórtico do prédio antigo), 7 não (ver seção 4).

## 4. O que continua diferente do real

- **TCE:** larguras do trecho leste (item 4 acima); coroamento da torre simplificado (o último andar é todo de vidro, sob cobertura de grande balanço); sem emblema.
- **Prédio branco:** falta o guarda-corpo do terraço leste, o gradil azul da rampa e as duas janelas estreitas do vizinho; totem com as cores do painel invertidas; largura ~6 % maior que a medida.
- **ESCON:** o volume azul ficou largo para a altura (1,33 contra ~1,1); azul mais violeta que o real; sem juntas, plantas e concertina.
- **Obra:** a tela é painel liso e cobre também o fundo da lateral, onde o real está sem tela; canto curvo com raio menor; sem pichação nem poste.
- **Casa branca:** vidro do terraço ainda leitoso; falta o vão aberto e a escada lateral do pórtico.
- **Sobrado:** a casa está ~1,2 m a oeste demais em relação ao muro (incerteza ±0,6 m); portões estreitos; a cobertura do térreo é laje e no real é telha ondulada clara.
- **Casa das grades:** desenho das grades mais miúdo que o real; portão de pedestre recuado; sem número, cerca e concertina.
- **Prédio antigo do pórtico:** brasão quadrado (é um disco); pórtico ~10 % estreito; pintura lisa (a real é descascada); a faixa entre a ciclovia e o tapume é piso liso do mapa (no real, bloquete, terra, grama seca, árvores e um acesso de concreto).
- **Oi:** portão de correr curto; faltam portãozinho, concertina e o painel do governo ao fundo; ainda há piso vermelho do mapa em volta da torre.
- **Corretora:** painel do totem com cores diferentes; telheiro e sobrado do fundo não conferidos.
- **Mapa de base:** ciclovia ~2,5 m perto demais das fachadas em todo o trecho.

**O que os revisores não conseguiram julgar:** valores absolutos de altura (todos dependem de uma referência suposta); topo da torre do TCE; altura do pilone da ESCON; cobogó do prédio branco (tapado pelo totem); o que árvores, contêineres e pontos de ônibus do jogo tapam.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200; colisores novos: TCE 135, Oi 31, frente norte 148.

## 5. O que continua sem fazer

1. **Nada foi jogado.** Sem build.
2. **Frente sul da avenida: não há vídeo.** Continua volume provisório.
3. Larguras do TCE e do muro até o prédio branco: precisam de imagem aérea melhor que a que temos.
4. Ciclovia do mapa de base fora do lugar (planta).
5. Postes, árvores e canteiros do vídeo (42 itens): não postos.
6. Muro pintado do governo a leste da Oi, cinco contornos fora desde a rodada 16, `IMG_4196.MOV`: como estavam.
7. Quarta verificação das correções finais (r19e).
8. Commit: nenhum.

## 6. Arquivos

- Especificação: `fachadas_spec.json` (`patch_r19.py`, `patch_r19b.py`, `patch_r19c.py`, `patch_r19e.py`; versões anteriores `_r18c`, `_r19a`, `_r19d`).
- Gerador `frente_norte_3d.py`: tapume recuado da linha do lote (`frente.recuo_do_lote_m`), peça pintada no próprio tapume (`na_frente`), um telhado por corpo (`por_corpo: 'todos'`, `sem_telhado`), abertura com plano próprio (`saliencia_m`), totem em lâmina; cópias `.r19a` e `.r19d`.
- `frente_norte_especiais.py` (obra), `tce.py`, `oi.py`: cópias `.r19a` / `.r19d`.
- Unity: câmeras `r19_*` no importador.
- Comparações: `07_comparacoes/r19/` (`antes/` = fotos da r18), `06_unity/compara_r19.py`.
- Medições e verificação: `09_relatorios/alturas_r19.json`, `09_relatorios/verificacao_r19.json`.
