# 一键构建：编译插件到 <游戏根目录>\BepInEx\plugins\WSMenuMod\
#
#   .\build.ps1
#   .\build.ps1 -GameDir "D:\steam\steamapps\common\WitchSpring R\"
#
param(
    [string]$GameDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# 默认假设仓库就在游戏根目录下：<游戏根目录>\WitchSpringR-MenuMod\
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $GameDir = Split-Path -Parent $root
}

if (-not (Test-Path (Join-Path $GameDir "BepInEx\core\BepInEx.Core.dll"))) {
    Write-Host "找不到 BepInEx：$GameDir\BepInEx\core\BepInEx.Core.dll" -ForegroundColor Red
    Write-Host "请先装好 BepInEx 6 (IL2CPP)，或用 -GameDir 指定游戏根目录。" -ForegroundColor Yellow
    exit 1
}
if (-not (Test-Path (Join-Path $GameDir "BepInEx\interop\Assembly-CSharp.dll"))) {
    Write-Host "找不到 interop 程序集，请先启动一次游戏让 BepInEx 生成。" -ForegroundColor Yellow
    exit 1
}
if (Get-Process -Name WitchSpringR -ErrorAction SilentlyContinue) {
    Write-Host "游戏正在运行，插件 DLL 被占用，请先退出游戏。" -ForegroundColor Red
    exit 1
}

Write-Host "GameDir = $GameDir"
dotnet build (Join-Path $root "src\MenuMod.csproj") -c Release -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "完成：$GameDir\BepInEx\plugins\WSMenuMod\WSMenuMod.dll" -ForegroundColor Green
