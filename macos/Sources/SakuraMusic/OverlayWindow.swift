import AppKit

/// A borderless, transparent, click-through window that sits one level above
/// ordinary app windows and never takes keyboard or mouse focus.
final class OverlayWindow: NSWindow {
    let overlayView: OverlayView

    init(frame: CGRect, settings: Settings) {
        overlayView = OverlayView(frame: NSRect(origin: .zero, size: frame.size), settings: settings)
        super.init(contentRect: frame, styleMask: [.borderless], backing: .buffered, defer: false)

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        ignoresMouseEvents = true
        isReleasedWhenClosed = false
        animationBehavior = .none
        isExcludedFromWindowsMenu = true
        // One step above normal windows: above Music, below floating panels/menus.
        level = NSWindow.Level(rawValue: NSWindow.Level.normal.rawValue + 1)
        // Follow Music into any Space, including its full-screen Space.
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        contentView = overlayView
    }

    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}
