import AppKit
import QuartzCore

/// Deterministic random source so the branch looks the same on every launch.
struct SeededGenerator: RandomNumberGenerator {
    private var state: UInt64

    init(seed: UInt64) { state = seed &+ 0x9E37_79B9_7F4A_7C15 }

    mutating func next() -> UInt64 {
        state &+= 0x9E37_79B9_7F4A_7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        return z ^ (z >> 31)
    }
}

enum SakuraPalette {
    static let space = CGColorSpace(name: CGColorSpace.sRGB)!

    static func rgba(_ r: CGFloat, _ g: CGFloat, _ b: CGFloat, _ a: CGFloat = 1) -> CGColor {
        CGColor(colorSpace: space, components: [r, g, b, a])!
    }

    static func hex(_ value: UInt32, alpha: CGFloat = 1) -> CGColor {
        rgba(
            CGFloat((value >> 16) & 0xFF) / 255,
            CGFloat((value >> 8) & 0xFF) / 255,
            CGFloat(value & 0xFF) / 255,
            alpha
        )
    }

    // Petals: pale to saturated sakura pinks.
    static let petalBlush = hex(0xFFD6E4)
    static let petalPink = hex(0xFFB7CE)
    static let petalRose = hex(0xF799B8)
    static let petalWhite = hex(0xFFF1F5)

    // Flowers on the branch.
    static let flowerFills: [CGColor] = [hex(0xFFC5D8), hex(0xFFB3CB), hex(0xF9A2BE), hex(0xFFD9E6)]
    static let flowerEdge = hex(0xE9779C, alpha: 0.55)
    static let flowerCenter = hex(0xF7DE7A)
    static let bud = hex(0xE8749A)

    static let branchWood = hex(0x3D2523, alpha: 0.92)
}

enum SakuraArt {
    /// Sakura petal outline: a teardrop with a notched tip, base at the bottom
    /// centre of `rect`, tip at the top.
    static func petalPath(in rect: CGRect) -> CGPath {
        func pt(_ x: CGFloat, _ y: CGFloat) -> CGPoint {
            CGPoint(x: rect.minX + x * rect.width, y: rect.minY + y * rect.height)
        }
        let p = CGMutablePath()
        p.move(to: pt(0.5, 0.0))
        p.addCurve(to: pt(0.30, 0.95), control1: pt(0.02, 0.22), control2: pt(-0.02, 0.86))
        p.addCurve(to: pt(0.5, 0.78), control1: pt(0.41, 1.02), control2: pt(0.47, 0.86))
        p.addCurve(to: pt(0.70, 0.95), control1: pt(0.53, 0.86), control2: pt(0.59, 1.02))
        p.addCurve(to: pt(0.5, 0.0), control1: pt(1.02, 0.86), control2: pt(0.98, 0.22))
        p.closeSubpath()
        return p
    }

    /// Five-petal blossom centred at the origin.
    static func flowerPath(radius: CGFloat) -> CGPath {
        let p = CGMutablePath()
        let petalRect = CGRect(x: -radius * 0.42, y: 0, width: radius * 0.84, height: radius)
        let petal = petalPath(in: petalRect)
        for i in 0..<5 {
            let rotation = CGAffineTransform(rotationAngle: CGFloat(i) * (2 * .pi / 5))
            p.addPath(petal, transform: rotation)
        }
        return p
    }

    /// Renders a soft-shaded petal sprite for the particle emitter.
    static func petalImage(size: CGFloat, scale: CGFloat, base: CGColor, tip: CGColor) -> CGImage? {
        let px = Int(size * scale)
        guard let ctx = CGContext(
            data: nil, width: px, height: px, bitsPerComponent: 8, bytesPerRow: 0,
            space: SakuraPalette.space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ) else { return nil }

        ctx.scaleBy(x: scale, y: scale)
        ctx.setShouldAntialias(true)

        let inset: CGFloat = 1.5
        let rect = CGRect(x: inset, y: inset, width: size - inset * 2, height: size - inset * 2)
        ctx.saveGState()
        ctx.addPath(petalPath(in: rect))
        ctx.clip()
        if let gradient = CGGradient(
            colorsSpace: SakuraPalette.space, colors: [base, tip] as CFArray, locations: [0, 1]
        ) {
            ctx.drawLinearGradient(
                gradient,
                start: CGPoint(x: size / 2, y: rect.minY),
                end: CGPoint(x: size / 2, y: rect.maxY),
                options: []
            )
        }
        ctx.restoreGState()

        // Faint central vein for a little depth.
        ctx.setStrokeColor(SakuraPalette.rgba(1, 1, 1, 0.35))
        ctx.setLineWidth(0.8)
        ctx.move(to: CGPoint(x: size / 2, y: rect.minY + rect.height * 0.08))
        ctx.addLine(to: CGPoint(x: size / 2, y: rect.minY + rect.height * 0.62))
        ctx.strokePath()

        return ctx.makeImage()
    }

    // MARK: - Branch

    /// Builds a layer tree with a cherry branch that reaches in from the top-right
    /// corner of a `size`-sized area. The branch is generated once from a fixed
    /// seed so it is identical across windows and launches.
    static func makeBranchLayer(size: CGSize, seed: UInt64 = 20240401) -> CALayer {
        let root = CALayer()
        root.bounds = CGRect(origin: .zero, size: size)
        root.anchorPoint = CGPoint(x: 1, y: 1)
        root.masksToBounds = false

        var rng = SeededGenerator(seed: seed)
        var segments: [(path: CGPath, width: CGFloat)] = []
        var flowers: [(center: CGPoint, radius: CGFloat, fill: CGColor, rotation: CGFloat)] = []
        var buds: [(center: CGPoint, radius: CGFloat)] = []

        func grow(from start: CGPoint, angle: CGFloat, length: CGFloat, width: CGFloat, depth: Int) {
            let end = CGPoint(x: start.x + cos(angle) * length, y: start.y + sin(angle) * length)
            let normal = CGPoint(x: -sin(angle), y: cos(angle))
            let bend = CGFloat.random(in: -0.28...0.28, using: &rng) * length
            let c1 = CGPoint(
                x: start.x + cos(angle) * length * 0.33 + normal.x * bend,
                y: start.y + sin(angle) * length * 0.33 + normal.y * bend
            )
            let c2 = CGPoint(
                x: start.x + cos(angle) * length * 0.66 + normal.x * bend * 0.6,
                y: start.y + sin(angle) * length * 0.66 + normal.y * bend * 0.6
            )
            let path = CGMutablePath()
            path.move(to: start)
            path.addCurve(to: end, control1: c1, control2: c2)
            segments.append((path, width))

            // Blossoms cluster on the thinner growth.
            if depth >= 1 {
                let count = Int(length / 22) + 1
                for _ in 0..<count {
                    let t = CGFloat.random(in: 0.15...1.0, using: &rng)
                    let along = CGPoint(x: start.x + (end.x - start.x) * t, y: start.y + (end.y - start.y) * t)
                    let side = CGFloat.random(in: -9...9, using: &rng)
                    let center = CGPoint(x: along.x + normal.x * side, y: along.y + normal.y * side)
                    if Double.random(in: 0...1, using: &rng) < 0.22 {
                        buds.append((center, CGFloat.random(in: 3.2...4.6, using: &rng)))
                    } else {
                        flowers.append((
                            center,
                            CGFloat.random(in: 7.5...14.5, using: &rng),
                            SakuraPalette.flowerFills.randomElement(using: &rng)!,
                            CGFloat.random(in: 0...(2 * .pi), using: &rng)
                        ))
                    }
                }
            }

            guard depth < 4, length > 18 else {
                flowers.append((end, CGFloat.random(in: 9...13, using: &rng),
                                SakuraPalette.flowerFills.randomElement(using: &rng)!,
                                CGFloat.random(in: 0...(2 * .pi), using: &rng)))
                return
            }

            // Main continuation.
            grow(from: end, angle: angle + CGFloat.random(in: -0.32...0.22, using: &rng),
                 length: length * 0.74, width: width * 0.68, depth: depth + 1)

            // Side shoots.
            let shoots = depth == 0 ? 2 : (Double.random(in: 0...1, using: &rng) < 0.75 ? 1 : 0)
            for _ in 0..<shoots {
                let sign: CGFloat = Bool.random(using: &rng) ? 1 : -1
                let t = CGFloat.random(in: 0.35...0.85, using: &rng)
                let origin = CGPoint(x: start.x + (end.x - start.x) * t, y: start.y + (end.y - start.y) * t)
                grow(from: origin, angle: angle + sign * CGFloat.random(in: 0.55...1.05, using: &rng),
                     length: length * 0.58, width: width * 0.5, depth: depth + 1)
            }
        }

        // Enter from just outside the top-right corner, heading left and slightly down.
        let start = CGPoint(x: size.width + 14, y: size.height - 26)
        grow(from: start, angle: .pi + 0.16, length: size.width * 0.34, width: 7.5, depth: 0)

        // Wood, thick to thin so thin segments sit on top of thick ones.
        for segment in segments.sorted(by: { $0.width > $1.width }) {
            let shape = CAShapeLayer()
            shape.path = segment.path
            shape.strokeColor = SakuraPalette.branchWood
            shape.fillColor = nil
            shape.lineWidth = max(1.2, segment.width)
            shape.lineCap = .round
            shape.lineJoin = .round
            root.addSublayer(shape)
        }

        for bud in buds {
            let shape = CAShapeLayer()
            shape.path = CGPath(ellipseIn: CGRect(x: -bud.radius, y: -bud.radius,
                                                  width: bud.radius * 2, height: bud.radius * 2), transform: nil)
            shape.fillColor = SakuraPalette.bud
            shape.position = bud.center
            root.addSublayer(shape)
        }

        for flower in flowers {
            let petals = CAShapeLayer()
            petals.path = flowerPath(radius: flower.radius)
            petals.fillColor = flower.fill
            petals.strokeColor = SakuraPalette.flowerEdge
            petals.lineWidth = 0.6
            petals.position = flower.center
            petals.setAffineTransform(CGAffineTransform(rotationAngle: flower.rotation))
            root.addSublayer(petals)

            let center = CAShapeLayer()
            let r = flower.radius * 0.18
            center.path = CGPath(ellipseIn: CGRect(x: -r, y: -r, width: r * 2, height: r * 2), transform: nil)
            center.fillColor = SakuraPalette.flowerCenter
            center.position = flower.center
            root.addSublayer(center)
        }

        root.shadowColor = SakuraPalette.rgba(0.25, 0.05, 0.1, 1)
        root.shadowOpacity = 0.28
        root.shadowRadius = 5
        root.shadowOffset = CGSize(width: 0, height: -2)
        root.shouldRasterize = true
        return root
    }
}
