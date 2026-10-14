param(
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$root = $PSScriptRoot
$src = Join-Path $root "src"
$out = Join-Path $root "bin"
$rustbuddy = Join-Path (Split-Path $root -Parent) "rustbuddy"
$game = Join-Path (Split-Path $root -Parent) "RustDedicated_Data\Managed"
$pluginsDir = Join-Path $rustbuddy "plugins"

New-Item -ItemType Directory -Force -Path $out | Out-Null

$refs = @(
    "/r:$rustbuddy\lib\0Harmony.dll",
    "/r:$rustbuddy\lib\netstandard.dll",
    "/r:$rustbuddy\bin\RustBuddyLoader.dll",
    "/r:$game\Assembly-CSharp.dll",
    "/r:$game\Rust.Data.dll",
    "/r:$game\Facepunch.UnityEngine.dll",
    "/r:$game\Facepunch.Network.dll",
    "/r:$game\Facepunch.Console.dll",
    "/r:$game\Facepunch.System.dll",
    "/r:$game\Rust.Global.dll",
    "/r:$game\UnityEngine.CoreModule.dll",
    "/r:$game\UnityEngine.AnimationModule.dll",
    "/r:$game\UnityEngine.dll",
    "/r:$game\UnityEngine.UI.dll",
    "/r:$game\Newtonsoft.Json.dll"
)

$files = Get-ChildItem "$src\*.cs" | ForEach-Object { $_.FullName }

& $csc /nologo /nowarn:1701,1684 /target:library /out:"$out\RustPG.dll" @refs $files
if ($LASTEXITCODE -ne 0) { throw "csc failed" }
Write-Host "built bin\RustPG.dll"

if ($Deploy) {
    Copy-Item "$out\RustPG.dll" "$pluginsDir\RustPG.dll" -Force
    Write-Host "deployed to $pluginsDir\RustPG.dll"
}

Write-Host "build OK"
