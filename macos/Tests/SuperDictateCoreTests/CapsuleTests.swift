import XCTest
#if canImport(CoreGraphics)
import CoreGraphics
#endif
@testable import SuperDictateCore

/// The same cases as the Windows self-test (`capsule.placement`, `capsule.snap`,
/// `capsule.skins`), on a 1920 × 1040 visible area.
final class CapsuleTests: XCTestCase {
    private let area = CGRect(x: 0, y: 0, width: 1920, height: 1040)
    private let small = CGSize(width: 168, height: 48)

    func testPlacementKeepsToAnEdgeAndGrowsAwayFromIt() {
        let top = CapsulePlacement.topCenter.place(small, widest: 460, in: area)
        XCTAssertEqual(top.minX, 876)
        XCTAssertEqual(top.minY, 12)

        // Dropped against the left edge it keeps to the left; right, the right; mid-screen, its centre.
        let left = CapsulePlacement(CGRect(x: 12, y: 400, width: 168, height: 48), in: area)
        XCTAssertEqual(left.horizontal, .start)
        XCTAssertEqual(left.x, 0)
        let right = CapsulePlacement(CGRect(x: 1740, y: 980, width: 168, height: 48), in: area)
        XCTAssertEqual(right.horizontal, .end)
        XCTAssertEqual(right.vertical, .end)
        XCTAssertTrue(right.atBottom)
        let middle = CapsulePlacement(CGRect(x: 860, y: 500, width: 168, height: 48), in: area)
        XCTAssertEqual(middle.horizontal, .center)
        XCTAssertEqual(middle.vertical, .center)

        // A placement puts the capsule back where it was dropped.
        let dropped = CGRect(x: 300, y: 200, width: 168, height: 48)
        let back = CapsulePlacement(dropped, in: area).place(small, widest: 460, in: area)
        XCTAssertEqual(back.minX, dropped.minX, accuracy: 0.01)
        XCTAssertEqual(back.minY, dropped.minY, accuracy: 0.01)

        // Grown to its widest, a right-edge capsule extends to the left and stays on screen.
        let grown = right.place(CGSize(width: 460, height: 48), widest: 460, in: area)
        XCTAssertEqual(grown.maxX, 1908, accuracy: 0.01)
        XCTAssertGreaterThanOrEqual(grown.minX, 12)

        // A centred capsule near an edge is pulled in so its widest still fits.
        let squeezed = CapsulePlacement(horizontal: .center, x: 0.36, vertical: .start, y: 0)
            .place(small, widest: 460, in: CGRect(x: 0, y: 0, width: 800, height: 600))
        XCTAssertGreaterThanOrEqual(squeezed.midX - 230, 12)
    }

    func testDraggedCapsuleSnaps() {
        let nearLeft = CapsulePlacement.snap(CGRect(x: 20, y: 300, width: 168, height: 48), in: area)
        XCTAssertEqual(nearLeft.rect.minX, 12)
        XCTAssertTrue(nearLeft.guides.contains(.left))

        let nearCenter = CapsulePlacement.snap(CGRect(x: 870, y: 300, width: 168, height: 48), in: area)
        XCTAssertEqual(nearCenter.rect.minX, 876)
        XCTAssertTrue(nearCenter.guides.contains(.centerX))

        let offScreen = CapsulePlacement.snap(CGRect(x: -500, y: 2000, width: 168, height: 48), in: area)
        XCTAssertEqual(offScreen.rect.minX, 12)
        XCTAssertEqual(offScreen.rect.maxY, 1028)
    }

    func testPlacementSurvivesStorage() {
        let placement = CapsulePlacement(horizontal: .end, x: 0.75, vertical: .center, y: 0.4)
        XCTAssertEqual(CapsulePlacement(rawValue: placement.rawValue), placement)
        XCTAssertNil(CapsulePlacement(rawValue: "sideways 1 2"))
    }

    func testSkinTextIsReadable() {
        for skin in CapsuleSkin.all {
            for (text, fill) in [(skin.text, skin.fill), (skin.muted, skin.fill), (skin.muted, skin.fillEnd)] {
                XCTAssertGreaterThanOrEqual(RGBA.contrast(text, fill), 4.5, skin.name)
            }
        }
        XCTAssertEqual(Set(CapsuleSkin.all.map(\.id)).count, 12)
        XCTAssertEqual(CapsuleSkin.find("nonsense").id, "midnight")
    }
}

final class SpokenLanguageTests: XCTestCase {
    func testFilterHoldsToOneAlphabet() {
        // One language chosen to listen for: that one.
        XCTAssertEqual(SpokenLanguage.filter(mode: "pl", chosen: ["en", "pl", "ru"]), "pl")
        // Automatic, all Latin: Latin only. Latin and Cyrillic: anything.
        XCTAssertEqual(SpokenLanguage.filter(mode: "auto", chosen: ["en", "de", "pl"]), "en")
        XCTAssertNil(SpokenLanguage.filter(mode: "auto", chosen: ["en", "ru"]))
        XCTAssertEqual(SpokenLanguage.filter(mode: "auto", chosen: ["ru", "uk"]), "ru")
        // A mode that's no longer among the user's languages counts as automatic.
        XCTAssertNil(SpokenLanguage.filter(mode: "ro", chosen: ["en", "ru"]))
    }

    func testStoredAndDefaultLanguages() {
        XCTAssertEqual(SpokenLanguage.codes("ru, en,xx,de"), ["en", "de", "ru"])
        XCTAssertEqual(SpokenLanguage.defaults(preferred: ["ru-RU", "en-US", "ja-JP"]), ["en", "ru"])
        XCTAssertEqual(SpokenLanguage.defaults(preferred: ["ja-JP"]), ["en"])
        XCTAssertEqual(SpokenLanguage.all.count, 25)
    }
}
