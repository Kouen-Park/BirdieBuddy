# BirdieBuddy brand assets

The logo combines the existing bird-on-tee symbol with a lowercase Outfit SemiBold wordmark. Lettering is outlined, so the web and iOS versions have identical shapes without loading a font. The master art is 197 × 40 with a transparent background.

- `birdiebuddy-logo.svg`: dark lettering for light surfaces.
- `birdiebuddy-logo-light.svg`: light lettering for the web sidebar.
- `birdiebuddy-logo.png`: transparent 4× export for pasting into documents or presentations.
- `birdiebuddy-logo-preview.png`: paper-background visual review image; keep the transparent PNG/SVG as reusable masters.
- `preview.html`: preview light/dark surfaces and navigation sizes.
- `generate.swift`: reproducible CoreText/CoreGraphics export script.

Run `swift -module-cache-path /tmp/birdie-brand-module-cache design/brand/generate.swift` from the repository root. It writes the masters here, the web assets under `wwwroot/icons/`, and the vector PDF in `ios/BirdieBuddyApp/Assets.xcassets/BirdieLogo.imageset/`. Keep the 197:40 aspect ratio in `BirdieBrand` aligned if changing the wordmark.

The transparent PNG is 788 × 160 (4×); the native PDF and both SVG variants are 197 × 40. The separate paper-background preview is a review artifact, not produced by `generate.swift`. Run the generator on macOS with the bundled Outfit font available; it uses CoreText/CoreGraphics and does not need an image service.

The SVG masters and native PDF contain paths, with no embedded fonts, linked images, gradients, shadows or external resources. The native catalog preserves the vector representation. `BirdieBrand` supplies the VoiceOver label; web images provide alt text.

The existing native app icon is the symbol reference. The matching favicon SVG has rounded corners; 180/192/512 PNG home-screen exports are opaque and unmasked so the platform applies its own mask. The 32px favicon PNG supplies a raster fallback. Web pages link both favicons and the Apple touch icon; the manifest uses 192px and 512px PNGs.

## Where the assets are used

| Surface | Asset |
| --- | --- |
| iOS navigation/login/session restoration | `BirdieLogo.imageset/BirdieLogo.pdf`, via `BirdieBrand` (default navigation height 30pt) |
| Web mobile header and authentication | `wwwroot/icons/birdiebuddy-logo.svg` |
| Web sidebar | `wwwroot/icons/birdiebuddy-logo-light.svg` |
| Browser favicon | `wwwroot/icons/birdie-buddy.svg` and `favicon-32.png` |
| Apple home-screen icon | `wwwroot/icons/apple-touch-icon.png` (180px) |
| PWA manifest icons | `wwwroot/icons/birdiebuddy-192.png`, `birdiebuddy-512.png` |
| Installed native app icon | Existing `AppIcon.appiconset/AppIcon-1024.png`; the wordmark generator does not replace it |

The native PDF uses dark lettering on a light surface; the light-lettering SVG is for the dark web sidebar. Appearance variants of the app icon remain future [Icon Composer work](../app-icon/README.md). The web service worker precaches the wordmarks/icons in `birdiebuddy-shell-v5`.

Branding merged in [PR #15](https://github.com/Kouen-Park/BirdieBuddy/pull/15). See [the design audit](../ios-audit/README.md) for export review, CI results, and outstanding native screen checks.

Outfit is bundled under the SIL Open Font License; see [its license](../../ios/BirdieBuddyApp/Fonts/LICENSE-Outfit.txt). Bird vectors come from `design/app-icon/`. Mint (`#DCEFE8`) is used on the wing; the mark uses night (`#0E211F`), paper (`#FCFDFB`) and ink (`#102523`).
