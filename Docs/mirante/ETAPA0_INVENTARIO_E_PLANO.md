# Mirante + gráfico Ultra: Etapa 0 (inventário e plano do blockout)

Data: 03/11/2026. Ramo: `claude/mirante-map-modeling-6bcbae`. Ele parte do mesmo commit da `main` (`5b823dfab`), então não precisa atualizar.
Não usei rebase: o `CLAUDE.md` proíbe reescrever histórico. Quando o ramo ficar para trás, atualizo com merge.

Nada no Unity foi alterado nesta etapa. Este arquivo é só leitura e plano.

## 1. Versões instaladas

| Item | Versão | O que isso libera |
|---|---|---|
| Unity | 6000.6.6f1 (o 6000.6.7f1 do Ruan abre igual) | |
| URP | 17.6.0 (embutido) | Forward+, **Deferred+**, **STP** e TAA, **GPU Resident Drawer** com GPU Occlusion Culling, **APV** com sky occlusion e Lighting Scenarios, **SSR nativo** (renderer feature `ScreenSpaceReflection`), decals em DBuffer, HDR de 64 bits |
| ProBuilder | 6.1.4 | blockout |
| AI Navigation | 2.0.15 | NavMesh |
| Névoa `com.cqf.urpvolumetricfog` | 0.5.91 (pacote local, MIT) | já funciona com render graph no Unity 6 |

**Não existe no URP:** reflexo planar pronto. As opções são o SSR nativo, que é o que recomendo, ou um script próprio de câmera espelhada para o espelho d'água. O script custa uma renderização extra da cena.

## 2. Tiers de qualidade atuais (`QualitySettings.asset`)

| Índice | Tier | URP asset | Padrão em |
|---|---|---|---|
| 0 | Mobile | `FB_URP_Mobile` | Android e iPhone |
| 1 | PC Médio | `FB_URP_PCMedio` | |
| 2 | PC Alto | `FB_URP_PC` | **Standalone (PC)** e consoles |
| 3 | Web | `FB_URP_Web` | WebGL |

O Ultra entra como **índice 4**, novo, e passa a ser o padrão do Standalone. Os índices 0 a 3 não mudam, e o Mobile e o Web continuam apontando para os mesmos assets.

## 3. `Assets/FreeBala/Settings/` (o PC Alto de hoje)

- **`FB_URP_PC`:**
  - Forward+, HDR em precisão 32 bits e MSAA 4x;
  - sombra principal de 4096, 4 cascatas e distância de **150 m**, com soft shadow alta;
  - Depth Texture ligada e **Opaque Texture desligada**;
  - Resident Drawer desligado;
  - **light probes no sistema antigo**, ou seja, sem APV;
  - reflection probes com blending, mas sem box projection.
- **`FB_URP_PC_Renderer`:** Forward+ com as features Decais (em **Screen Space**, não DBuffer), NevoaAltura, SSAO e NevoaVolumetrica.
- **`FB_Pos_PC`:** Tonemapping ACES, WhiteBalance, Vignette, Bloom, ColorAdjustments e um volume de **SSR**.
  - **Achado:** o renderer não tem a feature de SSR, então esse volume hoje **não faz nada**.
- Também existem `FB_URP_PCMedio`, `FB_URP_Mobile`, `FB_URP_Web` (cada um com o seu renderer), `FB_Pos_Mobile`, `FB_NevoaAltura.mat` e `FB_LuzVolumetrica.mat`.

## 4. O que já existe do Mirante (muita coisa)

O mapa do Mirante **já está no jogo**, como o mapa `orla` (cena `Assets/FreeBala/Scenes/Map_orla.unity`, arena v7).

- **Modelo:** `Assets/BURITIRO_Mapas/orla/orla_unity.glb` (470 MB, no LFS), recortado do `mirante_orla.glb` do Blender, que tem 10,2 milhões de triângulos.
  - 8.074 nós, 1.654 malhas e 374 materiais;
  - 47 células de HLOD em LOD0, LOD1 e LOD2, com 1,70 milhão, 1,12 milhão e 0,38 milhão de triângulos;
  - 732 colisores e 309 mil triângulos de horizonte.
- **A torre já foi modelada no Blender** (thread "Detalhar torre do Mirante Edileusa Lóz", área 1):
  - 120 m de altura, com observatório a 93 m;
  - elevador de vidro na face S, 6 varandas e passarela estaiada de 16 m;
  - 31 materiais `A1_*`, entre eles `A1_porcelanato_observatorio`, `A1_vidro_elevador`, `A1_vidro_guarda_corpo`, `A1_chapa_cupula`, `A1_antena_vermelho`/`_branco`, `A1_fita_led_violeta` e `A1_cabo_aco_estai`.
  - A geometria está **fundida nas células** `C_-1_-1`, `C_0_-1`, `C_-4_0` e `Chao_-3_0`/`-4_0`. A torre não é um objeto separado.
  - O resumo do projeto diz que **o interior e o topo são aproximados**.
- **Hoje a torre não é jogável:** um bloqueio, `COL_Bloqueio_Torre_Mirante`, fecha de 3 m a 116 m de altura, numa caixa de 26 x 26 m.
  - Em coordenadas do Blender, a caixa vai de x 16,5 a 42,5 e de y 55,8 a 81,8. No Unity, o x troca de sinal.
- **Arena:** centro em (-150, -20) e raio de 300 m. A torre fica a uns 200 m do centro, ainda dentro da arena.
- **O que a arena já tem:** a Selvinha, a praia, a passarela sobre o canal, o NavMesh (`NavMesh_orla.asset`), spawns, rotas (`lanes`), `basePairs` e rampas, tudo em `orla_dados.json` e no `fb_data`.
- **Os arquivos-fonte do Blender** (`mirante_orla.glb`, `_TRABALHO/area1_torre`, `prepara_unity.py`, `pipeline.sh`) ficam na pasta compartilhada da nuvem.
  - Neste PC só existe o GLB final e uma exportação antiga em `Documents\BURITIRO_MAPAS_EXPORTADOS\Mirante_Orla.glb`, de 9,8 MB.
- **Desempenho:** a Orla já é **o mapa mais pesado do jogo**, com 2.969 instâncias no LOD0, 2.405 delas árvores.
  - Ela ainda espera o teste A/B do GPU Resident Drawer.
  - Isso pesa direto na meta de 60 fps estáveis no Ultra.
- **`autoteste/`:** só tem prints antigos de moto e carro, e nenhum teste para rodar.
  - Os testes automáticos de verdade são o `-fbtesteexe` no .exe (ver o `CLAUDE.md`).

## 5. Referências: PARADA

- A pasta `Assets/BURITIRO_Mapas/mirante/_referencias/` **não existe**.
- Fora do repositório há 81 capturas do Street View em `Documents\BURITIRO_REFERENCIAS\MIRANTE_ORLA\references\mirante_edileusa_loz`.
  - São 8 pontos, cada um com 6 direções, chão e 4 inclinações olhando para a torre.
  - Pela regra do projeto, **imagens do Google são só referência e nunca entram em entrega**, então não as copio para o repositório.
- **Ângulos que faltam,** mesmo contando o Street View:
  - a plataforma por dentro, com o piso de vidro;
  - o guarda-corpo de perto;
  - a cabine e o poço do elevador;
  - o hall de entrada;
  - a escada, se houver;
  - a cúpula e a base da antena;
  - a torre à noite, iluminada;
  - a passarela até a Orla Taumanan.

## 6. Plano do blockout (proposta)

### 6.1 Não refazer o que já existe

Fazer um blockout do mapa inteiro em ProBuilder descartaria a arena v7: o modelo, as células de HLOD, os colisores, o NavMesh, as rotas e as bases, tudo já testado no 10x10. A proposta:

1. **Manter a arena v7 como base** (chão, orla, parque, quarteirões e horizonte).
2. **Tirar a torre das células e transformá-la num objeto próprio** (`Mirante_Torre`), com LOD0 a LOD3 e colisão simples. Assim dá para refinar o topo e o interior sem regerar o GLB de 470 MB.
3. **Blockout em ProBuilder só do que é novo:** os acessos à torre que a opção escolhida em 6.3 pedir, a cobertura na plataforma e os ajustes no espelho d'água e na passarela. Tudo em escala 1:1 e conferido com a cápsula do jogador (raio 0,3 m).

### 6.2 Área jogável e horizonte

- **Área jogável:** continua o círculo da v7, com 300 m de raio. A torre fica dentro.
- **Horizonte:** a 93 m de altura, a pessoa enxerga a uns 35 km. Hoje o horizonte da arena tem 3 km de cidade em malha simplificada.
  - **Proposta:** mais um anel de impostores (cards em octaedro) da base C de Boa Vista, que já existe no Blender, de 3 a 8 km.
  - Além disso, um card ou malha leve da **Serra Grande** ao sul e da margem oposta com a Praia Grande.
  - Isso só é preciso se a plataforma for jogável (opções B e C).

### 6.3 Plataforma a 93 m: escolha uma

| | A. Torre fechada (como hoje) | B. Plataforma jogável, acesso só pelo elevador | C. Varanda média jogável, topo fechado |
|---|---|---|---|
| Acesso | nenhum | 1 cabine de vidro, ~25 s de subida, visível e com som; quem chama espera | escada externa até uma das 6 varandas (~30 m) |
| Força do sniper | não existe | muito alta: 93 m de altura enxerga a arena inteira | média: altura parecida com a de um prédio de 10 andares |
| Contra-jogo | | cabine exposta na subida; núcleo central de concreto quebra a visão 360°; guarda-corpo de vidro **inquebrável**, mas parapeito baixo; teto sem cobertura; sem munição lá em cima | varanda com cobertura parcial, 2 rotas de flanco pela base e linha de visão cortada pelas árvores da Selvinha |
| Trabalho | só o visual (topo e interior bonitos para quem olha de baixo) | elevador funcional, NavMesh vertical (bots usando elevador = trabalho grande), horizonte de 8 km | escada, NavMesh com rampa, horizonte pequeno |
| Risco no 10x10 | nenhum | alto: pode decidir as rodadas | baixo/médio |

**Recomendo a C**, com o observatório (opção A) bonito de ver. A B dá o momento mais "uau", mas vai exigir horizonte de 8 km, bots que usam elevador e muito teste de equilíbrio.

### 6.3.1 DECIDIDO (03/11/2026): opção C

O Ruan escolheu a **C: varanda média jogável, topo fechado**.

**Medidas da torre que está no GLB da arena.** Coordenadas do glTF; no Unity, o x troca de sinal.

| Peça | Altura (m) | Planta (x / z) |
|---|---|---|
| 6 varandas (`A1_grade_aco_varanda`) | 12,6 · 25,0 · 37,4 · 49,8 · 62,2 · 74,6 (uma a cada 12,4 m) | x 25,2–33,9 / z 62,9–73,1 (8,7 x 10,2 m) |
| Porta em cada varanda (`A1_porta_aco_cinza`) | as mesmas 6 alturas | face z 68,4–69,3 |
| Elevador de vidro (`A1_vidro_elevador`) | 0,2 até 80,7 | face z ≈ 73,6 |
| Forro e LED do observatório | 80,1 a 90,8 | x 21,5–38,6 / z 61,3–83,4 |
| Piso do observatório (`A1_porcelanato_observatorio`) | **86,8** | |
| Cúpula | 91,0 a 93,6 | |
| Antena | 101,9 a 113,5 | |

**Atenção:** no modelo, o piso do observatório está a 86,8 m, e não nos 93 m do dado real. A causa provável é o modelo usar 93 m como o topo da cúpula. Só corrijo isso se as fotos ou a medida oficial confirmarem.

**Proposta para a C** (detalhes finais depois das fotos):
- **Varanda jogável:** a 2ª, a **25,0 m**, mais ou menos a altura de um prédio de 8 andares. Ela enxerga por cima das árvores da Selvinha, sem dominar a arena inteira.
  - Se no teste ficar forte demais, troco pela 1ª, a 12,6 m.
- **Acesso (decidido em 03/11): pelo elevador panorâmico,** sem escada.
  - É o elevador de vidro que já está no modelo, na face z ≈ 73,6 (sul no Blender).
  - Ele para só em 2 andares: o hall térreo e a varanda 2.
  - Leva uns **10 s** de subida (2,5 m/s), com 1,5 s de porta abrindo e fechando.
  - Tem botão de chamada nos dois andares e cabe até 3 pessoas.
  - A cabine é de vidro e fica **visível e atirável de fora durante a subida**. Esse é o risco de quem sobe. O vidro não quebra, mas as balas atravessam.
  - Tem som de motor e campainha, para quem está perto saber que alguém está subindo.
  - Se a cabine estiver lá em cima, quem chama embaixo espera ela descer. Isso cria disputa pela torre.
- **Bots:** também usam o elevador. Uma ligação no NavMesh liga o hall à varanda; o bot chama a cabine, espera, entra e sai.
  - Se os bots travarem no teste, o plano B é deixar só o jogador usar o elevador e tirar a varanda das rotas dos bots.
- **As outras 5 varandas e o observatório continuam fechados.** O `COL_Bloqueio_Torre_Mirante` vira um bloqueio com recorte só no hall, no poço do elevador e na varanda 2.
- **Equilíbrio:**
  - parapeito de grade, com guarda-corpo de 1,10 m que só cobre quem estiver agachado;
  - o núcleo de concreto quebra a visão de 360°, então quem está na varanda cobre uma face por vez;
  - o elevador é a única entrada e saída: quem está na varanda ouve a cabine chegando, e quem sobe fica exposto no vidro;
  - nenhuma base nasce com linha de visão direta para a varanda (vou conferir com `visao_base_a_base`).
- **Cobertura:** 1 ponto no hall e 1 na varanda.
- **Teste:** `-fbtesteexe paredes orla` e `10x10 orla 3 9x9`, comparando com a v7 (as vitórias por time não podem pender para quem nasce perto da torre).

### 6.4 Spawns, rotas, cobertura e veículos

- Os spawns e os `basePairs` da v7 ficam como estão. Eles já foram validados com caminho completo nos dois sentidos.
- Na opção C:
  - entra uma rota nova `lane` até o hall do elevador;
  - entram 2 pontos de cobertura na base da torre;
  - refaço o NavMesh e rodo `-fbtesteexe paredes orla` e `10x10 orla 3 9x9` para conferir que nenhum time ganhou vantagem.
- Os veículos continuam nas vagas `parked` da v7.

## 7. Ultra: o que muda em relação ao PC Alto (resumo, detalhes na Etapa Ultra)

Arquivos novos: `FB_URP_Ultra`, `FB_URP_Ultra_Renderer`, `FB_Pos_Ultra` e o tier 4 "Ultra".

- **Renderização:**
  - comparar Deferred+ e Forward+ (a versão tem os dois);
  - HDR de 64 bits;
  - comparar **STP** com MSAA 8x usando a mesma cena (guarda-corpo, cerca e folhas);
  - ligar Opaque Texture;
  - SSR **ligado de verdade** (adicionar a feature);
  - decals em DBuffer (Albedo, Normal e MAOS);
  - SSAO em resolução cheia.
- **Sombras:** a principal em 8192, 4 cascatas com distância maior que 150 m (a torre tem 120 m) e luzes adicionais com atlas grande.
- **GPU:** Resident Drawer e GPU Occlusion Culling ligados. Isso já é o teste A/B que a Orla está esperando.
- **Iluminação:** APV com sky occlusion e Lighting Scenarios (meio-dia, pôr do sol e noite).
  - **Atenção:** trocar para APV muda o sistema de light probes do asset. Faço isso só no Ultra, mas as cenas vão precisar de bake de APV.
  - Também ligo box projection nos probes.

Antes de mexer no Ultra, vou medir o desempenho atual da Orla no PC Alto, para ter uma base de comparação.

## 8. O que preciso do Ruan para seguir

1. **Fotos de referência** na pasta `Assets/BURITIRO_Mapas/mirante/_referencias/`, cobrindo os ângulos da seção 5, de preferência fotos próprias ou com licença.
   - Ou então autorização para usar o Street View só como consulta, fora do repositório.
2. ~~Escolher A, B ou C~~: **escolhida a C** em 03/11 (seção 6.3.1).
3. **Confirmar** que posso tirar a torre das células e transformá-la num objeto próprio (6.1), em vez de fazer o blockout do mapa inteiro.
4. **Abrir o Blender** com o add-on do MCP ligado, para a estrutura da torre e os guarda-corpos.
5. **Combinar a vez no Unity:** a thread "Ajustar física" está pausada, mas só um pode editar o Unity por vez.

## 9. Etapa 2: correção do visual (03/11/2026)

Fotos em `Docs/mirante/screens/etapa2_*` (antes = PC Alto, depois = Ultra; mesmos 7 ângulos). As fotos são de editor (`FBMiranteFotos`), com a névoa e a luz ambiente que o `FBWeather` monta no jogo.

- **Torre escura → branca.** O fuste usava `A1_concreto_pintado_branco` com uma textura de **piso** (`TX_PBR_A1_concreto_pintado_branco__chao`), e os guarda-corpos e frisos (`A1_aco_cinza_claro`) tinham metal 0,7. Com os reflection probes assados sem textura, a torre refletia o chão marrom. As cópias corrigidas ficam em `BURITIRO_Mapas/mirante/materiais/MIR_*_corrigido` e só trocam os materiais na `Map_orla` (`FBMiranteJogavel.MateriaisTorre`). O GLB não muda. Se a Orla for remontada, rode `Montar` de novo.
- **Névoa "sopa cinza" do alto da torre.** A névoa linear da arena ia de 70 a 512 m, e no jogo o fim fica em 85% disso, uns 435 m. No **Ultra**, o `FBWeather` estende a névoa para ~150 m até ~1,1 km. Os outros níveis não mudam.
- **Pôr do sol.** As fotos agora usam o sol do "anoitecer" do `FBWeather` e o céu (`_SunDir`/`_Dusk` do `FBSky`).

**Pendências que apareceram:**
1. Com a névoa mais longe no Ultra, os **blocos simples do horizonte** (caixas sem detalhe) aparecem ao fundo. Isso pede impostores e cartões do horizonte de 3 a 8 km (item 6.2).
2. O **reflection probe** da arena continua sendo assado sem textura (problema antigo). Para o Ultra: probes com box projection e um probe na plataforma.
3. As fotos do .exe e a medição de FPS ainda não foram feitas.
