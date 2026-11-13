# BV City — rodada 10 (11/11/2026): levantamento completo e planta r10

Continuação da rodada 9 (escopo e planta do OSM). Nada desta rodada entrou no Unity: é levantamento, georreferenciamento e planta, na ordem pedida. **Nenhum prédio novo foi modelado e nenhum setor está concluído.**

Cópia de segurança de antes da rodada: `F:\FreeBala_backups\bvcity_20261111_pre_reconstrucao\` (pasta `tools/bv_city` inteira, cena `Map_bv_city.unity`, `BURITIRO_Mapas/bv_city`, `FBMontaBVCity.cs`, `fb_data.json`, `Docs/bv_city`). A planta anterior (só OSM, plana) também ficou em `03_planta/_v1_osm_plano/gera_planta.py`.

## 1. Perímetro

A imagem do Google Maps citada no pedido **não chegou anexada** nesta rodada. Foi usada a captura de 10/11 (`02_referencias/escopo/escopo_google_maps.png`), georreferenciada na rodada 9 por 18 pontos de controle (RMS 6,3 m):

- NO 2.839758 / −60.696644 · NE 2.839758 / −60.647561 · SE 2.806132 / −60.647561 · SO 2.806132 / −60.696644
- 5.457 × 3.718 m = **20,29 km²**, 160 setores de 400 m (`BVC_S_X<i>_Y<j>`).

Ficam **fora** desse retângulo, embora citados: Dos Estados (63 m ao N), Parque do Mirandinha (96 m), Paraviana (534 m), Roraima Garden Shopping (1.614 m), quase todo o Aeroporto (só 2,8 ha dentro) e quase todo o Caçari (3,2 %). O perímetro não foi ampliado por conta própria: a regra do pedido é "conforme os limites identificados na imagem". O pipeline é paramétrico; ampliar é trocar o retângulo e rodar de novo.

## 2. Achado principal: a aérea está deslocada ~8 m, o OSM não

A rodada 9 tinha concluído que os prédios do OSM estavam ~9 m fora do telhado. Estava lido ao contrário. Quatro medidas independentes:

| Referência | Como | Resultado (referência − eixo do OSM) |
|---|---|---|
| Endereços do CNEFE/IBGE 2022 (13.784 pontos, GPS na porta) | meio entre as fileiras dos dois lados de cada rua, 275 vias | dE +0,1 m, dN +1,0 m; resíduo mediano 0,96 m |
| Trilhas de GPS públicas do OSM (5 trilhas, 6.442 pontos) | desvio lateral ao eixo, mínimos quadrados | por trilha: 0,3 a 2,9 m; todas juntas dE +0,7 / dN +0,4 m |
| Contornos do Google Open Buildings (sem usar imagem) | meio do vão entre as frentes dos dois lados, 495 vias | dE −1,1 m, dN −0,6 m |
| Aérea Bing z18 | registro de 8.131 contornos + perfil de asfalto das vias, 84 setores | **dE −7,5 m; dN +0,2 m a oeste e +3,7 m a leste/nordeste** (módulo 6,0 a 9,8 m, mediana 7,9 m) |

Logo a malha do OSM e os contornos do Google concordam entre si e com o GPS dentro de ~1 m; o mosaico Bing é que aparece ~8 m a oeste. Prova visual: `01_georef/gps/prova_desloc_oeste.png` e `prova_desloc_centro.png` (mesmo recorte sem e com a correção).

O que foi feito com isso:

- `03_planta/validacao/desloc_aerea.py` mede o campo de deslocamento setor a setor (`desloc_aerea.json`) e `aerea.py` passa a amostrar a imagem com ele. A imagem não é alterada e continua só como consulta.
- Com a correção, o conjunto de contornos cai sobre os telhados com resíduo **mediano de 0,16 m (p90 0,63 m)** em 78 setores; a fração de prédios nítidos a até 2 m do lugar passa de 0 % para 79 %.
- **Nenhum contorno é movido.** (A primeira versão do cadastro movia o OSM para a aérea; foi descartada antes de gravar.)

**Consequência para o que já estava feito** (não corrigido nesta rodada; fica para a Fase 4):

- `BVC_CCV_CATEDRAL`: na rodada 4 o contorno do OSM foi deslocado 6,5 m para oeste para casar com a aérea. O deslocamento deve ser desfeito (campo no setor: dE −7,1 / dN +0,5 m).
- `BVC_CCV_CULTURA`: planta traçada direto na aérea; está ~6 m a oeste (campo no setor: dE −6,0 / dN +0,3 m).
- Qualquer medida de posição absoluta tirada da aérea nas rodadas 4 e 5 carrega esse erro. Dimensões (largura, comprimento) não são afetadas.

Limites desta conclusão (os quatro primeiros vieram da verificação independente, seção 7-A):

- **O deslocamento da aérea não é uma translação única.** dE é uniforme (−7,6 m, desvio-padrão 0,37 m em 137 janelas), mas dN tem um **degrau**: ~+0,2 m a oeste e ~+3,7 m a leste/nordeste, numa divisa diagonal (emenda de duas tomadas do mosaico). O campo por setor acompanha o degrau: contra a medida independente difere 0,37 m na mediana e 1,0 m no p90; 5 de 137 janelas passam de 2 m, na divisa.
- **O valor de dE depende do método:** −7,5 m contra os contornos (pico nítido) e −9,6 m contra as pistas pela máscara de cinza (pico fraco). O "~8 m" cabe; o décimo não.
- **"Erro de ~1 m" vale para a MÉDIA da malha.** Rua a rua, o meio das fileiras de endereço se espalha com desvio-padrão de 5,3 m (mediana do módulo 1,7 m), parte ruído dos endereços, parte erro real de eixo. Há variação regional (metade sul dN +2,4 m). Não dá para prometer 1 m numa via individual.
- **A evidência do GPS, sozinha, é fraca:** 2 usuários identificados, 330 pontos casados pelo critério do verificador. Quem sustenta a conclusão são os endereços e os contornos do Google.
- O CNEFE veio pelo Overture/OpenAddresses, não direto do IBGE. Em 76 dos 160 setores o campo vem dos vizinhos ou da mediana (rio, mata, borda).

## 3. Levantamento novo (dados abertos)

| Dado | Fonte e licença | Quantidade no perímetro | Arquivo |
|---|---|---|---|
| Contornos de edificação | Overture 2026-10-21.0: OSM (ODbL), Google Open Buildings v3 (CC BY 4.0), Microsoft ML Buildings (ODbL) | **21.586** (OSM 1.182, Google 20.143, Microsoft 261) | `01_georef/predios/predios_bvcity.geojson` e `.csv` |
| Endereços | Overture addresses ← OpenAddresses br/rr ← CNEFE/IBGE | 14.324 pontos; 7.591 contornos com endereço | `overture_addresses.jsonl` |
| Estabelecimentos | Overture places (CDLA-Permissive 2.0) | 4.175 pontos; 1.491 contornos com nome de lugar. **Posição pouco confiável** (o ponto "Praça das Águas" cai a ~700 m da praça): serve de pista, não de identificação | `overture_places.jsonl` |
| Relevo | Copernicus DEM GLO-30 (ESA/Airbus), tile N02 W061 | recorte 133 × 189 px de ~30 m | `01_georef/relevo/` |
| Trilhas de GPS | API pública do OSM | 7.336 pontos, 6 trilhas | `01_georef/gps/` |

Cada contorno tem identificador `BVC_E_X<i>_Y<j>_<nnnn>`, id da fonte, confiança da fonte, área, retângulo mínimo, via de frente, endereços e lugares próximos, e o resultado da conferência na aérea.

**O que esse cadastro é e não é:**

- É contorno de telhado tirado por modelo de visão sobre imagem de satélite (Google/Microsoft) ou desenhado à mão (OSM). É **provisório**.
- Conferência um a um na aérea corrigida: **2.083 conferem** (pico nítido a ≤ 2 m), **1.345 divergem** (> 2 m), **18.158 sem decisão automática** (telhado sem contraste, árvore, sombra, ou contorno pequeno demais). Ou seja, só 9,6 % dos contornos foram confirmados individualmente.
- A cobertura **não é completa**: nas folhas aparecem telhados sem contorno, e no centro comercial os contornos do Google partem um telhado em vários retângulos. A taxa de falta não foi medida.
- Altura: 3 contornos têm altura da fonte e 287 têm número de andares. Os outros 21.296 usam um padrão ESTIMADO.
- Fachada, cobertura, cor, material, muro, portão: nada levantado.
- **Lotes: 0.** Não achei fonte aberta de lotes para Boa Vista.
- 41 contornos de modelo (36 sobre a pista, 5 sobre a água) ficam no cadastro mas fora do 3D. Prédio do OSM nessa situação fica: é cobertura sobre a via ou construção da orla sobre o rio.
- O Overture marca a licença dos endereços como "LicenseRef-Proprietary" (repasse do OpenAddresses). Os endereços entram só como atributo de apoio; conferir os termos do IBGE antes de qualquer uso além disso.

## 4. Larguras das vias

O `mede_vias.py` da rodada 9 foi descartado (aceitava trechos com desvio mediano de 5,7 m, justamente por causa da aérea deslocada). O novo `perfil_vias.py` mede o perfil transversal médio de asfalto de cada via na aérea corrigida:

- 1.403 vias candidatas, **511 aceitas** (172,3 de 317,1 km). Desvio do eixo |mediana| 1,95 m, p90 4,2 m.
- Conferência visual em 12 vias tiradas sem escolher (`validacao/_dev/perfil_prova.png`): a maioria plausível, mas há erros grosseiros (rotatória lida como 26,8 m; pista dupla lida como uma de 16,7 m) e tendência a medir de menos sob copa de árvore.
- Por isso a medida individual só entra com critério estrito (|desvio| ≤ 1,5 m, ≥ 20 estações, pico ≥ 0,7, entre 0,6 e 1,6 × a mediana da classe). As demais vias da classe usam a mediana das estritas.

| Classe | Padrão antigo | Mediana medida | Vias estritas |
|---|---|---|---|
| primary, mão única | 7,0 m | **9,4 m** | 36 |
| secondary, mão única | 7,0 m | 6,5 m | 43 |
| tertiary, mão única | 6,5 m | 6,9 m | 21 |
| tertiary, mão dupla | 9,0 m | **6,1 m** | 14 |
| residential, mão dupla | 8,0 m | **5,9 m** | 33 |

Por comprimento de pista dentro do perímetro (257,6 km): **MEDIDO_AEREA 53,8 km, CALIBRADO_AEREA 159,2 km, ESTIMADO 44,8 km** (antes: 85 % estimado).

Ressalvas: o que se mede é a faixa de **asfalto**; a sarjeta de concreto não entra, então meio-fio a meio-fio pode ser ~0,5 a 1 m maior. Precisão de ±1 m por borda (imagem de 0,6 m/px). Largura de calçada continua ESTIMADA por classe.

## 5. Relevo

A planta antiga era plana, com o rio a −3 m por estimativa. O Copernicus DEM mostra o platô da cidade a ~80–85 m e o Rio Branco a 60,5 m.

- Rio Branco na planta: **−21,9 m** (era −3,0).
- Entram o barranco da orla, um **terraço baixo de −5 a −6 m** entre o centro e a Orla Taumanan e o vale do igarapé ao sul do corredor.
- O platô fica **plano por decisão**: ali o dado lê telhado contra praça aberta (±3 a 5 m), não desnível de chão. Nada sobe acima do platô. Áreas abertas grandes ainda podem aparecer 1 a 3 m abaixo dele por esse viés.
- Dentro do corredor já feito (Praça das Águas + Centro Cívico) h = 0 por decisão, com transição de 150 m; o dado lê ali de −4,3 a +6,2 m. A rever na Fase 4.
- Incerteza: ±3 m na vertical, ~30 m na horizontal. Não há dado aberto melhor.
- Pontes de via e passarelas: tabuleiro com altura linear entre as cabeceiras. Nenhum chão fica abaixo da água (piso em nível do rio + 0,3 m).
- **Período de referência do rio, registrado como mistura:** contorno de água e de areia = OSM, de estiagem; nível = DSM de 2011–2015; a aérea de conferência mostra o rio mais cheio. A margem da planta não deve ser conferida contra a aérea.

Dois erros meus corrigidos no meio da rodada:

1. O primeiro filtro (percentil 25 em janela de 150 m) empurrava o barranco ~50 m para dentro da cidade e inventava rampas de 15 a 19 % em ruas planas do centro histórico. Trocado por mediana de 90 m.
2. A **Orla Taumanan é laje elevada**; o DSM põe o chão dela na cota do barranco (ver `conferencia/conferencia_relevo.json`). Isso **não foi resolvido**: a orla precisa de tratamento próprio na Fase 4 (plataforma na cota da rua).

## 6. Planta r10 (regerada)

- 160 setores, **671.759 triângulos** (era 376.369), maior setor 13.642.
- 21.545 volumes PROVISÓRIOS de edificação (eram 1.182), cada um com `id_bvc`, fonte e conferência nas propriedades do GLB.
- GLBs em `05_exportacoes/planta/` (80 MB).
- Conferência obrigatória (`confere_planta.py`): marcos a 0,01–0,06 m do referencial; 746.543 triângulos lidos; 1.247 lascas horizontais viradas para baixo (float32, como antes).
- Conferência do relevo (`confere_relevo.py`): ver seção 7-A.

## 7. Relatório por setor

`09_relatorios/RELATORIO_SETORES.md` e `setores_relatorio.json`, com indicadores separados de levantamento, construção e validação para os 160 setores. Totais:

- Posição: **98 setores conferidos**, 17 parciais, 45 sem medida (rio, mata, borda).
- Vias: 2.015 trechos por setor, 257,6 km.
- Contornos: 21.586 (2.083 conferidos, 1.345 divergem, 18.158 sem decisão).
- Lotes: 0. Modelos individuais: os do Centro Cívico das rodadas 1–5. Fachadas levantadas fora deles: 0.

Folhas aérea × planta × sobreposição: `07_comparacoes/planta/<setor>.png` (uma por setor) e `geral_aerea_x_planta.jpg`, `geral_relevo.jpg`.

## 7-A. Verificação independente (2 agentes, um por vez)

Pedi a dois verificadores, com código próprio, para tentar refutar as conclusões. Arquivos em `09_relatorios/verificacao_r10/`. Os dois deram **PARCIAL**.

**Georreferência — confirmado no principal:**

- Endereços × OSM refeito casando pelo nome da rua: dE +0,57 ± 0,44 m, dN +1,09 ± 0,51 m (237 trechos, 10.018 pontos).
- Os endereços são mesmo pontos de campo dos dois lados da rua (desvio lateral bimodal, 4,4 m de ruído por fileira, paridade do número × lado 0,92, nenhuma coordenada repetida), não interpolados no eixo.
- Contornos do Google × OSM: dE −0,74 ± 0,15 m. Aérea × contornos em 137 janelas: dE −7,58 m.
- Ressalvas: as quatro primeiras da seção 2.

**Planta — o que confirmou:**

- Relevo sem frestas: 1.199.436 amostras em 199.906 triângulos de chão, resíduo 0,0000 m em vias, calçadas, canteiros e passeios; 43.983 arestas compartilhadas com diferença só de 0,00 ou 0,15 m.
- Cobertura do perímetro: sobram 23,4 m² em 1.511 lascas (a maior de 0,75 m²).
- Larguras por fonte: 44,74 / 53,78 / 159,20 km, igual ao relatório. Contando vias de lote e passeios (todos estimados), o medido ou calibrado é 68,3 % e não 82,6 %.
- 21.545 volumes, todos com identificador distinto.

**Planta — o que derrubou, e o que fiz:**

| Defeito achado | Situação |
|---|---|
| Corredor não estava exatamente em h = 0: 214 vértices até 0,18 m abaixo (borda sudoeste) | corrigido (retângulo alargado de um passo da malha) |
| 3 passarelas mergulhavam até 1,36 m abaixo da água | corrigido (regra do tabuleiro passou a valer para passeios) |
| Quadra, verde e areia até 1,2 m abaixo do nível do rio em 23 triângulos | corrigido (piso em nível do rio + 0,3 m) |
| 3 prédios do OSM marcados "fora do 3D" estavam na malha | a marca é que estava errada: são 2 coberturas sobre a via e 1 construção da orla; desmarcados |
| 158 prédios com a base até 0,19 m acima do chão entre vértices | corrigido (base 0,3 m abaixo do menor terreno) |
| Até 13 m de parede enterrada em prédio no barranco (159 acima de 2 m) | **não corrigido**: volume provisório de base plana em terreno inclinado |
| 35 coberturas (`building=roof`) suspensas com `base_m` = 0 nas propriedades | **não corrigido**: é laje sem parede, de propósito; a propriedade está enganosa |
| Talude e água dos igarapés não conferidos contra regra própria | **não verificado** |

Conferência refeita por mim depois das correções (`03_planta/confere_relevo.py` → `conferencia/conferencia_relevo.json`). Ela repete os testes do verificador, mas **não é uma segunda verificação independente**:

- Corredor: 16.668 vértices de chão, 0 fora da cota.
- Chão abaixo do rio: nenhum (rio a −21,91 m; menor chão a −21,6 m).
- 41 objetos com ponte, nenhum abaixo da água.
- 21.545 volumes nas malhas = 21.586 do cadastro − 41 fora do 3D.
- Rampas ao longo dos eixos: 6 de 1.412 vias passam de 12 % (máximo 15 %) e 79 passam de 8 %. Entre elas a Av. Santos Dumont e a Rua Amajari (13 %). **Não conferi se essas rampas existem**; podem ser resto do viés do dado.
- Terreno na Orla Taumanan: rua de trás a −5,5 m, anfiteatro a −8,8 m, plataformas a −12 e −16 m, mirante a −19 m. É o problema da laje elevada, em aberto.

## 8. O que NÃO foi feito

- Nada no Unity (a planta não foi importada; não há streaming por setor ainda).
- Nenhuma modelagem, textura, vegetação, poste, fiação, sinalização.
- Nenhuma consulta ao Street View nesta rodada.
- Árvores: continuam as 161 do OSM + fileiras estimadas. Postes: nenhum dado.
- Catedral e Palácio da Cultura não foram reposicionados.
- Perímetro não ampliado para os bairros citados que ficam fora.

## 9. Próximos passos, na ordem do pedido

1. Decidir o perímetro (manter a captura ou ampliar).
2. Fechar a planta: lotes (procurar cadastro municipal; sem ele, registrar como lacuna definitiva), revisar os 1.345 contornos que divergem, medir a taxa de telhados sem contorno numa amostra.
3. Levar a planta para o Unity com carregamento por setor.
4. Fase 4: Praça das Águas e Centro Cívico, começando por desfazer o deslocamento da Catedral e do Palácio da Cultura.

## Como rodar

| Passo | Comando |
|---|---|
| Contornos, lugares, endereços | `C:/Python314/python 01_georef/predios/baixa_overture.py` |
| Relevo | `C:/Python314/python 01_georef/relevo/relevo_le.py` |
| Deslocamento da aérea | `C:/Python314/python 03_planta/validacao/desloc_aerea.py` (precisa de `perfil_vias_bruto.json`) |
| Larguras | `C:/Python314/python 03_planta/validacao/perfil_vias.py` |
| Cadastro de edificações | `C:/Python314/python 01_georef/predios/predios_consolida.py` |
| Planta | `C:/Python314/python 03_planta/gera_planta.py tudo` |
| GLBs | `blender -b --factory-startup --python 03_planta/blender_planta.py -- tudo` |
| Conferência | `C:/Python314/python 03_planta/confere_planta.py` |
| Relatório e folhas | `C:/Python314/python 03_planta/relatorio_setores.py` e `03_planta/validacao/folhas.py`, `folha_geral.py` |
| Conferências de GPS | `01_georef/gps/gps_confere.py`, `enderecos_confere.py`, `frente_confere.py` |
