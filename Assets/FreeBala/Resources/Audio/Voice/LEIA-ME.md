# Vozes de dano — origem e licença de cada arquivo

Todos os `.ogg` desta pasta são **mono, 44,1 kHz, Vorbis q5**, normalizados por categoria (RMS da parte ativa: dor 0,16; gemido/esforço 0,08–0,10;
gás 0,12) com pico limitado em −1 dBFS. São gerados por `Fontes~/make_voice.py` (pasta com `~` = o Unity ignora):

```
python "Fontes~/make_voice.py" <tools/re/out/sg/wwise/vo_wav> <FreeBala/Assets/FreeBala/Resources/Audio> . "<_extraido/bloodstrike/audio/esm_skills_gas_cough>"
```

Mapeamento de bancos/personagens: `voice_profiles.json` (lido por `FBVoiceProfiles`).

| Arquivos | Origem | Licença / situação |
|---|---|---|
| `m_dor_leve0..2`, `m_dor_grave0..2` | Free Fire (APK Sigma), Wwise `Character_Player_Vo`, evento `Player_Vo_Injured_Male` (mídias 841940804, 375934363, 801025215 / 852962676, 1044312679, 193400186), decodificado com vgmstream | **Extraído de APK comercial — direito de uso a confirmar pelo usuário** (mesma situação dos `sg_*` já usados no jogo) |
| `f_dor_leve0..2`, `f_dor_grave0..2` | idem, evento `Player_Vo_Injured_Female` (762049211, 824634407, 854314261 / 276564287, 256449024, 317321099) | idem |
| `f_gemido0..1` | `f_dor_leve0..1` mais graves (×0,9) com cauda (derivado) | idem |
| `m_esforco0..1`, `f_esforco0..1` | Free Fire, `Player_Vo_GetOver_Male` (593398063, 142471590) / `_Female` (88958359, 699378459) | idem |
| `m_dor_leve3..4`, `m_dor_grave3`, `m_gemido0..1` | Invasão Elite (APK): `ie_hit1`, `ie_hit2`, `ie_hit0`, `ie_injured0..1` (já no jogo) | **APK comercial — a confirmar pelo usuário** |
| `m_morte0..2`, `f_morte0..2` | dor longa do Sigma/IE ×0,94 + último suspiro **sintetizado** (derivado) | idem (parte sintetizada é original) |
| `m_gas_tosse0..3`, `f_gas_tosse0..3`, `*_gas_irrita*`, `*_gas_resp*`, `*_gas_fraca*`, `*_gas_morte*` | **Sintetizados** em `make_voice.py` (fonte glotal + ruído de aspiração em formantes masc./fem.) | Originais do projeto (livres) |
| `m_gas_tosse4..6`, `f_gas_tosse4..6` | Blood Strike (APK NetEase), banco `esm_skills_gas_cough` (20836838, 81091848, 905472291); quase sem voz → mesma tosse com altura ×0,97 (m) / ×1,1 (f) | **APK comercial — a confirmar pelo usuário** |

Observações
- O `tools/re/sg_wwise.py` junta contêineres pelo ID do pai e **mistura os dois sexos** (os nomes `Player_Vo_GetOver_Female_N.wav` em
  `_extraido/sigma/audio/` não indicam o sexo). A separação acima foi feita lendo a árvore Switch → RanSeq → Sound de cada evento.
  Os 4 eventos têm 20+ mídias, mas só 16 estão embutidas no APK (as demais são baixadas depois).
- Não há tosse/respiração gravada masculina × feminina nos APKs extraídos; se o usuário aprovar, substituir as sintetizadas por gravações
  CC0 (ex.: BigSoundBank, já usado no ambiente) — basta trocar os nomes no `voice_profiles.json`.
