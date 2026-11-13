# BV City — Rodada 15 (11/11/2026)

Setor Praça das Águas conferido trecho a trecho contra a aérea e o Street View; praça posta em escala real; três prédios novos na Praça Velia Coutinho; texturas próprias nos marcos, no chão da planta e nos volumes dos prédios.

**Nada foi jogado** (sem build). Google Maps / Street View foram só consulta: nenhuma imagem, textura ou medida deles entrou no jogo. Todo número é MEDIDO, salvo quando marcado ESTIMADO ou OBSERVADO (visto, sem medida).

## 1. Achado principal: a Praça das Águas estava 1,2 vez maior que o real

| | Real (aérea) | Cena antes (r14) | Cena agora |
|---|---|---|---|
| Avenida, de meio-fio externo a meio-fio externo | 87,8 m | 105 m | 87,5 m |
| Piscina em T, comprimento | 36,1 m | 45 m | 38,0 m |

Causa: o mapa `praca_aguas` veio do jogo web, onde o personagem tinha 2,1 m, e foi ampliado 1,2 vez de propósito (`tools/build_praca_aguas.py`, `SCALE = 1.2`; `FBImportMap.Real()`). No Unity o personagem tem 1,8 m, e a planta e o `centro_civico` do parceiro estão em 1:1. A rodada 6 concluiu que não havia descasamento de escala: **estava errada**.

Os números da tabela são da medição independente (seção 6), não meus.

## 2. O que mudou na cena (só em `Map_bv_city.unity`; `Map_praca_aguas.unity` não foi tocada)

1. **Praça reduzida a 1/1,2 em torno da origem** (`FBBVCityPlantaImporta.EscalaPA`): raízes do mapa, pontos, objetos trocados à mão, parada de ônibus; luminárias e janelas medidas; o pé e a altura dos 203 postes; cópia reduzida dos dados da vegetação (834 tufos dentro da zona, máscara e altura da grama).
2. **Pontos do jogo** (`fb_data`, mapa `bv_city`): os 49 pontos da praça divididos por 1,2; 2 pontos de rota que ficaram a oeste da zona foram trazidos para x = −96. Cópia de antes em `_backup/bvcity_r15_20261111/`. 100 de 100 pontos caem no NavMesh novo.
3. **Trecho leste devolvido.** A costura da rodada 7 ("x = 530") foi aplicada no espaço ampliado, ou seja, cortou a praça em **442 m reais**. Tudo entre 442 e 530 m (estacionamento, rua transversal, jardim) estava fora da cena desde então. Voltaram 704 peças; as 7 que já nascem desligadas no mapa original continuam desligadas.
4. **Zona mantida nova: x −100..530, y ±49,2 m** (era −180..530, ±59 no espaço ampliado). A oeste de −100 o mapa era asfalto inventado; ali entra a planta com a Av. Terêncio Lima (duas pistas e canteiro) e a Praça Velia Coutinho.

## 3. Praça Velia Coutinho: três prédios novos (marco `BVC_PDA_RESTAURANTES`, 5.800 triângulos)

Antes eram dois volumes provisórios sobre asfalto inventado.

| Peça | Planta | Composição | Alturas |
|---|---|---|---|
| Restaurante norte e restaurante sul, 2 pavimentos | contornos do OpenStreetMap conferidos na aérea (18,0 × 13,7 m cada): MEDIDO ±1 m | tijolo aparente; metade oeste fechada, metade leste com loja de vidro e terraço com guarda-corpo de madeira em X; telhado de duas águas em telha de barro, oitão aberto para leste: OBSERVADO no Street View (fev/2025, jul/2024), 4 ângulos | piso superior 3,30, beiral 6,30, cumeeira 9,10 m: ESTIMADAS |
| Passarela elevada com rampas, ligando os dois | laje do norte lida na aérea (6,5 × 10,6 m): MEDIDO ±1,5 m; a do sul e a ligação: OBSERVADAS, medidas ESTIMADAS | laje sobre pilares, guarda-corpo metálico, rampa encostada na parede de fora | traçado da rampa ESTIMADO (um lance reto; a real parece ter mais de um) |
| Prédio térreo de cinco pavilhões | contorno do OpenStreetMap (22 vértices, 459 m²) decomposto em 5 retângulos: MEDIDO ±1 m | tijolo, um telhado de quatro águas por pavilhão, o central mais alto com lanternim, entrada recuada com grade: OBSERVADO de um lado só (sul) | beirais 3,00 e 4,30 m: ESTIMADOS; lados norte e oeste repetem o padrão (ESTIMADO) |

Erro meu corrigido no caminho: li a mancha clara a oeste do restaurante norte como um anexo térreo; o Street View mostrou que é a laje elevada da passarela.

Não modelado: caixa-d'água, mesas, toldos e letreiros (nenhuma marca é reproduzida), a estrutura entre os prédios em (−166; 3), não identificada.

## 4. Texturas (todas próprias, geradas por script; nada de foto)

- `04_texturas/gera_texturas.py` gera 11 texturas ladrilháveis: tijolo vermelho e rosado e telha de barro (com mapa de relevo), madeira, treliça, cerca-viva, e quatro de detalhe neutro (reboco, concreto, placas, metal). Mais 7 reaproveitadas das texturas da própria Praça das Águas (asfalto, calçada, grama, pisos), também geradas por script.
- `03_blender/bvc_texturas.py` aplica por nome de material e cria o UV em metros por face. A textura neutra multiplica a cor que o prédio já tinha: **a cor de cada marco não mudou**, só ganhou detalhe.
- Aplicado nos 6 modelos: restaurantes (16 materiais), Assembleia (13), Palácio do Governo (10), Catedral (7), Palácio da Cultura (6), Tribunal de Justiça (5). Vidro, caixilho, letreiro e vegetação continuam em cor chapada.
- Planta: 21 dos 27 materiais com textura (asfalto, calçada, grama, terra, bloquete, placas, leito, lazer). Os 6 que ficam de fora são água, que usa o material de água do jogo.
- **Volumes provisórios dos prédios (21.192):** a parede passou de laranja chapado a reboco claro, cor única (ESTIMADA: a cor de cada fachada não foi levantada). O topo de cada um leva a **classe de cobertura lida na aérea**, prédio a prédio (`06_unity/telhados.py`, cor mediana dentro do contorno): 4.603 de telha de barro, 6.361 claras (metálica ou fibrocimento novo), 10.228 cinza (fibrocimento envelhecido, laje, ou copa de árvore por cima). Conferi uma amostra de 72 prédios sobre a aérea (`07_comparacoes/pda_r15/telhados_amostra.jpg`): a classe bate com o que se vê, salvo onde o contorno do cadastro está deslocado. Não medi a taxa de acerto no conjunto inteiro.
- **Eles continuam sendo volumes provisórios**: caixa do contorno do cadastro, topo plano, sem porta, janela nem fachada. A textura não os torna prédio final.

Onde aparece: muito na planta e nos prédios novos (tijolo, telha, grama, asfalto); **pouco nos cinco marcos do Centro Cívico**, que ganharam só granulado de reboco, concreto e metal sobre a cor que já tinham. As câmeras de perto do Tribunal de Justiça e da Assembleia caíram dentro de árvore e de parede e não serviram de comparação.

É detalhe de material, não fidelidade de fachada: nenhuma textura reproduz a fachada real de prédio algum. Da aérea entrou só a classe do telhado, não a imagem.

## 5. Verificação independente — revisão do código (um agente, só leitura)

| Achado | Gravidade | Situação |
|---|---|---|
| Os 203 postes guardam o pé e a altura em posição de mundo e não tinham sido reduzidos (pé gravado a até 106 m do real) | alta | **corrigido** |
| 88 colisores de tronco e de poste que a rodada 7 desligou não tinham voltado; 12 árvores sem tronco sólido | média | **corrigido** |
| Vegetação filtrada por célula: 35 % dos tufos de dentro da zona sumiam e 8 ficavam fora | baixa | **corrigido** (filtro por tufo: 834 de 834) |
| Tapume recortado virava malha vazia com colisor ligado | baixa | **corrigido** (o objeto sai) |
| 4 luminárias de 5,6 m caem abaixo do limite de 6 m que separa poste de rua de luminária fraca | baixa, provável | **não corrigido** (é regra do jogo, fora do cenário) |
| Malha recortada perde os níveis de detalhe (já vinha da rodada 11) | baixa | **não corrigido**; custo em FPS não medido |
| Parada de ônibus e food trucks postos à mão também encolheram; o tamanho certo não foi conferido | dúvida | **aberto** |

Conferido sem defeito: sinais e ordem das operações, critério de separação dos pontos, nenhum outro dado do `bv_city` em coordenada antiga, nenhuma luz, som ou partícula presa à escala.

## 6. Verificação independente — medição aérea × cena (outro agente)

Desvio = cena − real. Tabela completa, scripts e figuras em `09_relatorios/verificacao_r15/`.

| Elemento | Em x | Em y |
|---|---|---|
| Piscina em T | −2,8 / −1,9 / 0,0 m | −2,7 a −6,1 m |
| Rosa dos Ventos (centro) | −0,5 m | −3,8 m |
| Piscina em zigue-zague | +0,25 / −1,5 m | −1,25 / −2,25 m |
| Espelho d'água do Portal | −0,5 / +1,8 m | −0,8 / −3,3 m |
| Portal do Milênio (laje) | +4,8 m | não medido com confiança |
| Pavilhão oeste (cobertura) | +6,6 / +3,9 m | −0,9 / −1,5 m |
| Pavilhão leste (cobertura) | +3,1 / +2,0 m | −0,5 / −0,7 m |
| Rua transversal leste | +10,7 / +5,3 m; 5,4 m mais estreita | — |
| Meios-fios da avenida em x = 400 | — | todos a menos de 1 m |

O que a medição mostrou além da escala:

1. **A avenida real é inclinada 0,47° em relação ao eixo x; a praça do mapa é paralela a x.** Por isso o leste confere e o oeste fica 2,5 a 4,5 m ao norte do real.
2. **De x = 145 para leste o mapa fica 2 a 11 m a leste do real.** Não é escala: é o desenho do mapa antigo.
3. **Canteiro central em x = 100: 52,0 m no real, 39,5 m no mapa.** As pistas do mapa têm 24 m cada; as reais, 13 a 18 m. O estacionamento real é em baias recortadas no canteiro; no mapa é pintado no asfalto.
4. **Lote x 350..470: no real é terra batida com árvores; no mapa é estacionamento, gramado e piso, com uma rua em x 331..345 que não existe.**
5. Pavilhões: no real são duas coberturas por pavilhão; no mapa, uma só. Jardim leste: os caminhos não são os reais.

Ressalva da própria medição: a aérea e a planta divergem 1,2 a 3,5 m em y nesta zona, e não foi possível dizer qual está certa.

## 7. Relatório do setor

```
SETOR: Praça das Águas (zona mantida x −100..530, y ±49,2 m; 6,2 ha) e Praça Velia Coutinho
Planta geográfica: georreferenciada; escala conferida (avenida 87,8 m real × 87,5 m na cena)
Ruas catalogadas: 35 vias do OpenStreetMap, 13 com nome (inventário da rodada 11, feito na caixa antiga, maior)
Lotes identificados: 0 (lotes não levantados)
Edificações modeladas: 2 pavilhões e o Portal do Milênio (do mapa pronto) + 3 prédios novos na Praça Velia Coutinho
Edificações verificadas: 6 de 6 contra a aérea (posição); 5 de 6 contra o Street View (composição; o Portal não foi visto nesta rodada)
Fachadas pendentes: todas as da frente norte e sul da avenida (cerca de 45 contornos do cadastro, hoje volume provisório)
Comparações: 4 trechos aérea × antes × depois, 1 da Praça Velia Coutinho, 11 de textura antes × depois, 1 amostra da classe de telhado
Problemas: muro da arena em y ±49 (não existe no real e fecha as ruas transversais); praça paralela a x (real inclinada 0,47°);
           mapa 2 a 11 m a leste do real de x = 145 em diante; canteiro central 12,5 m mais estreito; lote x 350..470 e rua
           x 331..345 inventados; pavilhões com cobertura única
Correções: escala real; trecho leste devolvido; zona nova; 3 prédios; postes, colisores e vegetação; texturas
Próximas tarefas: tirar o muro e abrir as ruas; refazer o trecho x 326..530 pelo real; corrigir a inclinação e o canteiro
```

## 8. O que continua sem fazer ou sem verificar

1. **Nada foi jogado.** Sem build; a carga dos setores em jogo continua sem teste.
2. **O muro de perímetro da arena continua de pé** em y ±49 m. No real não há muro, há fachadas, e as ruas transversais passam.
3. Os problemas de desenho da seção 6 (inclinação, deslocamento a leste, canteiro, lote, pavilhões) **não foram corrigidos**: exigem refazer partes do mapa da praça.
4. Alturas de todos os prédios novos e de todos os marcos: ESTIMADAS.
5. NavMesh só nas duas zonas; a Praça Velia Coutinho está fora dele.
6. As texturas não foram vistas em jogo nem à noite; o relevo do tijolo e da telha só foi visto no render e na foto do Editor.
7. Commit: nenhum.

## 9. Arquivos

- Cena e importador: `Editor/FBBVCityPlantaImporta.cs` (cópia de antes em `_backup/bvcity_r15_20261111/`), `Scenes/Map_bv_city.unity`, `planta/recortes/`, `planta/tex/`, `marcos/BVC_PDA_RESTAURANTES.glb`.
- Planta: `06_unity/ancora.py`, `03_planta/confere_relevo.py`, `06_unity/exporta_unity.py`, `06_unity/modelados.geojson`, `06_unity/telhados.py` (+ `telhados_cache.json`), `06_unity/materiais_tex.py`.
- Modelos e texturas: `03_blender/restaurantes.py`, `03_blender/bvc_texturas.py`, `04_texturas/gera_texturas.py`; os 5 geradores antigos chamam o módulo de textura (cópias `.r14t`).
- Dados do jogo: `06_unity/fbdata_r15.py`.
- Comparações: `06_unity/pda_faixas.py`, `compara_r15.py`, `compara_tex_r15.py` → `07_comparacoes/pda_r15/`.
- Verificação: `09_relatorios/verificacao_r15/`.
