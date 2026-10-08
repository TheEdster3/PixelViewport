param([string]$Version = '0.2.0-alpha.2', [switch]$SkipSmokeTest)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid version' }
$repo = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repo 'artifacts/demo'
$publish = Join-Path $artifacts ('build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $publish -Force | Out-Null
& dotnet publish "$repo/samples/PixelViewport.VisionDemo/PixelViewport.VisionDemo.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Demo publish failed' }
# No video acquisition is used. Do not distribute the optional FFmpeg video plugin.
$videoPlugin = Join-Path $publish 'opencv_videoio_ffmpeg4110_64.dll'
if ((Split-Path -Parent ([IO.Path]::GetFullPath($videoPlugin))) -ne ([IO.Path]::GetFullPath($publish))) { throw 'Unsafe artifact path' }
if (Test-Path -LiteralPath $videoPlugin) { Remove-Item -LiteralPath $videoPlugin }

$notices = Join-Path $publish 'licenses'
New-Item -ItemType Directory -Path $notices | Out-Null
Copy-Item -LiteralPath "$repo/LICENSE" -Destination "$publish/LICENSE.txt"
Copy-Item -LiteralPath "$repo/docs" -Destination "$publish/docs" -Recurse
Copy-Item -LiteralPath "$repo/site" -Destination "$publish/reference" -Recurse
$assets = Get-Content "$repo/samples/PixelViewport.VisionDemo/obj/project.assets.json" -Raw | ConvertFrom-Json
$packageRoots = @($assets.packageFolders.psobject.Properties.Name)
$provenance = [System.Collections.Generic.List[object]]::new()
foreach ($library in $assets.libraries.psobject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    $packagePath = $null
    foreach ($packageRoot in $packageRoots) {
        $candidate = Join-Path $packageRoot $library.Value.path
        if (Test-Path -LiteralPath $candidate) { $packagePath = $candidate; break }
    }
    if (-not $packagePath) { throw "Missing restored package: $($library.Name)" }
    $packageNotices = Join-Path $notices ($library.Name.Replace('/', '-'))
    New-Item -ItemType Directory -Path $packageNotices | Out-Null
    $files = @(Get-ChildItem -LiteralPath $packagePath -File -Recurse | Where-Object { $_.Name -match 'LICENSE|NOTICE|COPYING|COPYRIGHT|\.nuspec$' })
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($packagePath, $file.FullName)
        $target = Join-Path $packageNotices $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    $provenance.Add([ordered]@{ package = $library.Name; notices = @($files.Name); scope = 'Restored dependency graph; may include build-only or non-Windows packages' })
}
# Preserve upstream texts omitted by some NuGet packages, including native notices.
$deps = Get-Content "$publish/PixelViewport.VisionDemo.deps.json" -Raw | ConvertFrom-Json
foreach ($runtime in $deps.libraries.psobject.Properties | Where-Object Name -Like 'runtimepack.*') {
    $runtimeId = $runtime.Name.Substring('runtimepack.'.Length).ToLowerInvariant()
    $runtimePath = $null
    foreach ($packageRoot in $packageRoots) {
        $candidate = Join-Path $packageRoot $runtimeId
        if (Test-Path -LiteralPath $candidate) { $runtimePath = $candidate; break }
    }
    if (-not $runtimePath) { throw "Missing runtime license directory: $runtimeId" }
    $runtimeNotices = Join-Path $notices ($runtimeId.Replace('/', '-'))
    New-Item -ItemType Directory -Path $runtimeNotices | Out-Null
    Get-ChildItem -LiteralPath $runtimePath -File | Where-Object Name -Match 'LICENSE|NOTICE|\.nuspec$' | Copy-Item -Destination $runtimeNotices
    if (-not (Test-Path -LiteralPath (Join-Path $runtimeNotices 'LICENSE.TXT'))) { throw 'Runtime license text missing' }
    $provenance.Add([ordered]@{ package = $runtime.Name; scope = 'Bundled self-contained runtime' })
}
$licenseSources = [ordered]@{
    'Avalonia-LICENSE' = 'https://raw.githubusercontent.com/AvaloniaUI/Avalonia/627ae9ef921621e27e7aa58df2796fbadb50af88/licence.md'
    'MicroCom-LICENSE' = 'https://raw.githubusercontent.com/kekekeks/MicroCom/master/LICENSE'
    'Tmds-DBus-LICENSE' = 'https://raw.githubusercontent.com/tmds/Tmds.DBus/8cb04f66c330b64244e996ff68f685f27f7381a0/COPYING'
    'OpenCvSharp-LICENSE' = 'https://raw.githubusercontent.com/shimat/opencvsharp/main/LICENSE'
    'System-Memory-LICENSE' = 'https://raw.githubusercontent.com/dotnet/maintenance-packages/f62ca0009b038cab4725a720f386623a969d73ad/LICENSE'
}
foreach ($source in $licenseSources.GetEnumerator()) {
    Invoke-WebRequest -Uri $source.Value -OutFile (Join-Path $notices ($source.Key + '.txt'))
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$cache = Join-Path $artifacts 'license-cache'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$nativeSources = [ordered]@{
    'opencv' = 'https://codeload.github.com/opencv/opencv/zip/refs/tags/4.11.0'
    'opencv_contrib' = 'https://codeload.github.com/opencv/opencv_contrib/zip/refs/tags/4.11.0'
    'ippicv' = 'https://raw.githubusercontent.com/opencv/opencv_3rdparty/7f55c0c26be418d494615afca15218566775c725/ippicv/ippicv_2021.12.0_win_intel64_20240425_general.zip'
}
foreach ($nativeSource in $nativeSources.GetEnumerator()) {
    $sourceRepo = $nativeSource.Key
    $archive = Join-Path $cache ($sourceRepo + '-native-notices.zip')
    $url = $nativeSource.Value
    if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest -Uri $url -OutFile $archive }
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    $noticeCount = 0
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.Name -notmatch '^(?:.*[-_])?(?:LICENSE|LICENCE|NOTICE|COPYING|COPYRIGHT|EULA)(?:[._-].*)?$|^third-party-programs\.txt$') { continue }
            $relative = $entry.FullName
            $nativeRoot = [IO.Path]::GetFullPath((Join-Path $notices $sourceRepo))
            $target = [IO.Path]::GetFullPath((Join-Path $nativeRoot $relative))
            if (-not $target.StartsWith($nativeRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe license archive path' }
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            $noticeCount++
        }
    } finally { $zip.Dispose() }
    if ($noticeCount -eq 0) { throw "No native notices collected for $sourceRepo" }
    $provenance.Add([ordered]@{ source = $url; sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash; scope = 'Native license/copyright/notice texts; broader than used modules' })
}
$commit = (& git -C $repo rev-parse HEAD).Trim()
[ordered]@{ version = $Version; sourceCommit = $commit; sourceWorkingTreeDirty = [bool](& git -C $repo status --porcelain); runtime = 'win-x64 / self-contained .NET 8'; signed = $false; ffmpegPlugin = 'Excluded; demo does not use video'; upstreamLicenseSources = $licenseSources; dependencies = $provenance } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$publish/BUILD-INFO.json" -Encoding utf8
@"
PixelViewport Vision Community evaluation / $Version

Extract the entire archive and run PixelViewport.VisionDemo.exe.
Windows x64. .NET runtime included. This is an unsigned prerelease;
respect your organization's security policy and do not bypass a warning blindly.

Formats: Gray8, Gray16LittleEndian, RGB24, BGR24, RGBA32, BGRA32.
Sources: managed lease, safe copy, native lease, OpenCvSharp copy/transfer.
Wheel: anchored zoom. Left drag: pan. Fit / 100%: reset view.
Pause / Step, padded rows, overlays, crosshair, frame statistics, raw inspection.
OpenCV RGB/RGBA requests use BGR/BGRA, as described in docs/demo.md.

Offline reference: reference/docs.html (open in a browser).
Source Markdown: docs/index.md. License texts: licenses/.
BUILD-INFO.json records package and upstream-notice provenance.
No GPU pipeline, camera acquisition, editable ROI, window/level, or Linux claim.
The separate WinUI ImageSource demo is available from the source repository.
https://github.com/TheEdster3/PixelViewport
"@ | Set-Content -LiteralPath "$publish/START-HERE.txt" -Encoding utf8
if (-not $SkipSmokeTest) {
    $process = Start-Process -FilePath "$publish/PixelViewport.VisionDemo.exe" -ArgumentList @('--smoke-test', ('"--smoke-report=' + "$publish/NATIVE-SMOKE.json" + '"')) -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(45000)) { $process.Kill(); throw 'Native presentation timed out' }
    if ($process.ExitCode -ne 0) { throw "Native presentation failed: $($process.ExitCode)" }
    if (-not (Test-Path -LiteralPath "$publish/NATIVE-SMOKE.json")) { throw 'Native smoke report missing' }
}
$archivePath = Join-Path $artifacts 'PixelViewport-VisionDemo-win-x64.zip'
Compress-Archive -Path "$publish/*" -DestinationPath $archivePath -Force -CompressionLevel Optimal
((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($archivePath)) | Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Published and verified Windows evaluation demo: $archivePath"
