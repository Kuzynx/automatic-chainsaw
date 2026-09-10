import AppKit
import ServiceManagement

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let settings = Settings.shared
    private let tracker = MusicWindowTracker()
    private lazy var overlays = OverlayManager(settings: settings)

    private var statusItem: NSStatusItem!
    private var statusLine: NSMenuItem!
    private var enabledItem: NSMenuItem!
    private var branchItem: NSMenuItem!
    private var loginItem: NSMenuItem!
    private var petalItems: [NSMenuItem] = []
    private var blushItems: [NSMenuItem] = []

    func applicationDidFinishLaunching(_ notification: Notification) {
        buildStatusItem()

        settings.onChange = { [weak self] in
            self?.overlays.applySettings()
            self?.refreshMenuState()
        }

        tracker.onUpdate = { [weak self] windows in
            guard let self else { return }
            self.overlays.sync(with: windows)
            self.updateStatusLine()
        }
        tracker.start()
    }

    // MARK: - Menu

    private func buildStatusItem() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.title = "🌸"
        statusItem.button?.toolTip = "Sakura Music"

        let menu = NSMenu()

        statusLine = NSMenuItem(title: "Looking for Music…", action: nil, keyEquivalent: "")
        statusLine.isEnabled = false
        menu.addItem(statusLine)
        menu.addItem(.separator())

        enabledItem = NSMenuItem(title: "Enabled", action: #selector(toggleEnabled), keyEquivalent: "")
        enabledItem.target = self
        menu.addItem(enabledItem)

        let petalsMenu = NSMenu()
        for density in Settings.PetalDensity.allCases {
            let item = NSMenuItem(title: density.title, action: #selector(choosePetals(_:)), keyEquivalent: "")
            item.target = self
            item.tag = density.rawValue
            petalsMenu.addItem(item)
            petalItems.append(item)
        }
        let petalsItem = NSMenuItem(title: "Petals", action: nil, keyEquivalent: "")
        petalsItem.submenu = petalsMenu
        menu.addItem(petalsItem)

        let blushMenu = NSMenu()
        for blush in Settings.Blush.allCases {
            let item = NSMenuItem(title: blush.title, action: #selector(chooseBlush(_:)), keyEquivalent: "")
            item.target = self
            item.tag = blush.rawValue
            blushMenu.addItem(item)
            blushItems.append(item)
        }
        let blushItem = NSMenuItem(title: "Blush tint", action: nil, keyEquivalent: "")
        blushItem.submenu = blushMenu
        menu.addItem(blushItem)

        branchItem = NSMenuItem(title: "Blossom branch", action: #selector(toggleBranch), keyEquivalent: "")
        branchItem.target = self
        menu.addItem(branchItem)

        menu.addItem(.separator())

        loginItem = NSMenuItem(title: "Launch at Login", action: #selector(toggleLaunchAtLogin), keyEquivalent: "")
        loginItem.target = self
        menu.addItem(loginItem)

        menu.addItem(.separator())
        let quit = NSMenuItem(title: "Quit Sakura Music", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        menu.addItem(quit)

        statusItem.menu = menu
        refreshMenuState()
    }

    private func refreshMenuState() {
        enabledItem.state = settings.enabled ? .on : .off
        branchItem.state = settings.showBranch ? .on : .off
        for item in petalItems { item.state = item.tag == settings.petals.rawValue ? .on : .off }
        for item in blushItems { item.state = item.tag == settings.blush.rawValue ? .on : .off }
        loginItem.state = SMAppService.mainApp.status == .enabled ? .on : .off
        statusItem.button?.appearsDisabled = !settings.enabled
    }

    private var lastStatusText = ""

    private func updateStatusLine() {
        let text: String
        if !tracker.musicIsRunning {
            text = "Music is not running"
        } else if !settings.enabled {
            text = "Paused"
        } else {
            switch overlays.visibleCount {
            case 0: text = "Music is hidden or covered"
            case 1: text = "Blooming over Music"
            case let n: text = "Blooming over \(n) Music windows"
            }
        }
        if text != lastStatusText {
            lastStatusText = text
            statusLine.title = text
        }
    }

    // MARK: - Actions

    @objc private func toggleEnabled() {
        settings.enabled.toggle()
    }

    @objc private func toggleBranch() {
        settings.showBranch.toggle()
    }

    @objc private func choosePetals(_ sender: NSMenuItem) {
        guard let density = Settings.PetalDensity(rawValue: sender.tag) else { return }
        settings.petals = density
    }

    @objc private func chooseBlush(_ sender: NSMenuItem) {
        guard let blush = Settings.Blush(rawValue: sender.tag) else { return }
        settings.blush = blush
    }

    @objc private func toggleLaunchAtLogin() {
        let service = SMAppService.mainApp
        do {
            if service.status == .enabled {
                try service.unregister()
            } else {
                try service.register()
            }
        } catch {
            let alert = NSAlert()
            alert.messageText = "Couldn't change Launch at Login"
            alert.informativeText = "\(error.localizedDescription)\n\nThis only works from the built app bundle (run ./build.sh), not from `swift run`."
            alert.runModal()
        }
        refreshMenuState()
    }
}
