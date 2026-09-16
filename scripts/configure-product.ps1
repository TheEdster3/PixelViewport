param(
    [Parameter(Mandatory = $true)]
    [string]$GitHubOwner,

    [string]$DesignPartnerUrl = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$textFiles = @(
    "README.md",
    "docs/release.md",
    "src/PixelViewport.Core/PixelViewport.Core.csproj",
    "src/PixelViewport.Imaging/PixelViewport.Imaging.csproj",
    "src/PixelViewport.OpenCvSharp/PixelViewport.OpenCvSharp.csproj",
    "src/PixelViewport.WinUI/PixelViewport.WinUI.csproj",
    "src/PixelViewport.Avalonia/PixelViewport.Avalonia.csproj",
    "site/index.html",
    "site/app.js"
)

foreach ($relativePath in $textFiles) {
    $path = Join-Path $root $relativePath
    $content = Get-Content -Raw $path
    $content = $content.Replace("OWNER", $GitHubOwner)
    Set-Content -Path $path -Value $content -NoNewline
}

$appJsPath = Join-Path $root "site/app.js"
$appJs = Get-Content -Raw $appJsPath
$appJs = $appJs -replace 'designPartnerUrl:\s*"[^"]*"', ('designPartnerUrl: "' + $DesignPartnerUrl + '"')
Set-Content -Path $appJsPath -Value $appJs -NoNewline

Write-Host "Configured PixelViewport for GitHub owner '$GitHubOwner'."
if ([string]::IsNullOrWhiteSpace($DesignPartnerUrl)) {
    Write-Host "The design-partner intake URL is empty; the intake button will remain disabled."
}
