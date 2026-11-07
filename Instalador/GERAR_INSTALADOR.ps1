# Gera o instalador do BURITIRO para os jogadores.
# 1) pega a build mais nova (Builds\Windows = IL2CPP ou Builds\WindowsMono), ou -Build <pasta>
# 2) grava o servidor online em <Jogo>_Data\StreamingAssets\servidor.txt
#    (padrao: primeira linha ws:// de Assets\StreamingAssets\servidor.txt; ou -Servidor ws://IP:8123/ws)
# 3) instala o Inno Setup (gratis) se faltar e gera Instaladores\BURITIRO_Instalador_<data>.exe
param([string]$Build = "", [string]$Servidor = "", [string]$Saida = "")
$ErrorActionPreference = "Stop"
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent $aqui

function Jogo($pasta) {
  # devolve o .exe do jogo (o que tem a pasta <nome>_Data ao lado)
  Get-ChildItem $pasta -Filter *.exe -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -ne "UnityCrashHandler64.exe" -and (Test-Path (Join-Path $pasta ($_.BaseName + "_Data")))
  } | Select-Object -First 1
}

if (-not $Build) {
  $bases = @()
  if ($env:FB_BUILD_DIR) { $bases += $env:FB_BUILD_DIR }
  $bases += (Join-Path $repo "..\Builds"), (Join-Path $repo "..\..\Builds")
  $cands = foreach ($b in $bases) { foreach ($n in "Windows", "WindowsMono") {
    $p = Join-Path $b $n
    if ((Test-Path $p) -and (Jogo $p)) { Get-Item (Resolve-Path $p) } } }
  $esc = $cands | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if (-not $esc) { throw "Nenhuma build encontrada (procurei Builds\Windows e Builds\WindowsMono). Gere a build no Unity: FreeBala > Build > Windows (.exe)." }
  $Build = $esc.FullName
}
$Build = (Resolve-Path $Build).Path
$exe = Jogo $Build
if (-not $exe) { throw "Nao achei o .exe do jogo em $Build" }
Write-Host "Build usada: $Build ($($exe.Name))"

if (-not $Servidor) {
  $cfg = Join-Path $repo "Assets\StreamingAssets\servidor.txt"
  if (Test-Path $cfg) { $Servidor = Get-Content $cfg | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^wss?://' } | Select-Object -First 1 }
}
if (-not $Servidor) { throw "Servidor nao definido. Use -Servidor ws://IP:8123/ws" }
$sa = Join-Path $Build ($exe.BaseName + "_Data\StreamingAssets")
New-Item -ItemType Directory -Force $sa | Out-Null
Set-Content -Path (Join-Path $sa "servidor.txt") -Value $Servidor -Encoding ASCII
Write-Host "Servidor gravado: $Servidor"

$locais = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
$iscc = $locais | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
  Write-Host "Instalando o Inno Setup (gratis)..."
  winget install --id JRSoftware.InnoSetup -e --silent --accept-package-agreements --accept-source-agreements | Out-Host
  $iscc = $locais | Where-Object { Test-Path $_ } | Select-Object -First 1
  if (-not $iscc) { throw "Inno Setup nao instalou. Baixe em https://jrsoftware.org/isdl.php e rode de novo." }
}

$versao = Get-Date -Format "yyyy.MM.dd.HHmm"
if (-not $Saida) { $Saida = Join-Path $repo "..\Instaladores" }
New-Item -ItemType Directory -Force $Saida | Out-Null
$Saida = (Resolve-Path $Saida).Path
$tam = (Get-ChildItem $Build -Recurse -File | Measure-Object Length -Sum).Sum
$args2 = @("/DPasta=$Build", "/DVersao=$versao", "/DSaida=$Saida", "/DExe=$($exe.Name)")
if ($tam -lt 1.9GB) { $args2 += "/DSemDisco=1" }
Write-Host ("Tamanho da build: {0:N0} MB. Compactando (pode levar varios minutos)..." -f ($tam / 1MB))
& $iscc @args2 (Join-Path $aqui "BURITIRO.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC falhou ($LASTEXITCODE)" }
Write-Host ""
Write-Host "PRONTO: $Saida\BURITIRO_Instalador_$versao.exe" -ForegroundColor Green
Get-ChildItem $Saida | Where-Object { $_.Name -like "BURITIRO_Instalador_$versao*" -or ($_.Extension -eq ".bin" -and $_.LastWriteTime -gt (Get-Date).AddHours(-1)) } | ForEach-Object { "{0}  {1:N0} MB" -f $_.Name, ($_.Length / 1MB) }
