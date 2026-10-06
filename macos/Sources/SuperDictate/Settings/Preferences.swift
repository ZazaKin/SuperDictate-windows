import Foundation

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
        static let language = "dictation.language"
        static let pressReturn = "dictation.pressReturn"
        static let livePreview = "dictation.livePreview"
        static let playSounds = "app.playSounds"
    }

    /// Defaults for the switches that start out on.
    static func register() {
        UserDefaults.standard.register(defaults: [Key.livePreview: true, Key.playSounds: true])
    }

    static var hotkey: Hotkey { value(Key.hotkey) ?? .rightCommand }
    static var mode: TriggerMode { value(Key.mode) ?? .toggle }
    static var language: DictationLanguage { value(Key.language) ?? .auto }
    static var pressReturn: Bool { UserDefaults.standard.bool(forKey: Key.pressReturn) }
    static var livePreview: Bool { UserDefaults.standard.bool(forKey: Key.livePreview) }
    static var playSounds: Bool { UserDefaults.standard.bool(forKey: Key.playSounds) }

    private static func value<T: RawRepresentable>(_ key: String) -> T? where T.RawValue == String {
        UserDefaults.standard.string(forKey: key).flatMap(T.init(rawValue:))
    }
}
