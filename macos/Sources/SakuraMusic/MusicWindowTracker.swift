import AppKit
import CoreGraphics

/// One on-screen Music.app window, in Cocoa screen coordinates (origin bottom-left).
struct TrackedWindow: Equatable {
    let id: CGWindowID
    let frame: CGRect
    /// True when some other app's window overlaps this one from above, in which case
    /// the overlay hides so it does not paint petals over the foreign window.
    let occluded: Bool
    /// Name of the app whose window is on top of this one, when occluded.
    let coveredBy: String?
    /// True when the window fills a whole display (full screen / zoomed), which
    /// means the window has square corners instead of the usual rounded ones.
    let fillsScreen: Bool
}

/// Polls the window server for Music.app windows.
///
/// Uses `CGWindowListCopyWindowInfo`, which reports window bounds, owner and
/// z-order without any privacy permission (only window *titles* are gated behind
/// Screen Recording, and those are not needed).
final class MusicWindowTracker {
    static let musicBundleID = "com.apple.Music"

    var onUpdate: (([TrackedWindow]) -> Void)?
    private(set) var musicIsRunning = false

    private var running = false
    private let ownPID = getpid()

    private let activeInterval: TimeInterval = 1.0 / 30.0
    private let idleInterval: TimeInterval = 1.0 / 5.0

    func start() {
        guard !running else { return }
        running = true
        tick()
    }

    func stop() {
        running = false
    }

    private func tick() {
        guard running else { return }
        let windows = poll()
        onUpdate?(windows)

        // Poll fast only while there is something to keep aligned; otherwise idle.
        let anyVisible = windows.contains { !$0.occluded }
        let interval = anyVisible ? activeInterval : idleInterval
        DispatchQueue.main.asyncAfter(deadline: .now() + interval) { [weak self] in
            self?.tick()
        }
    }

    private func poll() -> [TrackedWindow] {
        let musicPIDs = Set(
            NSRunningApplication.runningApplications(withBundleIdentifier: Self.musicBundleID)
                .map { $0.processIdentifier }
        )
        musicIsRunning = !musicPIDs.isEmpty
        guard musicIsRunning else { return [] }

        let options: CGWindowListOption = [.optionOnScreenOnly, .excludeDesktopElements]
        guard let list = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] else {
            return []
        }

        let screens = NSScreen.screens
        let primaryHeight = screens.first?.frame.height ?? 0
        let screenFrames = screens.map { $0.frame }

        // The list is ordered front to back. Track foreign windows seen so far so we
        // can tell whether a Music window is covered by something above it.
        var foreign: [(rect: CGRect, owner: String)] = []
        var result: [TrackedWindow] = []

        for info in list {
            guard
                let pid = (info[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value,
                let layer = (info[kCGWindowLayer as String] as? NSNumber)?.intValue,
                layer == 0,
                let alpha = (info[kCGWindowAlpha as String] as? NSNumber)?.doubleValue,
                alpha > 0.01,
                let boundsDict = info[kCGWindowBounds as String] as? NSDictionary,
                let cgBounds = CGRect(dictionaryRepresentation: boundsDict as CFDictionary),
                cgBounds.width > 1, cgBounds.height > 1
            else { continue }

            if pid == ownPID { continue }  // our own overlays never count as occluders

            // CG window coordinates have their origin at the top-left of the primary
            // display with y pointing down; Cocoa uses bottom-left with y pointing up.
            let frame = CGRect(
                x: cgBounds.origin.x,
                y: primaryHeight - cgBounds.origin.y - cgBounds.height,
                width: cgBounds.width,
                height: cgBounds.height
            )

            if musicPIDs.contains(pid) {
                // Skip tooltips, popovers and other transient scraps.
                guard frame.width >= 200, frame.height >= 150,
                      let id = (info[kCGWindowNumber as String] as? NSNumber)?.uint32Value
                else { continue }

                let coveredBy = foreign.first { $0.rect.intersects(frame) }?.owner
                let fillsScreen = screenFrames.contains { $0.equalTo(frame) }
                result.append(TrackedWindow(id: id, frame: frame, occluded: coveredBy != nil,
                                            coveredBy: coveredBy, fillsScreen: fillsScreen))
            } else {
                let owner = info[kCGWindowOwnerName as String] as? String ?? "another app"
                foreign.append((frame, owner))
            }
        }
        return result
    }
}
