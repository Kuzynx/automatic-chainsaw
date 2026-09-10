# 🌸 Sakura Music

A cherry blossom theme that goes **over the real Apple Music app** on macOS and Windows.

Apple doesn't let anyone restyle the signed Music app itself, so Sakura Music does the next
best thing: it draws a transparent, click-through window that tracks Apple Music's window
pixel-for-pixel and paints the theme on top of it.

- **Falling petals** drift down the whole window with a bit of wind.
- **Blush tint** warms the interface with a soft pink wash.
- **Blossom branch** reaches in from the top-right corner.

Everything underneath keeps working exactly as before. The overlay never takes focus, never
receives clicks, and never touches the Music app or its files. It hides automatically when
Music is minimised, hidden, or covered by another app, and follows Music into full screen.

Runs from the menu bar (macOS) or the system tray (Windows), where you can change the petal
density, tint strength, toggle the branch, or pause it.

---

## macOS

**Requirements:** macOS 13 Ventura or newer, and the Xcode Command Line Tools
(`xcode-select --install` if you don't have them).

### Build and run

```bash
git clone https://github.com/kuzynx/automatic-chainsaw.git
cd automatic-chainsaw/macos
./build.sh
open "build/Sakura Music.app"
```

To keep it around, copy it to Applications:

```bash
cp -R "build/Sakura Music.app" /Applications/
```

The first launch of an unsigned local build may show a Gatekeeper prompt. Right-click the
app and choose **Open** once, and it won't ask again.

For a quick test without building a bundle, `swift run` from the `macos/` folder works too
(Launch at Login is unavailable that way).

### Using it

1. Launch Sakura Music. A 🌸 appears in the menu bar.
2. Open Apple Music. The theme appears over the Music window as soon as it's on screen.
3. Click 🌸 to adjust:
   - **Enabled** pauses or resumes the whole theme.
   - **Petals** sets density: Off, Light breeze, Gentle fall, Full bloom.
   - **Blush tint** sets the pink wash: Off, Soft, Deep.
   - **Blossom branch** shows or hides the corner branch.
   - **Show even when covered** keeps the theme up when another window overlaps Music.
     Handy on multi-monitor setups, e.g. Music on one display while a game runs on the other.
   - **Launch at Login** starts it with your Mac (built app only).
4. **Quit Sakura Music** removes the overlay instantly.

No privacy permissions are required. Window positions come from the window server, which
doesn't need Screen Recording or Accessibility access.

---

## Windows

**Requirements:** Windows 10 or 11, the [Apple Music app from the Microsoft Store](https://apps.microsoft.com/detail/9PFHSD62MV6P),
and the [.NET SDK](https://dotnet.microsoft.com/download) (8.0 or newer).

### Build and run

```powershell
git clone https://github.com/kuzynx/automatic-chainsaw.git
cd automatic-chainsaw\windows\SakuraMusic
dotnet publish -c Release -r win-x64 --self-contained false -o ..\..\build\windows
..\..\build\windows\SakuraMusic.exe
```

Or during development, just `dotnet run` from that folder.

### Using it

1. Launch SakuraMusic.exe. A notification confirms it's running and a 🌸 blossom icon appears
   in the system tray. Windows 11 hides new tray icons by default: click the `^` arrow next to
   the clock to find it, and drag it onto the taskbar to keep it visible.
   If it ever crashes, a dialog appears and details go to `%APPDATA%\SakuraMusic\error.log`.
   If the theme doesn't appear over Music, the status line at the top of the tray menu says why,
   and **Copy diagnostics** puts a list of every window the tracker sees on the clipboard for a
   bug report.
2. Open Apple Music. The theme appears over the Music window.
3. Right-click the tray icon to adjust **Enabled**, **Petals**, **Blush tint**,
   **Blossom branch**, **Show even when covered** (keeps the theme up while a game runs on
   another monitor), and **Launch at startup**.
4. **Quit Sakura Music** removes the overlay instantly.

---

## How it works

Both builds share the same design:

| Piece | macOS | Windows |
| --- | --- | --- |
| Find Music's windows | `CGWindowListCopyWindowInfo` (front-to-back, no permissions) | `EnumWindows` + DWM extended frame bounds |
| Overlay window | Borderless `NSWindow`, `ignoresMouseEvents`, one level above normal windows | Borderless WPF window with `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE`, topmost |
| Petals | `CAEmitterLayer` with drawn petal sprites | Custom `FrameworkElement` rendering a petal field each frame |
| Branch | Procedural `CAShapeLayer` tree from a fixed seed | Same algorithm as a cached `DrawingGroup` |
| Tint | `CAGradientLayer` stack | XAML gradient brushes |

The tracker polls at 30 Hz while an overlay is visible and drops to 5 Hz otherwise. If any
other app's window is stacked above Music and overlaps it, the overlay hides so it never
paints petals on top of something else.

## Limitations

- The theme is an overlay, so it can't recolour Music's own buttons, text or album art. It
  adds petals, tint and the branch on top of the stock interface.
- Not possible on iOS or iPadOS: there is no way to draw over another app there.
- On macOS, Music's MiniPlayer set to "Keep on top" floats at a higher level than the
  overlay and won't be themed.
- macOS Music context menus and popovers appear above the petals, which is intended.

## License

MIT. See [LICENSE](LICENSE).
