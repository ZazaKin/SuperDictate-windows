import AppKit
import CoreGraphics

/// Types text where the cursor is, as keyboard events carrying the characters,
/// so the clipboard stays as the user left it. Needs Accessibility; without it
/// the text goes to the clipboard instead, and the caller says so.
@MainActor
enum TextInserter {
    /// - Returns: false when the text could only be copied.
    @discardableResult
    static func type(_ text: String, pressReturn: Bool) -> Bool {
        guard AXIsProcessTrusted() else {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(text, forType: .string)
            return false
        }

        let source = CGEventSource(stateID: .combinedSessionState)
        let units = Array(text.utf16)
        var start = 0
        while start < units.count {
            // An event carries at most 20 UTF-16 units; never split a surrogate pair between two.
            var end = min(start + 20, units.count)
            if end < units.count, UTF16.isLeadSurrogate(units[end - 1]) { end -= 1 }
            post(Array(units[start ..< end]), source: source)
            start = end
        }

        if pressReturn {
            for down in [true, false] {
                CGEvent(keyboardEventSource: source, virtualKey: 36, keyDown: down)?.post(tap: .cghidEventTap)
            }
        }
        return true
    }

    /// A key down and its key up, both carrying the characters: apps that track key state need the pair.
    private static func post(_ units: [UInt16], source: CGEventSource?) {
        for down in [true, false] {
            guard let event = CGEvent(keyboardEventSource: source, virtualKey: 0, keyDown: down) else { continue }
            event.flags = []
            units.withUnsafeBufferPointer { buffer in
                if let base = buffer.baseAddress {
                    event.keyboardSetUnicodeString(stringLength: buffer.count, unicodeString: base)
                }
            }
            event.post(tap: .cghidEventTap)
        }
    }
}
