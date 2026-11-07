# Instalador do BURITIRO para jogadores

1. No Unity: **FreeBala > Build > Windows (.exe)** (IL2CPP). Sem o módulo IL2CPP ou o C++ do Visual Studio, use em lote
   `-executeMethod FreeBala.EditorTools.FBBuild.WindowsMonoBatch` (sai em `Builds\WindowsMono`).
2. Dê dois cliques em `Instalador\GERAR_INSTALADOR.bat`. Ele pega a build mais nova, grava o servidor online e gera
   `..\Instaladores\BURITIRO_Instalador_<data>.exe` (instala o Inno Setup sozinho se faltar).
3. Mande o instalador para os jogadores. Se a build passar de 2 GB, saem também arquivos `.bin`: mande todos juntos.

## Trocar o servidor online
- Para as próximas builds: edite a primeira linha de `Assets/StreamingAssets/servidor.txt` (ex.: `ws://IP:8123/ws`).
- Só para um instalador: `GERAR_INSTALADOR.bat -Servidor ws://NOVO_IP:8123/ws`.
- Num jogo já instalado: edite `<pasta do jogo>\Buritiro_Data\StreamingAssets\servidor.txt`, ou digite no campo SERVIDOR do menu.

Quem digitou outro endereço no menu continua com o seu; quem estava no padrão antigo (localhost) passa a usar o do arquivo.
