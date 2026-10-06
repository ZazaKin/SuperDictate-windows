import AppKit
import CoreGraphics

/// Watches the dictation key system-wide, through a listen-only event tap: it
/// sees keys but never holds them back or changes them, which needs only Input
/// Monitoring. The key and the mode are read from ``Preferences`` on every
/// event, so a change in Settings applies at once.
///
/// A press counts only when no other key went down meanwhile: Right ⌘ + C
/// copies as always and starts nothing.
@MainActor
final class HotkeyListener {
    /// Toggle mode: the key was pressed and let go on its own.
    var onToggle: (() -> Void)?
    /// Hold mode: the key went down, and later came back up.
    var onHoldStart: (() -> Void)?
    var onHoldEnd: (() -> Void)?
    /// Escape while dictating, or another key pressed during a hold.
    var onCancel: (() -> Void)?
    var isDictating: () -> Bool = { false }

    private var tap: CFMachPort?
    private var source: CFRunLoopSource?
    private var down = false
    private var chord = false

    var isRunning: Bool { tap != nil }

    /// Starts listening. Fails, harmlessly, until Input Monitoring is allowed.
    @discardableResult
    func start() -> Bool {
        if tap != nil { return true }
        let mask: CGEventMask = (1 << CGEventType.keyDown.rawValue) | (1 << CGEventType.flagsChanged.rawValue)
        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: .listenOnly,
            eventsOfInterest: mask,
            callback: { _, type, event, info in
                if let info {
                    let listener = Unmanaged<HotkeyListener>.fromOpaque(info).takeUnretainedValue()
                    let keyCode = event.getIntegerValueField(.keyboardEventKeycode)
                    let flags = event.flags.rawValue
                    // The tap runs on the main run loop.
                    MainActor.assumeIsolated { listener.handle(type, keyCode: keyCode, flags: flags) }
                }
                return Unmanaged.passUnretained(event)
            },
            userInfo: Unmanaged.passUnretained(self).toOpaque()
        ) else {
            return false
        }

        self.tap = tap
        source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        return true
    }

    private func handle(_ type: CGEventType, keyCode: Int64, flags: UInt64) {
        switch type {
        case .tapDisabledByTimeout, .tapDisabledByUserInput:
            // macOS turns a slow tap off; turn it back on.
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }

        case .keyDown:
            if keyCode == 53, isDictating() {
                onCancel?() // Escape
                return
            }
            guard down else { return }
            chord = true
            if Preferences.mode == .hold, isDictating() { onCancel?() }

        case .flagsChanged:
            let hotkey = Preferences.hotkey
            guard keyCode == hotkey.keyCode else {
                if down { chord = true } // Another modifier joined in: a shortcut, not dictation.
                return
            }
            let pressed = flags & hotkey.deviceFlag != 0
            if pressed, !down {
                down = true
                chord = false
                if Preferences.mode == .hold { onHoldStart?() }
            } else if !pressed, down {
                down = false
                if Preferences.mode == .hold {
                    onHoldEnd?()
                } else if !chord {
                    onToggle?()
                }
            }

        default:
            break
        }
    }
}
