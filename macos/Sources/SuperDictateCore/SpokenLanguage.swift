import Foundation

/// A language Parakeet TDT v3 recognizes. It knows 25 European languages and
/// tells them apart by itself; what the app can add is an alphabet: naming a
/// language keeps letters of other alphabets out of short, unclear phrases.
public struct SpokenLanguage: Identifiable, Equatable, Sendable {
    public enum Script: Sendable {
        case latin, cyrillic, greek
    }

    public let code: String
    public let native: String
    public let english: String
    public let flag: String
    public let script: Script

    public var id: String { code }

    /// Most widely spoken first.
    public static let all = [
        SpokenLanguage(code: "en", native: "English", english: "English", flag: "🇬🇧", script: .latin),
        SpokenLanguage(code: "es", native: "Español", english: "Spanish", flag: "🇪🇸", script: .latin),
        SpokenLanguage(code: "fr", native: "Français", english: "French", flag: "🇫🇷", script: .latin),
        SpokenLanguage(code: "de", native: "Deutsch", english: "German", flag: "🇩🇪", script: .latin),
        SpokenLanguage(code: "it", native: "Italiano", english: "Italian", flag: "🇮🇹", script: .latin),
        SpokenLanguage(code: "pt", native: "Português", english: "Portuguese", flag: "🇵🇹", script: .latin),
        SpokenLanguage(code: "nl", native: "Nederlands", english: "Dutch", flag: "🇳🇱", script: .latin),
        SpokenLanguage(code: "pl", native: "Polski", english: "Polish", flag: "🇵🇱", script: .latin),
        SpokenLanguage(code: "ru", native: "Русский", english: "Russian", flag: "🇷🇺", script: .cyrillic),
        SpokenLanguage(code: "uk", native: "Українська", english: "Ukrainian", flag: "🇺🇦", script: .cyrillic),
        SpokenLanguage(code: "cs", native: "Čeština", english: "Czech", flag: "🇨🇿", script: .latin),
        SpokenLanguage(code: "sv", native: "Svenska", english: "Swedish", flag: "🇸🇪", script: .latin),
        SpokenLanguage(code: "el", native: "Ελληνικά", english: "Greek", flag: "🇬🇷", script: .greek),
        SpokenLanguage(code: "ro", native: "Română", english: "Romanian", flag: "🇷🇴", script: .latin),
        SpokenLanguage(code: "hu", native: "Magyar", english: "Hungarian", flag: "🇭🇺", script: .latin),
        SpokenLanguage(code: "bg", native: "Български", english: "Bulgarian", flag: "🇧🇬", script: .cyrillic),
        SpokenLanguage(code: "da", native: "Dansk", english: "Danish", flag: "🇩🇰", script: .latin),
        SpokenLanguage(code: "fi", native: "Suomi", english: "Finnish", flag: "🇫🇮", script: .latin),
        SpokenLanguage(code: "sk", native: "Slovenčina", english: "Slovak", flag: "🇸🇰", script: .latin),
        SpokenLanguage(code: "hr", native: "Hrvatski", english: "Croatian", flag: "🇭🇷", script: .latin),
        SpokenLanguage(code: "lt", native: "Lietuvių", english: "Lithuanian", flag: "🇱🇹", script: .latin),
        SpokenLanguage(code: "sl", native: "Slovenščina", english: "Slovenian", flag: "🇸🇮", script: .latin),
        SpokenLanguage(code: "lv", native: "Latviešu", english: "Latvian", flag: "🇱🇻", script: .latin),
        SpokenLanguage(code: "et", native: "Eesti", english: "Estonian", flag: "🇪🇪", script: .latin),
        SpokenLanguage(code: "mt", native: "Malti", english: "Maltese", flag: "🇲🇹", script: .latin),
    ]

    public static func find(_ code: String) -> SpokenLanguage? {
        all.first { $0.code == code }
    }

    /// The stored list, "en,de", as known codes in the order of ``all``.
    public static func codes(_ stored: String) -> [String] {
        let wanted = Set(stored.split(separator: ",").map { $0.trimmingCharacters(in: .whitespaces) })
        return all.map(\.code).filter(wanted.contains)
    }

    /// The user's languages on first start: those of the Mac's preferred
    /// languages ("en-US", "ru-RU") that Parakeet knows, or English.
    public static func defaults(preferred: [String]) -> [String] {
        let known = codes(preferred.compactMap { $0.split(separator: "-").first.map(String.init) }.joined(separator: ","))
        return known.isEmpty ? ["en"] : known
    }

    /// The language to hold recognition to, if any: the one chosen, or in
    /// automatic mode any of the user's languages when they all share one
    /// alphabet. The hint only keeps other alphabets out, so any language of
    /// that alphabet does.
    /// - Parameters:
    ///   - mode: "auto", or the code of the one language to listen for.
    ///   - chosen: The user's languages.
    public static func filter(mode: String, chosen: [String]) -> String? {
        let languages = chosen.compactMap(find)
        if languages.contains(where: { $0.code == mode }) { return mode }
        let scripts = Set(languages.map(\.script))
        return scripts.count == 1 ? languages.first?.code : nil
    }
}
