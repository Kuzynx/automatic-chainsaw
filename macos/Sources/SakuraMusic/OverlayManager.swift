import AppKit

/// Keeps one overlay window per visible Music window, aligned frame-for-frame.
final class OverlayManager {
    private let settings: Settings
    private var overlays: [CGWindowID: OverlayWindow] = [:]
    private var lastOrder: [CGWindowID] = []

    init(settings: Settings) {
        self.settings = settings
    }

    var visibleCount: Int {
        overlays.values.filter { $0.isVisible }.count
    }

    func sync(with tracked: [TrackedWindow]) {
        let trackedIDs = Set(tracked.map(\.id))

        // Music window went away (closed, minimised, other Space): drop its overlay.
        for (id, overlay) in overlays where !trackedIDs.contains(id) {
            overlay.orderOut(nil)
            overlays[id] = nil
        }

        var orderChanged = false
        for window in tracked {
            let shouldShow = settings.enabled && (!window.occluded || settings.alwaysShow)
            let overlay: OverlayWindow
            if let existing = overlays[window.id] {
                overlay = existing
            } else {
                overlay = OverlayWindow(frame: window.frame, settings: settings)
                overlays[window.id] = overlay
                orderChanged = true
            }

            if overlay.frame != window.frame {
                overlay.setFrame(window.frame, display: true)
            }
            overlay.overlayView.cornerRadius = window.fillsScreen ? 0 : 10

            if shouldShow, !overlay.isVisible {
                overlay.orderFrontRegardless()
                orderChanged = true
            } else if !shouldShow, overlay.isVisible {
                overlay.orderOut(nil)
            }
        }

        // Mirror Music's own z-order: bring overlays forward back-to-front.
        let order = tracked.map(\.id)
        if orderChanged || order != lastOrder {
            for id in order.reversed() {
                if let overlay = overlays[id], overlay.isVisible {
                    overlay.orderFrontRegardless()
                }
            }
            lastOrder = order
        }
    }

    func applySettings() {
        for overlay in overlays.values {
            overlay.overlayView.applySettings()
            if !settings.enabled { overlay.orderOut(nil) }
        }
    }
}
