# Dependency and redistribution record

Review this file and generated NuGet lock/assets data before every public release. It records direct dependencies, not legal advice.

| Dependency | Pinned version | Used by | License | Redistribution note |
| --- | --- | --- | --- | --- |
| Avalonia | 11.3.22 | Avalonia adapter/demo | MIT | Runtime dependencies flow through NuGet |
| Avalonia.Headless | 11.3.22 | Headless verification only | MIT | Development/test dependency; not packed into product packages |
| Avalonia.Themes.Fluent | 11.3.22 | Demo/headless verification | MIT | Demo/test dependency |
| OpenCvSharp4 | 4.11.0.20250507 | Optional OpenCvSharp adapter | Apache-2.0 | Managed wrapper is a dependency of the optional adapter package |
| OpenCvSharp4.runtime.win | 4.11.0.20250507 | Windows integration tests only | Apache-2.0 | Native test dependency; not a dependency of the adapter package |
| Microsoft.WindowsAppSDK | 2.4.0 | WinUI adapter/sample | MIT | Runtime/framework packaging rules also apply |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | WinUI build | MIT | Private build asset |
| xunit.v3.mtp-v2 | 4.0.0 | Unit/integration tests | Apache-2.0 | Development/test dependency |

## OpenCvSharp version choice

The adapter intentionally pins OpenCvSharp 4.11.0.20250507. The newer 4.13 package available during this release cycle includes an analyzer compiled for Roslyn 4.14, which the supported .NET 8 SDK cannot load. Version 4.11 builds cleanly on the declared toolchain and remains isolated behind the optional adapter.

The adapter package does not select or bundle an operating-system runtime. Applications must reference the appropriate OpenCvSharp runtime package themselves. This avoids imposing Windows native binaries on cross-platform consumers and keeps deployment choice at the application edge.

## Release check

Before publishing:

1. inspect every generated package with a package explorer;
2. review direct and transitive licenses from restored assets;
3. retain all required copyright/license notices;
4. run a vulnerability/deprecation audit;
5. verify native runtime redistribution terms for each supported platform;
6. update this record when any dependency version changes.
