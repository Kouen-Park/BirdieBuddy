# BirdieBuddy app icon source artwork

The four SVG files in `icon-composer/` are independent, full-size 1024 × 1024 layers, numbered from back to front. Import them into Apple Icon Composer in filename order. The flat composite in `birdiebuddy-icon-preview.svg` is a preview, not an Icon Composer export.

A flat, opaque export of this artwork is bundled as `ios/BirdieBuddyApp/Assets.xcassets/AppIcon.appiconset/AppIcon-1024.png`. The four SVG layers remain separate source files for a future Icon Composer export.

| Layer | Brand token | Color |
| --- | --- | --- |
| Background | `night` | `#0E211F` |
| Golf tee and bird body | `paper` | `#FCFDFB` |
| Wing | `mint` | `#DCEFE8` |

These colors match `wwwroot/css/styles.css` and `ios/BirdieBuddyApp/BirdieTheme.swift`. The bird's eye and the opening below its wing are cutouts in the body path; the background shows through them. The shapes contain no text, strokes, gradients, masks, shadows, or baked lighting. Foreground files have only the shape fill so Icon Composer can treat each as its own layer.

In Icon Composer, a solid `#0E211F` background color is also suitable in place of importing `01-background.svg`. Let Icon Composer apply the platform mask and preview Default, Dark, and Mono appearances at small sizes before exporting the `.icon` file. Avoid adding a rounded-square mask to the SVGs.
