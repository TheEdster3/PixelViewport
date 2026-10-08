$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}

Invoke-DotNet restore "$repo/PixelViewport.sln"
Invoke-DotNet build "$repo/PixelViewport.sln" -c Release --no-restore
Invoke-DotNet test "$repo/tests/PixelViewport.Core.Tests/PixelViewport.Core.Tests.csproj" -c Release --no-build
Invoke-DotNet test "$repo/tests/PixelViewport.Imaging.Tests/PixelViewport.Imaging.Tests.csproj" -c Release --no-build
Invoke-DotNet test "$repo/tests/PixelViewport.OpenCvSharp.Tests/PixelViewport.OpenCvSharp.Tests.csproj" -c Release --no-build
Invoke-DotNet run --project "$repo/tests/PixelViewport.Avalonia.Tests/PixelViewport.Avalonia.Tests.csproj" -c Release --no-build
Invoke-DotNet run --project "$repo/tools/PixelViewport.Benchmarks/PixelViewport.Benchmarks.csproj" -c Release --no-build

Write-Host "PixelViewport verification succeeded: libraries, samples, unit/native tests, headless UI checks, and conversion probe."
