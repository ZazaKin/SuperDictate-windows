import AppKit
import SwiftUI

/// A white SF Symbol on a colored rounded square, the way System Settings marks its rows.
struct IconTile: View {
    let symbol: String
    let color: Color
    var size: CGFloat = 26

    var body: some View {
        Image(systemName: symbol)
            .font(.system(size: size * 0.5, weight: .semibold))
            .foregroundStyle(.white)
            .frame(width: size, height: size)
            .background(color.gradient, in: RoundedRectangle(cornerRadius: size * 0.24, style: .continuous))
    }
}

/// The app's own icon, as macOS draws it.
struct AppIcon: View {
    var size: CGFloat

    var body: some View {
        Image(nsImage: NSApp.applicationIconImage)
            .resizable()
            .interpolation(.high)
            .frame(width: size, height: size)
    }
}

/// "Allowed" with a check, or a button that asks for the permission.
struct PermissionStatus: View {
    let permission: Permission
    let granted: Bool

    var body: some View {
        if granted {
            Label("Allowed", systemImage: "checkmark.circle.fill")
                .foregroundStyle(.green)
                .symbolEffect(.bounce, value: granted)
                .transition(.scale.combined(with: .opacity))
        } else {
            Button("Allow…") { permission.request() }
                .transition(.opacity)
        }
    }
}

/// A permission row: tile, name and why it's needed, then its status.
struct PermissionRow: View {
    let permission: Permission
    let granted: Bool

    var body: some View {
        HStack(spacing: 12) {
            IconTile(symbol: permission.symbol, color: permission.tint)
            VStack(alignment: .leading, spacing: 1) {
                Text(permission.title)
                Text(permission.detail)
                    .font(.callout)
                    .foregroundStyle(.secondary)
            }
            Spacer(minLength: 12)
            PermissionStatus(permission: permission, granted: granted)
        }
        .animation(.spring(duration: 0.35, bounce: 0), value: granted)
    }
}

/// A section's footnote, as in System Settings: secondary text, aligned with the rows above it.
struct Footnote: View {
    let text: String

    init(_ text: String) {
        self.text = text
    }

    var body: some View {
        Text(text)
            .font(.callout)
            .foregroundStyle(.secondary)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}
