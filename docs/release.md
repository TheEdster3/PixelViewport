# Release and hosting

## Configure the repository

Create a public repository named PixelViewport, then run:

    ./scripts/configure-product.ps1 -GitHubOwner <owner> -DesignPartnerUrl <intake-url>

The intake URL is optional and remains disabled on the site when omitted. Review the resulting diff before publishing.

## GitHub Pages

The included GitHub Pages workflow deploys site/. In repository settings, set Pages source to **GitHub Actions**. A custom domain can be added later.

## NuGet

1. Reserve/verify PixelViewport.Core, PixelViewport.Imaging, PixelViewport.OpenCvSharp, PixelViewport.WinUI, and PixelViewport.Avalonia.
2. Replace every OWNER placeholder and verify repository/project URLs.
3. Create a NuGet API key scoped to those IDs and save it as the NUGET_API_KEY Actions secret.
4. Run ./scripts/verify.ps1 and ./scripts/package.ps1.
5. Consume the packages from artifacts/packages in clean sample projects.
6. Tag the same alpha version across the package set, such as v0.2.0-alpha.1.

The release workflow builds every project, runs unit/native/headless verification, packs all five libraries at the tag version, publishes when the key exists, and attaches packages to the GitHub release.

## Alpha release checklist

- Replace OWNER placeholders and configure the intake URL.
- Confirm names and package IDs are available.
- Review dependency licenses and commit notices if required.
- Test the WinUI and Avalonia samples on supported Windows hardware.
- Test Avalonia on a named Linux environment before claiming Linux support.
- Record benchmark hardware, workload, median/p95/p99, allocation, and measurement boundary.
- Validate high-DPI, keyboard, pointer, shutdown, and buffer-lifetime behavior.
- Enable branch protection and required CI.
- Publish as prerelease; do not enable self-serve Professional checkout.
- Route qualified evaluations through the design-partner process.
