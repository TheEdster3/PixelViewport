$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo "artifacts/packages"

New-Item -ItemType Directory -Force -Path $output | Out-Null
Get-ChildItem -LiteralPath $output -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in @(".nupkg", ".snupkg") } |
    Remove-Item -Force

dotnet restore "$repo/PixelViewport.sln"
dotnet build "$repo/PixelViewport.sln" -c Release --no-restore
dotnet test "$repo/tests/PixelViewport.Core.Tests/PixelViewport.Core.Tests.csproj" -c Release --no-build
dotnet test "$repo/tests/PixelViewport.Imaging.Tests/PixelViewport.Imaging.Tests.csproj" -c Release --no-build
dotnet test "$repo/tests/PixelViewport.OpenCvSharp.Tests/PixelViewport.OpenCvSharp.Tests.csproj" -c Release --no-build
dotnet run --project "$repo/tests/PixelViewport.Avalonia.Tests/PixelViewport.Avalonia.Tests.csproj" -c Release --no-build
dotnet pack "$repo/src/PixelViewport.Core/PixelViewport.Core.csproj" -c Release -o $output --no-build
dotnet pack "$repo/src/PixelViewport.Imaging/PixelViewport.Imaging.csproj" -c Release -o $output --no-build
dotnet pack "$repo/src/PixelViewport.OpenCvSharp/PixelViewport.OpenCvSharp.csproj" -c Release -o $output --no-build
dotnet pack "$repo/src/PixelViewport.WinUI/PixelViewport.WinUI.csproj" -c Release -o $output --no-build
dotnet pack "$repo/src/PixelViewport.Avalonia/PixelViewport.Avalonia.csproj" -c Release -o $output --no-build

Write-Host "Packages written to $output"
