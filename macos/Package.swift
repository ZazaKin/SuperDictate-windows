// swift-tools-version: 5.10
//
// SuperDictate for macOS: a menu bar dictation app for Apple Silicon.
// SwiftUI and AppKit, with Parakeet TDT v3 speech recognition running on the
// Apple Neural Engine through FluidAudio. macOS 14 (Sonoma) or newer.
//
// SuperDictateCore holds the logic that needs no Mac frameworks (the rules of
// a dictation, the live preview, voice detection, the caption model), so its tests run anywhere
// Swift does. The app target adds audio, speech, input and the interface.
import PackageDescription

let package = Package(
    name: "SuperDictate",
    platforms: [
        .macOS(.v14),
    ],
    products: [
        .executable(name: "SuperDictate", targets: ["SuperDictate"]),
    ],
    dependencies: [
        // The revision the original Mac app ships with.
        .package(url: "https://github.com/FluidInference/FluidAudio.git",
                 revision: "313feb4bd692780a9a5b5fa9048fdb119486dde8"),
    ],
    targets: [
        .target(name: "SuperDictateCore"),
        .executableTarget(
            name: "SuperDictate",
            dependencies: [
                "SuperDictateCore",
                .product(name: "FluidAudio", package: "FluidAudio"),
            ]
        ),
        .testTarget(name: "SuperDictateCoreTests", dependencies: ["SuperDictateCore"]),
    ]
)
