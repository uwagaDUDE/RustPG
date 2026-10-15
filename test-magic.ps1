$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$buddy = Join-Path (Split-Path $root -Parent) "rustbuddy"
$game = Join-Path (Split-Path $root -Parent) "RustDedicated_Data\Managed"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& (Join-Path $root "build.ps1")
& $csc /nologo "/out:$root\bin\MagicRulesTests.exe" "$root\src\MagicRules.cs" "$root\tests\MagicRulesTests.cs"
if ($LASTEXITCODE -ne 0) { throw "rules test compilation failed" }
& "$root\bin\MagicRulesTests.exe"
if ($LASTEXITCODE -ne 0) { throw "rules checks failed" }
& $csc /nologo /nowarn:1701 "/r:$buddy\lib\Mono.Cecil.dll" "/r:$buddy\lib\netstandard.dll" "/out:$buddy\bin\VerifyMagic.exe" "$root\tools\VerifyMagic.cs"
if ($LASTEXITCODE -ne 0) { throw "binary verifier compilation failed" }
Push-Location (Split-Path $root -Parent)
try {
    & "$buddy\bin\VerifyMagic.exe" "$root\bin\RustPG.dll" $game
    if ($LASTEXITCODE -ne 0) { throw "binary verification failed" }
} finally { Pop-Location }
