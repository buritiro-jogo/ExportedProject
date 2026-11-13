# BV City — Rodada 31 (13/11/2026)

Pedido do dono: *"Sim, refazer"* — refazer o trecho x 1230–1256 do mapa do Centro Cívico (do parceiro) para a emenda oeste da Av. Jaime Brasil bater.

**Resposta curta: fiz a parte que tem medida e parei antes de demolir prédio.** A cunha do mapa vizinho que entrava em diagonal no trecho medido (x 1247,95 a 1256) saiu; ali agora vale o chão medido, e a emenda é uma linha reta em x 1247,95. **A emenda ainda não bate**, e descobri por quê: o trecho vizinho não tem só as guias fora — a quadra inteira dele é diferente da real. Refazer só até x 1230 levaria o mesmo degrau 18 m para oeste e cortaria um prédio dele ao meio. Nada foi jogado (sem build) e nada foi commitado.

## 1. O que foi feito

- **Importador**: novo campo `chao_fora` no `marcos_cena.json` (retângulos no corredor). Onde um marco traz o chão medido, o importador tira do mapa do parceiro tudo o que cai no retângulo — chão, guia, pintura, colisores e objetos, com o triângulo partido na borda. Só na cena do BV City; o mapa original do Centro Cívico não é alterado. (Até aqui o importador só sabia tirar prédio; piso ficava sempre.)
- **Marco `BVC_JB_RUA`**: a faixa da rua agora cobre também a cunha x 1247,95–1256,5. Resultado da importação: 17 malhas e 12 colisores do parceiro recortados (74.111 → 74.013 triângulos), nenhum ficou vazio.
- **Listagem**: novo `BatchListaCaixa` (lê `06_unity/caixa_lista.txt`) lista, malha por malha, o que a cena tem numa caixa do corredor. Resultado em `06_unity/lista_caixa.txt`.
- Estado da cena: 0 erros de compilação, 0 falhas de malha, 100 de 100 pontos do `fb_data` sobre o NavMesh, 1 ponto de chão vazio em 95.200.

## 2. O que a listagem mostrou no mapa vizinho (x 1228 a 1248)

| | Mapa vizinho | Medido (levantamento) |
|---|---|---|
| Guia norte | y −29,0 | y −26,8 |
| Guia sul | y −12,3 | y −9,7 |
| Canteiro | terra, y −22,7 a −18,55 | ilha pavimentada, y −21,6 a −14,6 |
| Prédio do lado sul (x 1232,5 a 1247,9) | frente em y −10,4 | parede das lojas em y −5,4 |
| Prédio do lado norte (x 1227 a 1240) | frente em y −30,1 | parede das lojas em y −32,0 |

O prédio do lado sul avança ~5 m sobre onde ficam a calçada e parte da pista reais. As fachadas dali são genéricas (casas e lojas inventadas); pela aérea, o real é a mesma quadra comercial contínua, com o mesmo canteiro de árvores e quiosques, de uma rua transversal em x ≈ 1150–1170 até a Av. Sebastião Diniz.

## 3. Por que parei

Qualquer corte no meio da quadra deixa um degrau: a seção inventada e a real não se encaixam em lugar nenhum dela. O único lugar onde a emenda fica natural é a rua transversal em x ≈ 1150–1170, uns 80 m a oeste. Isso é três vezes o trecho que o dono aprovou e inclui retirar prédios do mapa do parceiro (na cena do BV City), por isso volta para decisão.

Para refazer a quadra inteira com o mesmo método das rodadas 29 e 30: um levantamento medido (fachadas dos dois lados + rua e canteiro, x ≈ 1150 a 1247), conversão, importação e uma conferência.

## 4. Arquivos

- `unity/FreeBala/Assets/FreeBala/Editor/FBBVCityPlantaImporta.cs` (`CortaChaoParceiro`, `BatchListaCaixa`; cópia de antes em `_backup/bvcity_r31_20261113/`).
- `02_referencias/jaime_brasil_20261112/rua/monta_rua_jb.py` (`XP`, `CHAO_FORA`), `06_unity/registra_rua_jb.py`.
- Comparações: `07_comparacoes/r31/`.
