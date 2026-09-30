# BirdieBuddy brand assets

The logo combines the existing bird-on-tee symbol with a lowercase Outfit SemiBold wordmark. Lettering is outlined, so the web and iOS versions have identical shapes without loading a font. The master art is 197 × 40 with a transparent background.

- `birdiebuddy-logo.svg`: dark lettering for light surfaces.
- `birdiebuddy-logo-light.svg`: light lettering for the web sidebar.
- `birdiebuddy-logo.png`: transparent 4× export for pasting into documents or presentations.
- `preview.html`: preview light/dark surfaces and navigation sizes.
- `generate.swift`: reproducible CoreText/CoreGraphics export script.

Run `swift -module-cache-path /tmp/birdie-brand-module-cache design/brand/generate.swift` from the repository root. It writes the masters here, the web assets under `wwwroot/icons/`, and the vector PDF in `ios/BirdieBuddyApp/Assets.xcassets/BirdieLogo.imageset/`. Keep the 197:40 aspect ratio in `BirdieBrand` aligned if changing the wordmark.

The SVG masters and native PDF contain paths, with no embedded fonts, linked images, gradients, shadows or external resources. The native catalog preserves the vector representation. `BirdieBrand` supplies the VoiceOver label; web images provide alt text.

The existing native app icon is the symbol reference. The matching favicon SVG has rounded corners; 180/192/512 PNG home-screen exports are opaque and unmasked so the platform applies its own mask. The 32px favicon PNG supplies a raster fallback. Web pages link both favicons and the Apple touch icon; the manifest uses 192px and 512px PNGs.

Outfit is bundled under the SIL Open Font License; see `ios/BirdieBuddyApp/Fonts/LICENSE-Outfit.txt`. Bird vectors come from `design/app-icon/`. Bright mint is used on the wing; the mark uses night (`#0E211F`), paper (`#FCFDFB`) and ink (`#102523`).
