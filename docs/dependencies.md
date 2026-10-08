# Dependency and redistribution record

Review this file and generated NuGet lock/assets data before every public release. It records direct dependencies, not legal advice.

| Dependency | Pinned version | Used by | License | Redistribution note |
| --- | --- | --- | --- | --- |
| Avalonia | 11.3.22 | Avalonia adapter/demo | MIT | Runtime dependencies flow through NuGet |
| Avalonia.Headless | 11.3.22 | Headless verification only | MIT | Development/test dependency; not packed into product packages |
| Avalonia.Themes.Fluent | 11.3.22 | Demo/headless verification | MIT | Demo/test dependency |
| OpenCvSharp4 | 4.11.0.20250507 | Optional OpenCvSharp adapter | Apache-2.0 | Managed wrapper is a dependency of the optional adapter package |
| OpenCvSharp4.runtime.win | 4.11.0.20250507 | Windows demo and integration tests | Apache-2.0 plus native third-party notices | Not a dependency of the adapter package; optional FFmpeg video plugin excluded from the demo ZIP |
| Microsoft.WindowsAppSDK | 2.4.0 | WinUI adapter/sample | MIT | Runtime/framework packaging rules also apply |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | WinUI build | MIT | Private build asset |
| xunit.v3.mtp-v2 | 4.0.0 | Unit/integration tests | Apache-2.0 | Development/test dependency |

## OpenCvSharp version choice

The adapter intentionally pins OpenCvSharp 4.11.0.20250507. The newer 4.13 package available during this release cycle includes an analyzer compiled for Roslyn 4.14, which the supported .NET 8 SDK cannot load. Version 4.11 builds cleanly on the declared toolchain and remains isolated behind the optional adapter.

The adapter package does not select or bundle an operating-system runtime. Applications must reference the appropriate OpenCvSharp runtime package themselves. This avoids imposing Windows native binaries on cross-platform consumers and keeps deployment choice at the application edge.

## Windows demo redistribution

The self-contained Windows ZIP includes .NET 8, Avalonia, SkiaSharp, HarfBuzzSharp, and OpenCV native components. scripts/publish-demo.ps1 copies restored package/runtime license/notice texts and NuGet metadata, fetches missing upstream license texts, and retains license/copyright/notice files from the OpenCV and opencv_contrib 4.11.0 source trees plus the matching Intel IPPICV 2021.12.0 Windows archive referenced by OpenCV. BUILD-INFO.json records source commit, dirty-tree status, versions, source URLs, and source-archive hashes. The collected notices intentionally include some build-only/non-Windows dependencies; they are not a minimal binary inventory.

NATIVE-SMOKE.json records the deployed OpenCV build configuration and successful native presentation after exercising 60 format/source/padding combinations. The upstream runtime reports non-free algorithms enabled; this sample uses Mat storage/copying only and does not invoke those algorithms. Do not assume the optional runtime's entire algorithm catalog has the same licensing/patent scope as this adapter.

SkiaSharp.NativeAssets.Win32 and HarfBuzzSharp.NativeAssets.Win32 carry native THIRD-PARTY-NOTICES texts. Avalonia.Angle.Windows.Natives carries its native LICENSE text. The optional opencv_videoio_ffmpeg4110_64.dll is excluded: this application does not use video acquisition or decoding. Do not add that plugin later without reviewing its distinct redistribution obligations.

Keep LICENSE.txt, licenses/, and BUILD-INFO.json with any redistribution. The demo is unsigned evaluation software. License collection is a release gate, not a legal opinion or production certification.

## Product website typography

The website loads DM Sans and Manrope from Google Fonts. Both are SIL Open Font License 1.1 fonts; they are not included in the SDK packages or self-hosted in this source tree. If fonts are later bundled locally, retain their copyright and OFL notices.

- [DM Sans license](https://github.com/google/fonts/blob/main/ofl/dmsans/OFL.txt)
- [Manrope license](https://github.com/google/fonts/blob/main/ofl/manrope/OFL.txt)

The website privacy notice discloses third-party font requests. Website JavaScript and geometry use browser APIs without additional package dependencies.

## Release check

October 8, 2026: `dotnet list PixelViewport.sln package --vulnerable --include-transitive` reported no known vulnerable packages for any of the 12 projects against the configured feeds. This is a point-in-time advisory check, not a security guarantee.

Before publishing:

1. inspect every generated package with a package explorer;
2. review direct and transitive licenses from restored assets;
3. retain all required copyright/license notices;
4. run a vulnerability/deprecation audit;
5. verify native runtime redistribution terms for each supported platform;
6. update this record when any dependency version changes.
