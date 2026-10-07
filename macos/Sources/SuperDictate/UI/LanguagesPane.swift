import SuperDictateCore
import SwiftUI

/// Settings › Languages: the languages the user speaks, as flag tiles, and
/// whether to tell them apart automatically or listen for just one.
struct LanguagesPane: View {
    @AppStorage(Preferences.Key.languages) private var stored = Preferences.defaultLanguages
    @AppStorage(Preferences.Key.language) private var mode = "auto"
    @State private var search = ""

    private var chosen: [String] { SpokenLanguage.codes(stored) }

    private var found: [SpokenLanguage] {
        let query = search.trimmingCharacters(in: .whitespaces)
        guard !query.isEmpty else { return SpokenLanguage.all }
        return SpokenLanguage.all.filter {
            $0.native.localizedCaseInsensitiveContains(query) || $0.english.localizedCaseInsensitiveContains(query)
        }
    }

    var body: some View {
        Form {
            Section {
                HStack(spacing: 6) {
                    Image(systemName: "magnifyingglass")
                        .foregroundStyle(.secondary)
                    TextField("Search", text: $search, prompt: Text("Search \(SpokenLanguage.all.count) languages"))
                        .textFieldStyle(.plain)
                        .labelsHidden()
                }
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 170), spacing: 8)], spacing: 8) {
                    ForEach(found) { language in
                        LanguageTile(language: language, chosen: chosen.contains(language.code),
                                     last: chosen == [language.code]) { toggle(language.code) }
                    }
                }
                .padding(.vertical, 2)
                if found.isEmpty {
                    Text("No language matches “\(search)”.")
                        .foregroundStyle(.secondary)
                }
            } header: {
                HStack {
                    Text("Languages You Speak")
                    Spacer()
                    Text("\(chosen.count) of \(SpokenLanguage.all.count)")
                        .monospacedDigit()
                        .foregroundStyle(.secondary)
                        .contentTransition(.numericText())
                        .animation(.snappy, value: chosen.count)
                }
            } footer: {
                Footnote("These are the 25 European languages the speech model knows.")
            }

            Section {
                Picker("Recognize", selection: modeChoice) {
                    Text("Automatically").tag("auto")
                    ForEach(chosen, id: \.self) { code in
                        Text("Only \(SpokenLanguage.find(code)?.english ?? code)").tag(code)
                    }
                }
            } header: {
                Text("Detection")
            } footer: {
                Footnote("Automatically tells your languages apart. When they all use one alphabet, it also keeps other alphabets out. Choose one language if short phrases still come out wrong.")
            }
        }
        .formStyle(.grouped)
        .frame(height: 600)
    }

    /// A one-language choice that's no longer among the user's languages reads as automatic.
    private var modeChoice: Binding<String> {
        Binding { chosen.contains(mode) ? mode : "auto" } set: { mode = $0 }
    }

    private func toggle(_ code: String) {
        var codes = Set(chosen)
        if codes.contains(code) {
            guard codes.count > 1 else { return }
            codes.remove(code)
        } else {
            codes.insert(code)
        }
        stored = SpokenLanguage.all.map(\.code).filter(codes.contains).joined(separator: ",")
    }
}

/// A language: its flag, its own name and its English name, and a check when chosen.
private struct LanguageTile: View {
    let language: SpokenLanguage
    let chosen: Bool
    /// The only language chosen, which stays: dictation needs one.
    let last: Bool
    let toggle: () -> Void

    var body: some View {
        Button(action: toggle) {
            HStack(spacing: 10) {
                Text(language.flag)
                    .font(.system(size: 22))
                VStack(alignment: .leading, spacing: 0) {
                    Text(language.native)
                        .lineLimit(1)
                    Text(language.english)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
                Spacer(minLength: 4)
                Image(systemName: chosen ? "checkmark.circle.fill" : "circle")
                    .font(.system(size: 16))
                    .foregroundStyle(chosen ? Color.accentColor : Color.secondary.opacity(0.5))
                    .contentTransition(.symbolEffect(.replace))
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .background(chosen ? Color.accentColor.opacity(0.12) : Color.primary.opacity(0.04),
                        in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            .contentShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
        .buttonStyle(.plain)
        .help(last ? "Keep at least one language" : language.english)
        .accessibilityLabel(language.english)
        .accessibilityAddTraits(chosen ? .isSelected : [])
        .animation(.spring(duration: 0.25, bounce: 0), value: chosen)
    }
}
