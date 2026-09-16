# Contributing

Thanks for considering a contribution.

1. Keep the public API small and imaging-domain-neutral.
2. Put coordinate/viewport behavior in `PixelViewport.Core` and frame/pixel behavior in `PixelViewport.Imaging` when it does not require a UI framework.
3. Add tests for geometry, frame ownership, conversion, queue, or transform changes.
4. Avoid camera-vendor and application-specific concepts in public contracts.
5. Run `./scripts/verify.ps1` before opening a pull request.

For substantial API changes, open an issue first so compatibility can be discussed before implementation.
