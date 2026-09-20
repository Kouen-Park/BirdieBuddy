import SwiftUI
import UIKit
import XCTest
@testable import BirdieBuddyApp

/// The scorecard has to stay readable on the narrowest supported iPhone and at
/// accessibility type sizes. These tests render the real view and compare its
/// geometry, so removing the accessibility-size branch fails the suite instead of
/// shipping a clipped scorecard that only a physical device would reveal.
@MainActor
final class ScorecardLayoutTests: XCTestCase {
    /// An iPhone SE is 375pt wide; a grouped list insets its rows by 16pt a side.
    private let narrowContentWidth: CGFloat = 343

    private func renderedSize(typeSize: DynamicTypeSize) -> CGSize {
        // A deliberately busy hole: a penalty stroke and a two-digit score are the
        // widest the row ever gets.
        let hole = Hole(
            id: 1,
            holeNumber: 18,
            par: 5,
            score: 10,
            putts: 3,
            gir: true,
            fairwayHit: true,
            penalty: 2)

        let view = HoleRow(hole: hole)
            .environment(\.dynamicTypeSize, typeSize)
            .frame(width: narrowContentWidth)

        let renderer = ImageRenderer(content: view)
        renderer.scale = 1
        return renderer.uiImage?.size ?? .zero
    }

    func testRowAdaptsAtAccessibilityTypeSizes() {
        let compact = renderedSize(typeSize: .large)
        let accessible = renderedSize(typeSize: .accessibility3)

        XCTAssertGreaterThan(compact.height, 0, "The row rendered nothing at the default type size.")
        XCTAssertEqual(compact.width, narrowContentWidth, accuracy: 1)
        XCTAssertEqual(accessible.width, narrowContentWidth, accuracy: 1)

        // One line cannot hold hole, par, score, to-par, putts and three badges at
        // accessibility sizes, so the row becomes a stacked block and grows taller.
        XCTAssertGreaterThan(
            accessible.height,
            compact.height,
            "HoleRow did not adapt at accessibility type sizes; the single-line layout would clip.")
    }

    func testRowRendersAtEveryTypeSize() {
        for size in DynamicTypeSize.allCases {
            let rendered = renderedSize(typeSize: size)
            XCTAssertGreaterThan(rendered.height, 0, "HoleRow rendered nothing at \(size).")
            XCTAssertEqual(
                rendered.width,
                narrowContentWidth,
                accuracy: 1,
                "HoleRow demanded more than the narrow content width at \(size).")
        }
    }
}
