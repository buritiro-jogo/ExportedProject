# Piscada no menu ao escolher mapa (corrigido em 10/11/2026)

## O que acontecia

Qualquer clique dentro da tela **ESCOLHER PARTIDA** — mapa, modo, dificuldade, horário, clima, lado —
apagava a tela inteira e a trazia de volta. O efeito era uma piscada a cada interação.

## A causa, no código

Os botões da página chamam `MatchPicker()` de novo:

```csharp
// FBMenu.cs, cartão de mapa
FBKit.Tile(b, "mapa_" + m.id, x, y, CW, 124, sel,
           () => { FBSession.map = m.id; FBSession.Save(); MatchPicker(); });
```

E `MatchPicker()` começa com `Page("ESCOLHER PARTIDA", true)`, que fazia **três coisas caras** a cada clique
(`FBMenuInicio.cs`):

1. `Clear()` — destrói **toda** a interface. Como `Destroy` só vale no fim do quadro, por um quadro o que
   estava saindo e o que estava entrando apareciam **juntos**.
2. `StageCam(false)` e `preview.SetActive(true)` — mexia na câmera do palco e **ligava o personagem 3D**,
   que a própria `MatchPicker` desligava na linha seguinte. O boneco piscava junto.
3. `FBFadeIn.Add(p, 0, 10)` — a página nova nascia com **alpha 0** e subia 10 px ao longo de 0,22 s.

Ou seja: cada clique = apagar a tela, ligar e desligar o personagem, e trazer tudo de volta do preto.

## O que mudou

Em `Assets/FreeBala/Runtime/UI/FBMenuInicio.cs`:

1. **`Clear()` desliga antes de destruir**, acabando com o quadro em que os dois conjuntos apareciam juntos:

   ```csharp
   void Clear() { foreach (Transform t in root) { t.gameObject.SetActive(false); Destroy(t.gameObject); } ... }
   ```

2. **`Page()` sabe quando é a mesma página.** Um campo `curPage` guarda qual está aberta. Se o título é o
   mesmo, a página está só **se atualizando**, não trocando: sem fade, sem mexer na câmera do palco e sem
   ligar/desligar o personagem.

   ```csharp
   bool mesma = title == curPage; curPage = title;
   Clear(); atHome = false; userYaw = 0;
   if (!mesma) StageCam(false);
   if (preview && !mesma) { ... preview.SetActive(true); }
   var p = ...;
   if (!mesma) FBFadeIn.Add(p, 0, 10);
   ```

**A transição entre telas diferentes continua existindo** — os 0,22 s de fade com 10 px de subida, que são
discretos e dão a sensação de acabamento. O que saiu foi a transição onde ela não fazia sentido: dentro da
mesma tela.

## O que NÃO mudei, de propósito

- **`Home()` continua refazendo câmera e personagem.** Há chamadas de `Home()` que existem justamente para
  trocar a skin ou a pose (`FBMenu` no modo `-fbauto`, o laboratório de poses, a volta do PERSONALIZAR).
  Se eu pulasse o `Preview()` ali, o personagem não se atualizaria. A ida e a volta do INÍCIO são troca de
  tela de verdade, então o fade é o comportamento certo.
- **Nenhuma funcionalidade saiu.** Os cliques fazem exatamente o que faziam; só o efeito visual mudou.

## Um ponto que fica anotado, fora do escopo

Todo clique chama `FBSession.Save()`, que faz `PlayerPrefs.Save()` — escrita em disco a cada seleção. Não é
a piscada (é engasgo, não flash), e mexer na hora de gravar pode custar configuração perdida, então deixei
como está. Se aparecer engasgo ao clicar rápido, o caminho é adiar a gravação para quando a tela fechar.

## Como conferir

Abrir o jogo, ir em **JOGAR → ESCOLHER PARTIDA** e clicar em vários mapas e modos seguidos. A tela deve
trocar o destaque na hora, sem escurecer, sem subir e sem o personagem aparecer e sumir. Voltar para o
INÍCIO e entrar de novo deve manter a transição suave de antes.
