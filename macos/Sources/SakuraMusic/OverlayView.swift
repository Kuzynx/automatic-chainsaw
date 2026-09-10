import AppKit
import QuartzCore

/// Layer-backed view holding the three visual layers of the theme:
/// a blush tint, a blossom branch in the top-right corner and falling petals.
final class OverlayView: NSView {
    private let settings: Settings

    private let tintGroup = CALayer()
    private let tintLayer = CAGradientLayer()
    private let glowLayer = CAGradientLayer()
    private let duskLayer = CAGradientLayer()
    private var branchLayer: CALayer?
    private let emitter = CAEmitterLayer()
    private var windTimer: Timer?

    private static let branchSize = CGSize(width: 470, height: 300)
    private static let cellNames = ["blush", "pink", "rose", "white"]

    var cornerRadius: CGFloat = 10 {
        didSet { layer?.cornerRadius = cornerRadius }
    }

    init(frame: NSRect, settings: Settings) {
        self.settings = settings
        super.init(frame: frame)
        wantsLayer = true
        layerContentsRedrawPolicy = .never

        guard let root = layer else { return }
        root.masksToBounds = true
        root.cornerRadius = cornerRadius
        root.backgroundColor = nil

        configureTint()
        configureEmitter()

        root.addSublayer(tintGroup)
        root.addSublayer(emitter)
        applySettings()
        startWind()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) { fatalError("init(coder:) is not supported") }

    deinit { windTimer?.invalidate() }

    // MARK: - Layers

    private func configureTint() {
        // Diagonal wash, strongest in the top-left, fading to a warm bottom-right.
        tintLayer.type = .axial
        tintLayer.startPoint = CGPoint(x: 0, y: 1)
        tintLayer.endPoint = CGPoint(x: 1, y: 0)
        tintLayer.colors = [
            SakuraPalette.hex(0xFFB7C5, alpha: 0.20),
            SakuraPalette.hex(0xFFC9D6, alpha: 0.06),
            SakuraPalette.hex(0xFFD8B8, alpha: 0.12),
        ]
        tintLayer.locations = [0, 0.5, 1]

        // Radial glow behind the branch.
        glowLayer.type = .radial
        glowLayer.startPoint = CGPoint(x: 0.88, y: 0.96)
        glowLayer.endPoint = CGPoint(x: 1.38, y: 1.56)
        glowLayer.colors = [
            SakuraPalette.hex(0xFFB3CB, alpha: 0.32),
            SakuraPalette.hex(0xFFB3CB, alpha: 0.0),
        ]

        // Slight cool dusk along the bottom edge so the tint does not read flat.
        duskLayer.type = .axial
        duskLayer.startPoint = CGPoint(x: 0.5, y: 0)
        duskLayer.endPoint = CGPoint(x: 0.5, y: 0.35)
        duskLayer.colors = [
            SakuraPalette.hex(0xC98BB0, alpha: 0.14),
            SakuraPalette.hex(0xC98BB0, alpha: 0.0),
        ]

        tintGroup.addSublayer(tintLayer)
        tintGroup.addSublayer(duskLayer)
        tintGroup.addSublayer(glowLayer)
    }

    private func configureEmitter() {
        emitter.emitterShape = .line
        emitter.emitterMode = .surface
        emitter.renderMode = .oldestFirst
        emitter.masksToBounds = false

        let scale = window?.backingScaleFactor ?? 2
        let variants: [(name: String, size: CGFloat, base: CGColor, tip: CGColor, rate: Float)] = [
            ("blush", 22, SakuraPalette.petalRose, SakuraPalette.petalBlush, 2.0),
            ("pink", 18, SakuraPalette.petalPink, SakuraPalette.petalWhite, 1.6),
            ("rose", 15, SakuraPalette.petalRose, SakuraPalette.petalPink, 1.1),
            ("white", 12, SakuraPalette.petalBlush, SakuraPalette.petalWhite, 0.8),
        ]

        emitter.emitterCells = variants.compactMap { variant in
            guard let image = SakuraArt.petalImage(size: variant.size, scale: scale,
                                                   base: variant.base, tip: variant.tip) else { return nil }
            let cell = CAEmitterCell()
            cell.name = variant.name
            cell.contents = image
            cell.contentsScale = scale
            cell.birthRate = variant.rate
            cell.lifetime = 22
            cell.lifetimeRange = 6

            // Layer space is y-up, so "down" is negative y.
            cell.emissionLongitude = -.pi / 2
            cell.emissionRange = .pi / 7
            cell.velocity = 22
            cell.velocityRange = 14
            cell.yAcceleration = -7
            cell.xAcceleration = 0

            cell.spin = 0.9
            cell.spinRange = 1.8
            cell.scale = 1.0
            cell.scaleRange = 0.35
            cell.alphaRange = 0.15
            cell.alphaSpeed = -0.025
            return cell
        }
    }

    // MARK: - Layout

    override func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        needsLayout = true
    }

    override func layout() {
        super.layout()
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        defer { CATransaction.commit() }

        let b = bounds
        tintGroup.frame = b
        tintLayer.frame = b
        glowLayer.frame = b
        duskLayer.frame = b

        emitter.frame = b
        emitter.emitterPosition = CGPoint(x: b.midX, y: b.height + 24)
        emitter.emitterSize = CGSize(width: b.width + 160, height: 1)
        emitter.birthRate = settings.petals.multiplier * Float(max(0.35, min(2.4, b.width / 900)))

        if let branch = branchLayer {
            branch.position = CGPoint(x: b.maxX, y: b.maxY)
            branch.isHidden = !(settings.showBranch && b.width >= 640 && b.height >= 420)
        }
    }

    override func viewDidChangeBackingProperties() {
        super.viewDidChangeBackingProperties()
        let scale = window?.backingScaleFactor ?? 2
        layer?.contentsScale = scale
        emitter.contentsScale = scale
        branchLayer?.contentsScale = scale
        branchLayer?.rasterizationScale = scale
        branchLayer?.sublayers?.forEach { $0.contentsScale = scale }
    }

    // MARK: - Settings

    func applySettings() {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        defer { CATransaction.commit() }

        tintGroup.opacity = settings.blush.opacity

        if settings.showBranch, branchLayer == nil, let root = layer {
            let branch = SakuraArt.makeBranchLayer(size: Self.branchSize)
            let scale = window?.backingScaleFactor ?? 2
            branch.contentsScale = scale
            branch.rasterizationScale = scale
            branch.sublayers?.forEach { $0.contentsScale = scale }
            root.insertSublayer(branch, below: emitter)
            branchLayer = branch
        }
        needsLayout = true
    }

    // MARK: - Wind

    /// Every few seconds nudge the horizontal drift so the fall is not perfectly straight.
    private func startWind() {
        windTimer = Timer.scheduledTimer(withTimeInterval: 4.5, repeats: true) { [weak self] _ in
            guard let self else { return }
            let gust = Float.random(in: -9...9)
            for name in Self.cellNames {
                self.emitter.setValue(gust, forKeyPath: "emitterCells.\(name).xAcceleration")
            }
        }
        RunLoop.main.add(windTimer!, forMode: .common)
    }
}
