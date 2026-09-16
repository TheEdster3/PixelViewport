$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

dotnet restore "$repo/PixelViewport.sln"
dotnet build "$repo/PixelViewport.sln" -c Release --no-restore
dotnet test "$repo/tests/PixelViewport.Core.Tests/PixelViewport.Core.Tests.csproj" -c Release --no-build
dotnet test "$repo/tests/PixelViewport.Imaging.Tests/PixelViewport.Imaging.Tests.csproj" -c Release --no-build
dotnet test "$repo/tests/PixelViewport.OpenCvSharp.Tests/PixelViewport.OpenCvSharp.Tests.csproj" -c Release --no-build
dotnet run --project "$repo/tests/PixelViewport.Avalonia.Tests/PixelViewport.Avalonia.Tests.csproj" -c Release --no-build
dotnet run --project "$repo/tools/PixelViewport.Benchmarks/PixelViewport.Benchmarks.csproj" -c Release --no-build

Write-Host "PixelViewport verification succeeded: libraries, samples, unit/native tests, headless UI checks, and conversion probe."
