# BV City — Rodada 28 (12/11/2026)

Pedido do dono: *"ao concluir já prossiga para todo o restante de casas, ruas etc de todo o mapa"* — segundo passo: fachadas do lado norte da avenida, a oeste da praça.

**Resposta curta: a frente norte da Av. Capitão Ene Garcez está modelada de x −143 a −1216 em 18 conjuntos (33.328 triângulos; 19 contornos do cadastro deixaram de ser volume provisório).** Um revisor independente conferiu a primeira versão com 25 vistas próprias do Street View e achou 20 diferenças (5 graves, 12 médias, 3 leves); tratei todas, e **a versão refeita só eu conferi**. Não é 100 % fiel. **Nada foi jogado** (sem build) e nada foi commitado.

## 1. O que entrou

Sem vídeo próprio. Posição e largura: aresta de frente do contorno do cadastro (o revisor conferiu: bate em x; os contornos do OSM batem em y dentro de 1 a 2 m; **as frentes dos contornos automáticos entre x −385 e −160 estavam 4 a 5,6 m perto demais da rua** e foram corrigidas pela triangulação dele). Alturas: medidas pelo revisor na perspectiva, com a posição da câmera lida em cada vista (±0,7 a 1,5 m).

| x | Conjunto | Como ficou |
|---|---|---|
| −177 a −161 | laboratório de fachada vermelha | 6,7 m, frente em y −52; nome em fonte genérica |
| −161 a −143 | casa antiga | mureta de 0,7 m com gradil até 1,4 m, portão a oeste; cobertura clara baixa |
| −246 a −223 | prédio térreo de testeira azul | frente em y −60 (triangulada), 4,1 m; guarita na frente; gradil só na metade oeste |
| −346 a −290 | Câmara Municipal | ala em três blocos com testeira verde e o nome; caixa cinza suspensa (6,7 a 9,5 m) sobre vidro com 5 aletas; gradil com portão de correr |
| −384 a −366 | CGU | 7,3 m, caixa azul e viga com o nome sobre colunas; gradil azul em y −47,1 |
| −457 a −421 | muro branco | 2,3 m (sem a concertina) |
| −653 a −616 | Ginásio da Guarnição | empena em arco assimétrico (seis degraus), ápice 13,2 m; térreo azul avançado 2,7 m com portão e seis janelas |
| −772 a −764 | portaria pequena | 3,7 m, colunas marrons, porta de vidro |
| −930 a −835 | muro e quatro residências | muro de 2,2 m com seis portões nos x medidos; casas de telhado de barro; a do leste com bloco mais alto |
| −1063 a −988 | sede administrativa | cinco pavimentos, 18,2 m, em L (corpo principal de 20 m de fundo, ala oeste recuada, ala dos fundos); gradil branco |
| −1153 a −1080 | muro de tijolo, cobertura metálica e casa branca | muro de 1,9 m com pilastras a cada 3,1 m |
| −1216 a −1159 | FUNASA | três pavimentos, 11,6 m, faixas azuis, aletas, painel central com o nome, bloco de entrada; mureta com gradil azul |

## 2. Verificação independente da primeira versão

Arquivo: `09_relatorios/verificacao_r28.json`. Minha primeira versão vinha de uma passada de consulta em que supus a distância da câmera pelo cadastro; o revisor triangulou.

**Graves, os cinco corrigidos:**
1. **Sede administrativa com planta errada:** eu tinha posto 17,5 m de prédio sobre uma área de 39 × 19 m que é estacionamento e a ala oeste 6 a 8 m recuada demais.
2. **FUNASA com quatro pavimentos; são três.**
3. **Muro de tijolo com 3,0 m; tem 1,7 a 1,9 m** (o topo fica abaixo da altura da câmera).
4. **Uma caixa na calçada da Câmara que não existe** (contorno falso do cadastro automático; saiu da planta).
5. **Prédio azul 5,6 m perto demais da rua.**

**Médias (12):** a caixa na frente do ginásio era na verdade o térreo avançado; arco do ginásio assimétrico; laboratório e CGU 4 m perto demais e ~15 % baixos; muro branco alto demais e em cima da ciclovia; pontas e portões do muro das residências deslocados; uma residência com bloco mais alto; Câmara (portão, blocos, altura das aletas); mureta e portão da casa antiga; painel, entrada e gradil da FUNASA; planta da cobertura metálica; portão e térreo da sede. **Leves (3):** nome da Câmara só no bloco leste; porta da casa branca; portaria com porta de vidro.

**Confirmou:** x e largura de todas as unidades; nenhum lado leste/oeste trocado; alturas do ginásio, da Câmara, da portaria, das residências A e B, do muro das residências e a altura total da sede (18,6 ± 1,0 m contra 17,5 do modelo, dentro da tolerância).

## 3. O que continua diferente do real

- **A versão refeita não passou por nova verificação.**
- Curvas saíram retas ou em degraus: arco do ginásio, fachada curva e canto arredondado da ala oeste da sede, cobertura em borboleta da casa branca.
- A cobertura metálica atrás do muro é aberta por baixo no real; no modelo é um volume fechado.
- Não postos: torre treliçada e parque de diversões atrás do prédio azul, garagem coberta da CGU, **prédio em obra em x ≈ −970 (não há contorno no cadastro)**, concertinas, sebes, árvores, abrigos de ônibus, mastros, sacadas e aparelhos de ar da sede, varanda da casa antiga.
- Nomes só em fonte genérica; brasões e logotipos não reproduzidos; o nome na base da sede e as placas do prédio azul e da portaria não foram lidos (sem texto).
- Sete contornos pequenos dentro dos lotes (edículas, guaritas) continuam como volume provisório.
- Volume térreo atrás da ala da Câmara: só pelo contorno e pela aérea.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200.

## 4. Arquivos

- Especificação: `02_referencias/frente_norte_oeste_20261112/` (`bloco_1_frente_norte.txt`, `monta_spec_norte_oeste.py`, `LEVANTAMENTO.md`, `contornos.json`, `consulta/` com o x no nome de cada captura).
- Gerador: `BVC_LADO=norte_oeste` em `03_blender/frente_norte_3d.py` (`patch_gerador_norte_oeste.py`); marco `BVC_EG_NORTE_OESTE` (pivô −700 / −60).
- Unity: `06_unity/registra_norte_oeste.py` (lista explícita dos contornos feitos e dos falsos); setores `BVC_S_X-4_Y-1` a `X-1_Y-1` reexportados; câmeras `r28_*`.
- Comparações: `07_comparacoes/r28/` (`r28a/` = primeira versão), `06_unity/compara_r28.py`.
