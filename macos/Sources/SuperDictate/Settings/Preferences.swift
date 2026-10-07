import Foundation
import SuperDictateCore

/// The key that starts and stops dictation. Only keys that do nothing on their
/// own qualify, so pressing one never types anything or triggers a shortcut.
enum Hotkey: String, CaseIterable, Identifiable {
    case rightCommand, rightOption, rightControl, function

    var id: Self { self }

    var title: String {
        switch self {
        case .rightCommand: "Right Command ⌘"
        case .rightOption: "Right Option ⌥"
        case .rightControl: "Right Control ⌃"
        case .function: "fn (Globe)"
        }
    }

    /// How hints name it: "Press Right ⌘ to dictate".
    var shortName: String {
        switch self {
        case .rightCommand: "Right ⌘"
        case .rightOption: "Right ⌥"
        case .rightControl: "Right ⌃"
        case .function: "fn"
        }
    }

    var keyCode: Int64 {
        switch self {
        case .rightCommand: 54
        case .rightOption: 61
        case .rightControl: 62
        case .function: 63
        }
    }

    /// The modifier flag that is set only while this very key is down: the right-hand
    /// device bits, so the left key of the same kind doesn't count.
    var deviceFlag: UInt64 {
        switch self {
        case .rightCommand: 0x10
        case .rightOption: 0x40
        case .rightControl: 0x2000
        case .function: 0x800000
        }
    }
}

enum TriggerMode: String, CaseIterable, Identifiable {
    /// Press once to start, again to stop.
    case toggle
    /// Talk while the key is held; letting go stops.
    case hold

    var id: Self { self }

    var title: String {
        switch self {
        case .toggle: "Press to start, press again to stop"
        case .hold: "Hold while talking"
        }
    }
}

/// Everything the user can set, in UserDefaults. The settings window edits these
/// through `@AppStorage` with the same keys; the rest of the app reads them here,
/// at the moment it needs them, so a change applies at once.
enum Preferences {
    enum Key {
        static let hotkey = "dictation.hotkey"
        static let mode = "dictation.mode"
        /// "auto", or the code of the one language to listen for.
        static let language = "dictation.language"
        /// The user's languages, "en,de".
        static let languages = "dictation.languages"
        static let pressReturn = "dictation.pressReturn"
        static let livePreview = "dictation.livePreview"
        static let playSounds = "app.playSounds"
        static let skin = "capsule.skin"
        /// "system", or "#RRGGBB".
        static let accent = "capsule.accent"
        static let scale = "capsule.scale"
        static let opacity = "capsule.opacity"
        static let meter = "capsule.meter"
        static let timer = "capsule.timer"
        /// How wide the capsule may grow, in points at size 1.
        static let maxWidth = "capsule.maxWidth"
        static let placement = "capsule.placement"
        /// "active": the screen the user is working on; "main": always the main screen.
        static let screen = "capsule.screen"
        static let settingsTab = "settings.tab"
    }

    /// Defaults for the switches that start out on.
    static func register() {
        UserDefaults.standard.register(defaults: [Key.livePreview: true, Key.playSounds: true])
    }

    static var hotkey: Hotkey { value(Key.hotkey) ?? .rightCommand }
    static var mode: TriggerMode { value(Key.mode) ?? .toggle }
    static var language: String { UserDefaults.standard.string(forKey: Key.language) ?? "auto" }
    static var languages: [String] { SpokenLanguage.codes(UserDefaults.standard.string(forKey: Key.languages) ?? defaultLanguages) }
    static var pressReturn: Bool { UserDefaults.standard.bool(forKey: Key.pressReturn) }
    static var livePreview: Bool { UserDefaults.standard.bool(forKey: Key.livePreview) }
    static var playSounds: Bool { UserDefaults.standard.bool(forKey: Key.playSounds) }
    static var placement: CapsulePlacement { value(Key.placement) ?? .topCenter }
    static var capsuleOnMainScreen: Bool { UserDefaults.standard.string(forKey: Key.screen) == "main" }
    static var capsuleScale: Double { UserDefaults.standard.object(forKey: Key.scale) as? Double ?? 1 }
    static var capsuleMaxWidth: Double {
        UserDefaults.standard.object(forKey: Key.maxWidth) as? Double ?? Double(CapsuleStyle.widestAtOne)
    }

    /// Until the user picks: the Mac's own languages that the model knows.
    static var defaultLanguages: String {
        SpokenLanguage.defaults(preferred: Locale.preferredLanguages).joined(separator: ",")
    }

    private static func value<T: RawRepresentable>(_ key: String) -> T? where T.RawValue == String {
        UserDefaults.standard.string(forKey: key).flatMap(T.init(rawValue:))
    }
}
