# Rodada 69: online com armas, 2 × 2 sorteado e chat de voz (13/11/2026)

Pedido: "corrija a parte de ligar o microfone para se comunicar com os amigos, e a parte online, para que possamos ter modos de rolê com
armas, modos 2v2 quando 4 amigos forem selecionados, dividindo aleatoriamente 2 amigos vs 2 amigos". E uma build dev sem lixo.

## O que estava errado

- **Microfone:** não existia chat de voz. A entrada "mic" do controle não fazia nada, e a tecla M é o mapa.
- **Rolê online:** o modo tirava as armas de todo mundo (`wu.Strip()`), então não havia rolê com armas.
- **Squad:**
  - o `pgo` punha todos no mesmo time, então não havia como jogar amigo contra amigo;
  - as bases do mata-mata online eram sorteadas em cada PC, então cada jogador via a área num lugar diferente.
- **Servidor:**
  - o `servidor.txt` apontava para o VPS antigo, que foi desligado;
  - o VPS novo (Brasil, o do ramo mobile do parceiro) responde ao ping, mas a porta **8123 estava fechada** em 13/11.

## Como ficou

**Tela SQUAD → CONTRA JOGADORES.** Só o líder escolhe o modo:

| Modo | O que é |
|---|---|
| DE ROLÊ | Cidade livre, sem armas (igual antes) |
| ROLÊ ARMADO | Cidade livre, cada um com o próprio arsenal, jogadores inimigos entre si. Morreu, volta em 3 s longe dos outros. Loja aberta. |
| TIMES | Mata-mata em times; o squad fica do mesmo lado |
| 2 × 2 | Só com 4 no squad. O servidor sorteia 2 contra 2 numa sala privada. A área de combate é a mesma nos 4 PCs (semente = código da sala). Vence quem chega a 20 abates, ou quem lidera em 10 min. Quem cai e volta continua no mesmo lado. |

**Microfone.**
- **Como ligar:** Caps Lock, o botão "LIGAR MICROFONE" na tela SQUAD ou a pílula "MIC" na partida (no celular, toque na pílula).
- **Quem ouve:**
  - no menu, os outros do squad;
  - na partida, a sala toda no rolê e só o próprio time no TIMES e no 2 × 2.
- **Como funciona:** só transmite quando há voz (porta de ruído). Usa 16 kHz IMA ADPCM, cerca de 11 KB/s enquanto fala.
- **Eco:** sem fone de ouvido, o microfone do amigo pode pegar o som da caixa, porque o jogo não cancela eco.

**Arquivos:**
- `Runtime/Net/FBVoiceChat.cs` (novo);
- `FBNet`, `FBLobbyNet`, `FBLanServer` e `Core/FBSession` (com `netMode`);
- `Modes/FBMatch`: `PvpPlayers`, `Arena2x2`;
- `Modes/FBModes`: `DeRoleArmado`, `Duelo2x2`;
- `UI/FBMenuPaginas`: os botões de modo e de microfone;
- `game/server.mjs`.

**Protocolo novo:**
- `pgo` com `nm`;
- a resposta traz `nm`, `team` e `lados`;
- eventos `ev vc` (voz na partida) e `pvc` (voz no squad);
- caps `rolex`, `x2` e `voz`.

O servidor antigo ignora tudo isso, e o jogo avisa ("servidor antigo").

## Testes feitos

`unity/tools/lan/teste_protocolo.mjs` ganhou um bloco r69 que confere:
- o sorteio 2 contra 2;
- a sala privada, que não aparece na lista e não aceita intruso;
- a voz só para a dupla no 2 × 2 e para a sala toda no rolê;
- a voz do squad no menu, sem eco de volta;
- o descarte de pacote grande;
- o placar por time;
- quem cai e volta no mesmo lado;
- o 2 × 2 recusado com squad de 2.

Resultados:
- `server.mjs`: 62/62.
- Host da REDE LOCAL (`FBLanServer`, fora do Unity): 65/65.
- Codec de voz: 33 dB de relação sinal/ruído com um pacote perdido; cerca de 1,1 mil caracteres por pacote de 100 ms.

- **No executável da demo:**
  - o Host dentro do jogo passou em 65/65;
  - dois jogos no mesmo De Rolê online funcionaram (carro, loja e tiros, com 0 falhas);
  - a tela SQUAD foi fotografada com amigos de mentira (`squad_modos_r69.png`).

**Não testado:** microfone de verdade entre dois PCs, 2 × 2 com 4 pessoas reais e ROLÊ ARMADO ponta a ponta no exe (o teste de dois jogos
usa o De Rolê comum).

## Para o online pela internet voltar

O VPS novo precisa rodar o `game/server.mjs` desta rodada na porta 8123, com a porta liberada no firewall. Quem tem acesso ao VPS faz
isso. Até lá, use a REDE LOCAL: um PC hospeda e os outros entram pela rede.

## Build da demonstração

- **Como gerar:** `FBBuild.WindowsDemoDevBatch` (Mono + Development, sem a cena de teste IKTest).
- **Saída:** `F:/FreeBala_builds/Dev_r69/WindowsDemo`.
- **Removido da build:** os glTF dos mapas antigos (Orla e Praça do PT antigas e Estande), que estavam em `Resources` e entravam no executável à toa, e as sobras do pacote de testes de desempenho.
  - Os arquivos foram movidos, não apagados, para `Assets/FreeBala/Excluded_Beta_Assets~` (veja o README dela).
- **Disco C: cheio:** a primeira build falhou por isso. O cache `Library/Bee/Android` (23 GB, de uma build Android de 17/10) foi movido para `F:/FreeBala_builds/_cache_movido_r69/`. A Unity refaz esse cache na próxima build Android.
