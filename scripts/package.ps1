$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo "artifacts/packages"

function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}

New-Item -ItemType Directory -Force -Path $output | Out-Null
Get-ChildItem -LiteralPath $output -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in @(".nupkg", ".snupkg") } |
    Remove-Item -Force

& "$PSScriptRoot/verify.ps1"
Invoke-DotNet pack "$repo/src/PixelViewport.Core/PixelViewport.Core.csproj" -c Release -o $output --no-build
Invoke-DotNet pack "$repo/src/PixelViewport.Imaging/PixelViewport.Imaging.csproj" -c Release -o $output --no-build
Invoke-DotNet pack "$repo/src/PixelViewport.OpenCvSharp/PixelViewport.OpenCvSharp.csproj" -c Release -o $output --no-build
Invoke-DotNet pack "$repo/src/PixelViewport.WinUI/PixelViewport.WinUI.csproj" -c Release -o $output --no-build
Invoke-DotNet pack "$repo/src/PixelViewport.Avalonia/PixelViewport.Avalonia.csproj" -c Release -o $output --no-build

Write-Host "Packages written to $output"
