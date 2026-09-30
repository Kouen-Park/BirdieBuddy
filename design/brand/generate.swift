// Run from the repository root: swift design/brand/generate.swift
// Uses the existing bird-on-tee vectors and the bundled, OFL-licensed Outfit.
import Foundation
import CoreText
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

let root = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let fontURL = root.appendingPathComponent("ios/BirdieBuddyApp/Fonts/outfit-600.ttf")
CTFontManagerRegisterFontsForURL(fontURL as CFURL, .process, nil)
let font = CTFontCreateWithName("Outfit-SemiBold" as CFString, 28, nil)
precondition(CTFontCopyPostScriptName(font) as String == "Outfit-SemiBold")
let attributed = NSAttributedString(string: "birdiebuddy", attributes: [
    NSAttributedString.Key(kCTFontAttributeName as String): font,
    NSAttributedString.Key(kCTKernAttributeName as String): -0.6
])
let line = CTLineCreateWithAttributedString(attributed)
let textPath = CGMutablePath()
for run in CTLineGetGlyphRuns(line) as! [CTRun] {
    let count = CTRunGetGlyphCount(run)
    var glyphs = [CGGlyph](repeating: 0, count: count)
    var positions = [CGPoint](repeating: .zero, count: count)
    CTRunGetGlyphs(run, CFRange(location: 0, length: 0), &glyphs)
    CTRunGetPositions(run, CFRange(location: 0, length: 0), &positions)
    for index in 0..<count {
        if let glyph = CTFontCreatePathForGlyph(font, glyphs[index], nil) {
            textPath.addPath(glyph, transform: CGAffineTransform(translationX: positions[index].x, y: positions[index].y))
        }
    }
}
let height: CGFloat = 40
let width = ceil(textPath.boundingBoxOfPath.width + 52)
let bounds = textPath.boundingBoxOfPath
var textTransform = CGAffineTransform(a: 1, b: 0, c: 0, d: -1, tx: 48 - bounds.minX, ty: (height + bounds.height) / 2 + bounds.minY)
let lettering = textPath.copy(using: &textTransform)!

let tee = CGMutablePath()
tee.move(to: CGPoint(x: 450, y: 610))
tee.addQuadCurve(to: CGPoint(x: 574, y: 610), control: CGPoint(x: 512, y: 618))
tee.addLine(to: CGPoint(x: 574, y: 625))
tee.addCurve(to: CGPoint(x: 521, y: 699), control1: CGPoint(x: 541, y: 640), control2: CGPoint(x: 527, y: 667))
for point in [CGPoint(x: 519, y: 795), CGPoint(x: 512, y: 817), CGPoint(x: 505, y: 795), CGPoint(x: 503, y: 699)] { tee.addLine(to: point) }
tee.addCurve(to: CGPoint(x: 450, y: 625), control1: CGPoint(x: 497, y: 667), control2: CGPoint(x: 483, y: 640))
tee.closeSubpath()
let bird = CGMutablePath()
bird.move(to: CGPoint(x: 231, y: 401))
bird.addCurve(to: CGPoint(x: 494, y: 329), control1: CGPoint(x: 330, y: 440), control2: CGPoint(x: 416, y: 414))
bird.addCurve(to: CGPoint(x: 657, y: 218), control1: CGPoint(x: 552, y: 264), control2: CGPoint(x: 603, y: 218))
bird.addCurve(to: CGPoint(x: 763, y: 277), control1: CGPoint(x: 709, y: 218), control2: CGPoint(x: 745, y: 241))
bird.addLine(to: CGPoint(x: 826, y: 288)); bird.addLine(to: CGPoint(x: 765, y: 333))
bird.addCurve(to: CGPoint(x: 713, y: 452), control1: CGPoint(x: 742, y: 351), control2: CGPoint(x: 731, y: 400))
bird.addCurve(to: CGPoint(x: 519, y: 596), control1: CGPoint(x: 681, y: 544), control2: CGPoint(x: 608, y: 594))
bird.addCurve(to: CGPoint(x: 231, y: 401), control1: CGPoint(x: 387, y: 597), control2: CGPoint(x: 286, y: 537))
bird.closeSubpath()
bird.move(to: CGPoint(x: 300, y: 446))
bird.addCurve(to: CGPoint(x: 572, y: 328), control1: CGPoint(x: 416, y: 456), control2: CGPoint(x: 517, y: 405))
bird.addCurve(to: CGPoint(x: 560, y: 484), control1: CGPoint(x: 594, y: 372), control2: CGPoint(x: 593, y: 441))
bird.addCurve(to: CGPoint(x: 419, y: 519), control1: CGPoint(x: 527, y: 528), control2: CGPoint(x: 469, y: 543))
bird.addCurve(to: CGPoint(x: 300, y: 446), control1: CGPoint(x: 363, y: 491), control2: CGPoint(x: 327, y: 465))
bird.closeSubpath(); bird.addEllipse(in: CGRect(x: 659, y: 275, width: 44, height: 44))
let wing = CGMutablePath()
wing.move(to: CGPoint(x: 300, y: 446))
wing.addCurve(to: CGPoint(x: 572, y: 328), control1: CGPoint(x: 416, y: 456), control2: CGPoint(x: 517, y: 405))
wing.addCurve(to: CGPoint(x: 568, y: 435), control1: CGPoint(x: 587, y: 365), control2: CGPoint(x: 588, y: 406))
wing.addCurve(to: CGPoint(x: 457, y: 484), control1: CGPoint(x: 544, y: 470), control2: CGPoint(x: 502, y: 485))
wing.addCurve(to: CGPoint(x: 300, y: 446), control1: CGPoint(x: 399, y: 484), control2: CGPoint(x: 345, y: 465))
wing.closeSubpath()
let badge = CGPath(roundedRect: CGRect(x: 0, y: 2, width: 36, height: 36), cornerWidth: 10, cornerHeight: 10, transform: nil)
let markTransform = CGAffineTransform(a: 36/1024, b: 0, c: 0, d: 36/1024, tx: 0, ty: 2)
var transform = markTransform
let shapes: [(CGPath, String)] = [(badge, "0E211F"), (tee.copy(using: &transform)!, "FCFDFB"), (bird.copy(using: &transform)!, "FCFDFB"), (wing.copy(using: &transform)!, "DCEFE8"), (lettering, "102523")]
func svgPath(_ path: CGPath) -> String {
    var result = ""
    func point(_ p: CGPoint) -> String { String(format: "%.3f %.3f", Double(p.x), Double(p.y)) }
    path.applyWithBlock { pointer in
        let e = pointer.pointee
        switch e.type {
        case .moveToPoint: result += "M" + point(e.points[0])
        case .addLineToPoint: result += "L" + point(e.points[0])
        case .addQuadCurveToPoint: result += "Q" + point(e.points[0]) + " " + point(e.points[1])
        case .addCurveToPoint: result += "C" + point(e.points[0]) + " " + point(e.points[1]) + " " + point(e.points[2])
        case .closeSubpath: result += "Z"
        @unknown default: break
        }
    }
    return result
}
func color(_ hex: String) -> CGColor {
    let value = UInt32(hex, radix: 16)!
    return CGColor(red: CGFloat((value >> 16) & 255)/255, green: CGFloat((value >> 8) & 255)/255, blue: CGFloat(value & 255)/255, alpha: 1)
}
func draw(_ context: CGContext) {
    context.translateBy(x: 0, y: height); context.scaleBy(x: 1, y: -1)
    for (index, shape) in shapes.enumerated() {
        context.setFillColor(color(shape.1)); context.addPath(shape.0)
        context.drawPath(using: index == 2 ? .eoFill : .fill)
    }
}
let fm = FileManager.default
let brand = root.appendingPathComponent("design/brand")
let asset = root.appendingPathComponent("ios/BirdieBuddyApp/Assets.xcassets/BirdieLogo.imageset")
try fm.createDirectory(at: asset, withIntermediateDirectories: true)
for inverted in [false, true] {
    let body = shapes.enumerated().map { index, shape in
        let (path, hex) = shape
        return "<path fill=\"#\(inverted && hex == "102523" ? "FCFDFB" : hex)\" fill-rule=\"\(index == 2 ? "evenodd" : "nonzero")\" d=\"\(svgPath(path))\"/>"
    }.joined(separator: "\n")
    let svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"\(Int(width))\" height=\"40\" viewBox=\"0 0 \(Int(width)) 40\"><title>BirdieBuddy</title>\n\(body)\n</svg>\n"
    let name = inverted ? "birdiebuddy-logo-light.svg" : "birdiebuddy-logo.svg"
    try svg.write(to: brand.appendingPathComponent(name), atomically: true, encoding: .utf8)
    try svg.write(to: root.appendingPathComponent("wwwroot/icons/\(name)"), atomically: true, encoding: .utf8)
}
var mediaBox = CGRect(x: 0, y: 0, width: width, height: height)
let consumer = CGDataConsumer(url: asset.appendingPathComponent("BirdieLogo.pdf") as CFURL)!
let pdf = CGContext(consumer: consumer, mediaBox: &mediaBox, nil)!
pdf.beginPDFPage(nil); draw(pdf); pdf.endPDFPage(); pdf.closePDF()
try """
{"images":[{"filename":"BirdieLogo.pdf","idiom":"universal"}],"info":{"author":"xcode","version":1},"properties":{"preserves-vector-representation":true}}
""".write(to: asset.appendingPathComponent("Contents.json"), atomically: true, encoding: .utf8)
let bitmap = CGContext(data: nil, width: Int(width)*4, height: 160, bitsPerComponent: 8, bytesPerRow: 0, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
bitmap.scaleBy(x: 4, y: 4); draw(bitmap)
let png = CGImageDestinationCreateWithURL(brand.appendingPathComponent("birdiebuddy-logo.png") as CFURL, UTType.png.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(png, bitmap.makeImage()!, nil); CGImageDestinationFinalize(png)
let iconSource = try String(contentsOf: root.appendingPathComponent("design/app-icon/birdiebuddy-icon-preview.svg"), encoding: .utf8)
let favicon = iconSource.replacingOccurrences(of: "<rect width=\"1024\" height=\"1024\"", with: "<rect width=\"1024\" height=\"1024\" rx=\"224\"")
try favicon.write(to: root.appendingPathComponent("wwwroot/icons/birdie-buddy.svg"), atomically: true, encoding: .utf8)
// Opaque, unmasked home-screen icons; the platform supplies its own corner mask.
for (size, name, rounded) in [(32, "favicon-32.png", true), (180, "apple-touch-icon.png", false),
                               (192, "birdiebuddy-192.png", false), (512, "birdiebuddy-512.png", false)] {
    let context = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
        space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    context.translateBy(x: 0, y: CGFloat(size)); context.scaleBy(x: CGFloat(size)/1024, y: -CGFloat(size)/1024)
    context.setFillColor(color("0E211F"))
    context.addPath(CGPath(roundedRect: CGRect(x: 0, y: 0, width: 1024, height: 1024),
        cornerWidth: rounded ? 224 : 0, cornerHeight: rounded ? 224 : 0, transform: nil))
    context.fillPath()
    for (path, hex, mode) in [(tee, "FCFDFB", CGPathDrawingMode.fill),
                              (bird, "FCFDFB", .eoFill), (wing, "DCEFE8", .fill)] {
        context.setFillColor(color(hex)); context.addPath(path); context.drawPath(using: mode)
    }
    let destination = CGImageDestinationCreateWithURL(root.appendingPathComponent("wwwroot/icons/\(name)") as CFURL,
        UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(destination, context.makeImage()!, nil); CGImageDestinationFinalize(destination)
}
print("Generated \(Int(width))×40 outlined logo, vector PDF, transparent PNG, web variants and matching favicon.")
