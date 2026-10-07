import AppKit
import SuperDictateCore
import SwiftUI

/// Settings › Capsule. While the page is open and the window is in front, the
/// real capsule shows on screen with sample words building up in it, and every
/// change shows on it at once. Changes apply as they're made, as everywhere in
/// Settings on a Mac.
struct CapsulePane: View {
    @Environment(DictationModel.self) private var model
    @Environment(\.controlActiveState) private var windowState
    private var stored = StoredCapsuleStyle()
    @AppStorage(Preferences.Key.placement) private var placement = CapsulePlacement.topCenter
    @AppStorage(Preferences.Key.screen) private var screen = "active"
    @AppStorage(Preferences.Key.livePreview) private var livePreview = true
    @State private var onScreen = false
    @State private var moving = false

    var body: some View {
        Form {
            Section {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 150), spacing: 10)], spacing: 10) {
                    ForEach(CapsuleSkin.all) { skin in
                        SkinTile(skin: skin, style: stored.style, chosen: skin.id == stored.skin) { stored.skin = skin.id }
                    }
                }
                .padding(.vertical, 4)
            } header: {
                Text("Skin")
            } footer: {
                Footnote("While this page is open, the capsule shows on your screen, and every change shows on it at once.")
            }

            Section("Look") {
                LabeledContent("Size") {
                    ValueSlider(value: stored.$scale, range: 0.7 ... 1.6, step: 0.1,
                                text: stored.scale.formatted(.number.precision(.fractionLength(1))) + "×")
                }
                LabeledContent("Widest") {
                    ValueSlider(value: stored.$maxWidth, range: CapsuleStyle.widestRange, step: 20,
                                text: stored.maxWidth.formatted(.number.precision(.fractionLength(0))))
                }
                LabeledContent("Opacity") {
                    ValueSlider(value: stored.$opacity, range: 0.5 ... 1, step: 0.05,
                                text: stored.opacity.formatted(.percent.precision(.fractionLength(0))))
                }
                LabeledContent("Accent") {
                    AccentPicker(selection: stored.$accent)
                }
                Picker("Voice meter", selection: stored.$meter) {
                    ForEach(CapsuleStyle.Meter.allCases) { Text($0.title).tag($0) }
                }
                .pickerStyle(.segmented)
            }

            Section {
                LabeledContent("Place") {
                    HStack {
                        Picker("Place", selection: preset) {
                            ForEach(CapsulePlacement.presets) { Text($0.name).tag($0.id) }
                            Text("Custom").tag("custom")
                        }
                        .labelsHidden()
                        .fixedSize()
                        Button("Move…", action: move)
                    }
                }
                Picker("Screen", selection: $screen) {
                    Text("Where You're Working").tag("active")
                    Text("Main Screen").tag("main")
                }
            } header: {
                Text("Position")
            } footer: {
                Footnote("Move… lets you drag it anywhere. Next to the left or right edge, it grows away from that edge as your words appear.")
            }

            Section {
                Toggle("Show words as you speak", isOn: $livePreview)
                Toggle("Show recording time", isOn: stored.$timer)
            } header: {
                Text("While You Dictate")
            } footer: {
                Footnote("The words are a quick draft. When you finish, the whole recording is transcribed again, so the typed text can be a little better.")
            }
        }
        .formStyle(.grouped)
        .toggleStyle(.switch)
        .onAppear { onScreen = true }
        .onDisappear { onScreen = false }
        .onChange(of: staged, initial: true) { model.stage(staged) }
    }

    /// The sample shows while this page is on screen and the window is in front,
    /// except while the position editor shows its own capsule.
    private var staged: Bool { onScreen && !moving && windowState != .inactive }

    /// The preset the placement matches, or "custom" for a dragged one.
    private var preset: Binding<String> {
        Binding {
            CapsulePlacement.presets.first { $0.placement == placement }?.id ?? "custom"
        } set: { id in
            // "Custom" keeps the dragged spot.
            if let chosen = CapsulePlacement.presets.first(where: { $0.id == id }) { placement = chosen.placement }
        }
    }

    private func move() {
        guard let screen = NSApp.keyWindow?.screen ?? NSScreen.main else { return }
        moving = true
        CapsuleLayoutEditor.show(on: screen, placement: placement, style: stored.style) { chosen in
            if let chosen { placement = chosen }
            moving = false
        }
    }
}

/// A skin, drawn as a small capsule, with its name. The chosen one is ringed.
private struct SkinTile: View {
    let skin: CapsuleSkin
    let style: CapsuleStyle
    let chosen: Bool
    let choose: () -> Void

    var body: some View {
        Button(action: choose) {
            VStack(spacing: 8) {
                CapsulePill(style: CapsuleStyle(skin: skin, accent: style.accent, scale: 0.75, meter: style.meter),
                            live: false)
                    .frame(height: 38)
                Text(skin.name)
                    .font(.callout)
                    .foregroundStyle(chosen ? .primary : .secondary)
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 12)
            .background(chosen ? Color.accentColor.opacity(0.12) : Color.primary.opacity(0.04),
                        in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous)
                .strokeBorder(chosen ? Color.accentColor : .clear, lineWidth: 2))
            .contentShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
        }
        .buttonStyle(.plain)
        .accessibilityLabel("\(skin.name) skin")
        .accessibilityAddTraits(chosen ? .isSelected : [])
        .animation(.easeOut(duration: 0.15), value: chosen)
    }
}

/// A slider with its value beside it.
private struct ValueSlider: View {
    @Binding var value: Double
    let range: ClosedRange<Double>
    let step: Double
    let text: String

    var body: some View {
        HStack(spacing: 10) {
            Slider(value: $value, in: range, step: step)
                .labelsHidden()
                .frame(width: 180)
            Text(text)
                .monospacedDigit()
                .foregroundStyle(.secondary)
                .frame(width: 40, alignment: .trailing)
        }
    }
}

/// Color dots, as in System Settings › Appearance: Multicolor follows the Mac's accent color.
private struct AccentPicker: View {
    @Binding var selection: String

    var body: some View {
        HStack(spacing: 8) {
            ForEach(CapsuleAccent.all) { accent in
                let chosen = accent.id == selection
                Button {
                    selection = accent.id
                } label: {
                    Circle()
                        .fill(fill(accent.id))
                        .frame(width: 18, height: 18)
                        .overlay(Circle().strokeBorder(.black.opacity(0.15)))
                        .overlay {
                            if chosen {
                                Circle().fill(.white).frame(width: 6, height: 6)
                            }
                        }
                        .padding(2)
                        .overlay(Circle().strokeBorder(chosen ? Color.accentColor.opacity(0.5) : .clear, lineWidth: 2))
                }
                .buttonStyle(.plain)
                .help(accent.name)
                .accessibilityLabel(accent.name)
                .accessibilityAddTraits(chosen ? .isSelected : [])
            }
        }
    }

    private func fill(_ id: String) -> AnyShapeStyle {
        guard let color = RGBA(hex: id) else {
            return AnyShapeStyle(AngularGradient(colors: [.red, .orange, .yellow, .green, .blue, .purple, .red], center: .center))
        }
        return AnyShapeStyle(Color(color))
    }
}
