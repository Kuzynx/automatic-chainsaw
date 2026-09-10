import AppKit

// Menu bar only: no Dock icon, no main window. The overlay windows never take focus.
let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()
