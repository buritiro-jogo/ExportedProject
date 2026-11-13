# BV City — Rodada 27 (12/11/2026)

Pedido do dono: *"ao concluir já prossiga para todo o restante de casas, ruas etc de todo o mapa"*.

**Resposta curta: as casas do mapa inteiro deixaram de ser caixas de topo plano.** O mapa tem 21.586 construções no cadastro e só 60 têm fachada própria; as outras 21.176 são volumes provisórios. Agora 14.446 têm telhado inclinado (7.491 em duas águas, 6.955 em quatro águas) e 6.696 continuam planas. **A forma é estimada**, por regra declarada; o que vem de medida é a classe da cobertura, lida na aérea. Um revisor independente achou erros na primeira versão (2 graves, 5 médios, 5 leves) e a regra foi refeita; **a versão refeita só eu conferi**. **Nada foi jogado** (sem build) e nada foi commitado.

## 1. A regra, por classe

| Classe na aérea | Quantas | Forma no modelo | Base |
|---|---|---|---|
| Telha de barro (avermelhado) | 4.596 | quatro águas a 25° | teste de iluminação do revisor em 925 telhados de barro: índice de quatro águas 0,85 ± 0,14 |
| Fibrocimento / metal velho (cinza) | 10.215 | duas águas a 12°, cumeeira no eixo maior, oitão fechado | o mesmo teste em 2.035 telhados cinza: 0,38 ± 0,10 (perto de zero descontado o artefato) |
| Clara (laje, metal novo) | 6.331 | plano | contraste de iluminação pela metade |

Outras regras: contornos da mesma classe que se encostam e têm a mesma altura ganham **um telhado só** (3.931 contornos em 1.683 telhados de grupo: geminados, casa com edícula, telhado que o cadastro partiu na cumeeira); a **altura total** estimada do cadastro é mantida (a parede desce pela altura do telhado, com piso de 2,7 m); contorno em L/U tem crista em cada ala; contorno redondo vira cone; ficam planos o prédio com lado menor acima de 25 m, parede acima de 7 m, contorno em várias partes e o que fica a menos de 6 m de fachada já modelada.

**Cemitério Nossa Senhora da Conceição:** os 266 "prédios" que o cadastro tem ali dentro são túmulos. Eram caixas de 3,8 m; viraram lápides planas de 0,8 m.

## 2. Verificação independente da primeira versão

Arquivo: `09_relatorios/verificacao_r27.json`. O revisor refez a exportação construção a construção (a contagem de triângulos bateu nos 160 setores) e olhou 552 contornos em 8 áreas espalhadas: a classe bate em 464 e erra em 23.

**Graves, os dois corrigidos:**
1. **O fibrocimento não é quatro águas.** Eu tinha aplicado quatro águas a tudo (69 % dos telhados inclinados são cinza). Agora é duas águas.
2. **210 túmulos do cemitério tinham ganhado telhado de casa.** Agora são lápides.

**Médios:** o telhado tinha sido somado em cima da caixa, deixando a casa 0,9 m mais alta na mediana (corrigido: altura total mantida); alas de contorno em L ficavam chatas (corrigido); cada casa geminada ganhava a sua pirâmide (corrigido: telhado por grupo); custo de +39 % de triângulos, mais da metade em forro de beiral (corrigido: sem forro; o aumento ficou em +21 %). **Não corrigido:** a classe não vale para todo contorno — 1.108 "cinza" são copa de árvore por cima do telhado, há "barro" lido em lote de terra sem construção, e 2 m de erro de registro mudam ~11 % das classes.

**Leves:** platô em prédio grande (agora plano acima de 25 m de lado), torre e hotel com telhado de casa (agora plano acima de 7 m de parede), um volume que ficou sem tampa (corrigido), triângulos sem área (filtrados), telhado entrando em contorno modelado (plano a menos de 6 m).

## 3. O que continua diferente do real

- **A versão refeita não passou por nova verificação.**
- A forma é uma regra por classe, não medida casa a casa: a aérea de 0,6 m por pixel só deixou o revisor ver a forma em 48 de 552 contornos.
- Inclinação (25° e 12°) e beiral (0,30 a 0,35 m) são valores típicos, não medidos.
- Classe errada onde há árvore por cima, sombra ou lote vazio (ver acima).
- Igrejas e galpões grandes ficam planos; o real costuma ser duas águas.
- As paredes continuam lisas, sem portas nem janelas: são volumes provisórios, não casas modeladas.
- Custo no jogo (quadros por segundo) não foi medido: sem build.

**Estado da cena depois da última importação:** 0 erros de compilação; 100 de 100 pontos do `fb_data` sobre o NavMesh novo; colisor de chão com 1 ponto(s) vazio(s) em 95.200; planta com 794.086 triângulos.

## 4. Arquivos

- `06_unity/telhado_forma.py` (regra), trecho `r27b` de `06_unity/exporta_unity.py` (telhado por grupo, altura total, túmulos), `06_unity/cemiterios.geojson` (polígono do OSM).
- 160 setores reexportados em `06_unity/glb/`; cópia anterior em `_backup/bvcity_r27_20261112/glb/`.
- Comparações: `07_comparacoes/r27/` (`antes/` = topo plano; `r27a/` = primeira versão), `06_unity/compara_r27.py`.
