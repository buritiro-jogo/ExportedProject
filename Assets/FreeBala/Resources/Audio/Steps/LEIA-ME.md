# Passos por piso — origem e licença

`step_<piso><n>.ogg`, mono 44,1 kHz, Vorbis q4, RMS igualado (a série "terra" do IE vinha ~12 dB mais baixa), pico −1 dBFS.
Gerados por `Fontes~/make_steps.py` (pasta ignorada pelo Unity):

```
python "Fontes~/make_steps.py" <FreeBala/Assets/FreeBala/Resources/Audio> "<_extraido/ie/audio>" .
```

| Piso | Arquivos | Origem | Licença / situação |
|---|---|---|---|
| concreto | 0–3 | Invasão Elite `ie_step_concrete0..3` (já no jogo web/Unity) | APK comercial — **a confirmar pelo usuário** |
| terra | 0–3 | IE `ie_step_dirt0..3` | idem |
| areia | 0–2 | IE `ie_step_sand0..2` | idem |
| lama | 0–3 | IE `ie_step_mud0..3` | idem |
| metal | 0–3 | IE `ie_step_metal0..1` + `_extraido/ie/audio/step-metal*.wav` | idem |
| madeira | 0–3 | IE `ie_step_wood0` + 3 variações de altura/EQ (derivadas) | idem |
| agua | 0–3 | IE `ie_step_water0..1` + `foot-watter*.wav`, cortados em 0,6 s (o web cortava em 0,55 s) | idem |
| asfalto | 0–3 | **derivado**: concreto IE ×0,9 de altura, passa-baixa 3,2 kHz + grão sintetizado | idem (processamento original) |
| grama | 0–3 | **derivado**: terra IE abafada + farfalhar de folhas sintetizado | idem (processamento original) |
| vidro | 0–3 | **derivado**: concreto IE + estalos/tilintar de cacos sintetizados | idem (processamento original) |

O banco de passos do Free Fire (`Player_Footsteep_Walk/Run`) não vem no APK (baixado depois), e o do Blood Strike (`fst_general`) também não.
Se o usuário quiser gravações reais de grama/asfalto/vidro, basta trocar os arquivos (CC0, ex.: BigSoundBank) mantendo os nomes.
