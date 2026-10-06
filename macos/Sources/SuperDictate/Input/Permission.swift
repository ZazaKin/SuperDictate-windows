import AppKit
import ApplicationServices
import AVFoundation
import CoreGraphics
import SwiftUI

/// The three things macOS asks the user to allow. SuperDictate never resets
/// them; taking a permission back is the user's call, in System Settings.
enum Permission: CaseIterable, Identifiable {
    case microphone
    /// To type the text into other apps.
    case accessibility
    /// To notice the dictation key while other apps are in front.
    case inputMonitoring

    var id: Self { self }

    var title: String {
        switch self {
        case .microphone: "Microphone"
        case .accessibility: "Accessibility"
        case .inputMonitoring: "Input Monitoring"
        }
    }

    var detail: String {
        switch self {
        case .microphone: "To hear you while you dictate."
        case .accessibility: "To type your words where the cursor is."
        case .inputMonitoring: "To notice the dictation key in any app."
        }
    }

    var symbol: String {
        switch self {
        case .microphone: "mic.fill"
        case .accessibility: "accessibility"
        case .inputMonitoring: "keyboard.fill"
        }
    }

    var tint: Color {
        switch self {
        case .microphone: .orange
        case .accessibility: .blue
        case .inputMonitoring: .gray
        }
    }

    var isGranted: Bool {
        switch self {
        case .microphone: AVCaptureDevice.authorizationStatus(for: .audio) == .authorized
        case .accessibility: AXIsProcessTrusted()
        case .inputMonitoring: CGPreflightListenEventAccess()
        }
    }

    /// The first time, shows the system prompt. After that macOS won't prompt again,
    /// so it opens the matching pane of System Settings: one or the other, never both.
    func request() {
        let asked = "permission.asked.\(self)"
        switch self {
        case .microphone:
            if AVCaptureDevice.authorizationStatus(for: .audio) == .notDetermined {
                AVCaptureDevice.requestAccess(for: .audio) { _ in }
            } else {
                openSettings()
            }
        case .accessibility, .inputMonitoring:
            guard !UserDefaults.standard.bool(forKey: asked) else {
                openSettings()
                return
            }
            UserDefaults.standard.set(true, forKey: asked)
            if self == .accessibility {
                // The documented value of kAXTrustedCheckOptionPrompt.
                _ = AXIsProcessTrustedWithOptions(["AXTrustedCheckOptionPrompt": true] as CFDictionary)
            } else {
                _ = CGRequestListenEventAccess()
            }
        }
    }

    func openSettings() {
        let pane = switch self {
        case .microphone: "Privacy_Microphone"
        case .accessibility: "Privacy_Accessibility"
        case .inputMonitoring: "Privacy_ListenEvent"
        }
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?\(pane)") {
            NSWorkspace.shared.open(url)
        }
    }
}
