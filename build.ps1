# Builds the addon and packages dist\AIInfluenceArmyCommand + a release zip.
#   .\build.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord" [-Install]
param(
    [string]$GameDir = $env:BANNERLORD_DIR,
    [switch]$Install
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $GameDir) { throw "Pass -GameDir or set BANNERLORD_DIR to your Bannerlord install folder." }

$dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } elseif (Test-Path "K:\dotnet\dotnet.exe") { "K:\dotnet\dotnet.exe" } else { throw "dotnet SDK not found" }
& $dotnet build "$root\src\AIInfluenceArmyCommand.csproj" -c Release -p:GameDir="$GameDir" -o "$root\build"
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$version = ([xml](Get-Content "$root\module\SubModule.xml")).Module.Version.value
$dist = "$root\dist\AIInfluenceArmyCommand"
if (Test-Path "$root\dist") { Remove-Item "$root\dist" -Recurse -Force }
New-Item -ItemType Directory -Force "$dist\bin\Win64_Shipping_Client" | Out-Null
Copy-Item "$root\module\*" $dist -Recurse -Force
Copy-Item "$root\build\AIInfluenceArmyCommand.dll" "$dist\bin\Win64_Shipping_Client\"
Copy-Item "$root\README.md", "$root\CHANGELOG.md", "$root\LICENSE" $dist
Compress-Archive -Path $dist -DestinationPath "$root\dist\AIInfluenceArmyCommand-$version.zip" -Force
Write-Host "Packaged: $root\dist\AIInfluenceArmyCommand-$version.zip"

if ($Install) {
    $target = Join-Path $GameDir "Modules\AIInfluenceArmyCommand"
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $dist $target -Recurse
    Write-Host "Installed to $target"
}
