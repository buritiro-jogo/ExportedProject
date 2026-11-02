# PENDENTE: tarefas para o Claude Code do terminal

Escrito em 02/11/2026 pela thread "Ajustar física do jogo BURITIRO", que entrou em pausa a pedido do Ruan.
Enquanto ela estiver pausada, o terminal pode editar o Unity. Leia o `CLAUDE.md` antes de começar.

## Antes de tudo

1. Rode `git pull` e depois `git status`. A árvore tem que estar limpa (`Assets/_Recovery*` pode ser ignorado).
2. Feche o Play do Unity e confira que nenhum build ou `BURITIRO.exe` de teste está rodando.
3. Faça uma tarefa por vez, nesta ordem. Faça commit e push só depois que a tarefa compilar e passar no teste.

## Tarefa 1: bots e piloto travando na ponta de muro (correção pronta)

**Sintoma:** o piloto, o bot que joga no lugar do jogador, ficou preso 5 s na Praça do PT em (-240,7, 0,17, 45,8). O ponto fica na ponta do muro grafitado da R. Carmelo (`COL_G_PT_Muro_Grafitado_R_Carmelo_concreto`). Os bots usam a mesma lógica de andar e podem travar do mesmo jeito.

**Causa,** confirmada pelo teste `paredes` em `..\..\_diag\paredes_pt\relatorio.txt`:
- O muro e o NavMesh estão certos: o caminho é completo e contorna a ponta do muro.
- O problema está em como o caminho é seguido, em `FBBot.Steer` e `FBPiloto.Navega`:
  - o canto é largado quando o bot chega a 1,3 m dele, e o bot já mira o canto seguinte, do outro lado da ponta do muro;
  - o atalho de canto usa `FBWorld.ClearLine`, dois raios finos;
  - o raio passa raspando a ponta, mas a cápsula do personagem (raio 0,3 + pele 0,08) bate e fica empurrando o muro.

**Correção pronta:** `Docs/pendente_02nov/correcao_muro.patch`. Ela compila com 0 erros, mas ainda não foi revisada nem testada no .exe.
- `FBWorld.ClearLineLarga(a, b)`: além do `ClearLine`, exige linha livre 0,4 m para cada lado. É sempre mais rígido que o `ClearLine`, nunca libera o que ele bloqueia.
- `FBWorld.CorpoLargo` (padrão `true`): com `false`, volta ao comportamento antigo, para comparar A/B.
- `FBBot.Steer`: o canto só é largado a 1,3 m se o corpo passa livre até o próximo canto. Senão, só a 0,6 m. O atalho de canto passa a usar `ClearLineLarga`.
- `FBBot` (linha com `Estrito || destPendente`): ir reto até um destino a menos de 12 m também passa a exigir `ClearLineLarga`.
- `FBPiloto.Navega`: a mesma regra dos bots.
- `FBTesteExe`: nova opção `cantofino`, que liga `FBWorld.CorpoLargo = false` para o teste A/B.

**Como aplicar:**

```powershell
git apply --check --3way Docs/pendente_02nov/correcao_muro.patch
git apply --3way Docs/pendente_02nov/correcao_muro.patch
```

O patch espera estes MD5 nos arquivos de antes: FBBot `8ab3e8b5d0e1e0cf26a35a8e87bd9fba`, FBWorld `1c7c63a4e064064bc086267d435d262f`, FBPiloto `074e0f4d1e377a8f8c692d15c8885fd3`, FBTesteExe `2f5e52f03b4723896c459879e72fe954`.

- Se o patch não aplicar porque o fim de linha é diferente, tente `git apply --ignore-whitespace`.
- Se alguém mudou esses arquivos depois de 02/11, aplique a mudança à mão.
- Os arquivos finais, inteiros, estão em `Docs/pendente_02nov/arquivos/`. Use-os só como referência, não por cima de mudança nova.
- Faça backup em `..\..\_backup\muro_<data>\` antes.

**Como testar:**
1. Compile com `lab_run.ps1 -Pedido refresh`; tem que dar `erros 0`. Depois gere a build com `-Pedido build`.
2. Crie uma lista em `..\..\_diag\` e rode com `lanca_noite.ps1`:
   ```
   muro_piloto_novo|10x10 praca_pt 7 piloto mirabot|30
   muro_piloto_antigo|10x10 praca_pt 7 piloto mirabot cantofino|30
   muro_pt_9x9|10x10 praca_pt 3 9x9|14
   muro_orla_9x9|10x10 orla 3 9x9|14
   ```
3. Compare as duas versões. Com a correção, tem que ter igual ou menos de:
   - `piloto PRESO`;
   - `presos:` (saídas a pé no stuckN 4);
   - `bots realocados`.
4. Confira também que as rodadas não ficaram mais lentas: o "primeiro tiroteio com X s" deve continuar parecido, uns 25 a 50 s.
5. Com o exe rodando, não mexa no PC: perder o foco pausa o jogo.
6. Se melhorou, faça commit (só os 4 arquivos, com mensagem em português) e push.

## Tarefa 2: NavMesh passando por dentro de objetos (achado, sem correção)

O teste `paredes` achou objetos que têm colisão, mas não abriram buraco no NavMesh. O bot planeja passar por dentro deles e trava. Os piores:
- Praça das Águas:
  - parquinho `PA_parquinho_166` (180 arestas);
  - food truck `FB_PropsTrocados/FoodTruck_011`;
  - mesas `PA_mesas_*` e ônibus `PA_onibus_063`.
- Praça do PT e Águas: pontos de ônibus `FB_Rua171/x171_PontoOnibus*`, orelhões e lixeiras.

A lista completa está em `..\..\_diag\paredes_<mapa>\relatorio.txt`, nas linhas "N arestas (N parede, ...)".

**Ideia:** no começo do 10x10, depois do recorte das paredes da arena (que já usa `NavMeshObstacle` com `carving` em `FBModo10x10`), pôr um `NavMeshObstacle` com `carving` e `carveOnlyStationary` no tamanho de cada `BoxCollider` desses grupos.
- Não use para veículos que andam (o `Caveirao` aparece na lista, mas se move).
- Ignore os resultados "teto" a 3 m de altura ou mais: é NavMesh em cima de telhado e não atrapalha.
- Teste com `-fbtesteexe paredes <mapa>`: o número de "atravessam parede" tem que cair.

## Tarefa 3: economia de CPU para 120 FPS (só o plano)

O plano, em inglês, está em `Docs/pendente_02nov/cpu_plan.md`. O grupo 1 é seguro; o grupo 2 exige cuidado. Ele foi feito a partir da auditoria da partida 10x10, com uns 20 personagens. Nada disso foi aplicado.
- Faça depois da tarefa 1, porque mexe no mesmo `FBBot.cs` e `FBWorld.cs`.
- Meça antes e depois com `10x10 orla 3`, rodando duas vezes cada versão.

## Já decidido (não mexer sem falar com o Ruan)

- 10x10 v14: fica como está. Isso foi decidido pela thread do 10x10 com base nos testes resumidos em `Docs/pendente_02nov/v14_RESUMO.md`.
- Fica anotado para depois: a rodada 1 (pistola) ainda acaba no tempo em 5 de 6 partidas, porque a pistola mata pouco (10% de acerto, primeira morte aos 62 s).
- O dinheiro abaixo do minimapa já está corrigido e confirmado em print.
