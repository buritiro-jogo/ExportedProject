# BV City — Rodada 23 (12/11/2026)

Pedido do dono: *"Sim, tirar"* — tirar do canteiro norte as árvores do mapa da praça, que no vídeo não existem ali.

**Feito, e conferido só por mim nas fotos.** Nada foi jogado (sem build).

## O que mudou

- **Árvores:** as 14 árvores de rua do mapa da praça que ficavam no canteiro norte (x −100 a 333) são desligadas na importação. O mapa original do parceiro não foi alterado. Os outros 302 objetos do antigo canteiro (postes com a fiação, capim, motos) continuam indo para o canteiro novo.
- **Mureta sobre a calçada (x ≈ 125):** era a mureta da rampa da ESCON, que eu tinha deixado 2,4 m além da linha do lote. Agora termina na linha do lote; o perfil medido mostra a calçada livre de y −45,7 a −48,2 nesse ponto.

## O que continua em aberto na faixa

- Placas: a de regulamentação em frente ao TCE e as das esquinas ficam na calçada no real; na cena estão no canteiro.
- Pontas do canteiro em corte reto; sem ilhas de plantas (agave, cica), lixeiras e pictogramas da ciclovia.
- A versão corrigida da faixa (r22b) e esta rodada não passaram por verificação independente.
- Árvores dentro dos lotes (as reais ficam ali) não foram postas.
- O lado sul não foi medido.

**Estado da cena depois da importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200.

## Arquivos

- Importador: no passo "faixa norte (r22)", objeto `PA_Arvore_Rua*` com centro na faixa é desligado em vez de deslocado.
- `fachadas_spec.json` (`r23`): saliência do painel da rampa da ESCON de 4,0 para 1,5 m; cópia anterior `fachadas_spec_r22.json`.
- Comparações: `07_comparacoes/r23/` (`antes/` = rodada 22), `06_unity/compara_r23.py`.
