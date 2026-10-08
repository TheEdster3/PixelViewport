# Release and publishing

## Public source and website

- Repository: https://github.com/TheEdster3/PixelViewport
- Product website: https://pixelviewport-vision.glassyloach1.chatgpt.site/
- `site/` mirrors the dedicated Sites checkout. Hosting uses Sites, not GitHub Pages: Pages is not intended for commercial storefronts.
- Website publishing is independent of SDK tags; no Azure resources or paid domain are required.

## Windows verification and alpha release

Run `scripts/verify.ps1` and `scripts/package.ps1` with the .NET 8 SDK. The scripts fail on failed native commands and verify before packing five MIT libraries.

Tag the exact verified version, such as `v0.2.0-alpha.1`, and push the tag. `release.yml` builds/tests/packs and attaches packages and symbols to a prerelease. This does **not** imply NuGet publication.

## NuGet — account setup required

The manually dispatched `nuget.yml` workflow uses [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing). No long-lived API key is stored in the repository.

1. Sign into or create a NuGet.org account with a Microsoft account. Complete required account verification yourself.
2. Check the five IDs: PixelViewport.Core, PixelViewport.Imaging, PixelViewport.OpenCvSharp, PixelViewport.WinUI, PixelViewport.Avalonia. Availability is not a reservation.
3. Add a NuGet trusted publishing policy: repository owner `TheEdster3`, repository `PixelViewport`, workflow filename `nuget.yml` (not the full path). Leave environment empty. Restrict package scope to `PixelViewport.*`; permit new-package publishing for the first release.
4. Set GitHub Actions variable `NUGET_USER` to your NuGet **profile username**, not your email or presumed GitHub username.
5. Dispatch **Publish NuGet** with an existing tagged version. It checks the tag, rebuilds/tests, checks filenames, obtains a temporary credential, and pushes packages with paired symbols.
6. Verify all five listings are indexed and inspect metadata. Only then claim NuGet availability.

The host application still selects its native OpenCV runtime.

## Commercial launch boundaries

- Community alpha is MIT; private Pro source is not in this repository.
- Pilot scope and availability must be agreed before charging.
- Professional pricing is a target, not a purchasable production release.
- No checkout, merchant account, support mailbox, or custom domain is configured yet.
- Evaluation issues are public and must contain no confidential data.
- Windows verified; Linux/embedded validation, GPU/zero-copy rendering, and end-to-end claims remain open.

Before a paid production release, confirm license/support terms, merchant onboarding, private package delivery, and customer acceptance. Do not build an activation server for an alpha.
