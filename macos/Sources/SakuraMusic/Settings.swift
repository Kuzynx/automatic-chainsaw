import Foundation

/// User preferences, persisted in UserDefaults. All mutations fire `onChange`.
final class Settings {
    static let shared = Settings()

    enum PetalDensity: Int, CaseIterable {
        case off = 0, light, normal, heavy

        var title: String {
            switch self {
            case .off: return "Off"
            case .light: return "Light breeze"
            case .normal: return "Gentle fall"
            case .heavy: return "Full bloom"
            }
        }

        /// Multiplier applied to the emitter's base birth rate.
        var multiplier: Float {
            switch self {
            case .off: return 0
            case .light: return 0.45
            case .normal: return 1
            case .heavy: return 2.2
            }
        }
    }

    enum Blush: Int, CaseIterable {
        case off = 0, soft, deep

        var title: String {
            switch self {
            case .off: return "Off"
            case .soft: return "Soft"
            case .deep: return "Deep"
            }
        }

        var opacity: Float {
            switch self {
            case .off: return 0
            case .soft: return 0.55
            case .deep: return 1
            }
        }
    }

    private enum Key {
        static let enabled = "enabled"
        static let petals = "petalDensity"
        static let blush = "blush"
        static let branch = "showBranch"
        static let alwaysShow = "alwaysShow"
    }

    private let defaults = UserDefaults.standard
    var onChange: (() -> Void)?

    private init() {
        defaults.register(defaults: [
            Key.enabled: true,
            Key.petals: PetalDensity.normal.rawValue,
            Key.blush: Blush.soft.rawValue,
            Key.branch: true,
            Key.alwaysShow: false,
        ])
    }

    var enabled: Bool {
        get { defaults.bool(forKey: Key.enabled) }
        set { defaults.set(newValue, forKey: Key.enabled); onChange?() }
    }

    var petals: PetalDensity {
        get { PetalDensity(rawValue: defaults.integer(forKey: Key.petals)) ?? .normal }
        set { defaults.set(newValue.rawValue, forKey: Key.petals); onChange?() }
    }

    var blush: Blush {
        get { Blush(rawValue: defaults.integer(forKey: Key.blush)) ?? .soft }
        set { defaults.set(newValue.rawValue, forKey: Key.blush); onChange?() }
    }

    var showBranch: Bool {
        get { defaults.bool(forKey: Key.branch) }
        set { defaults.set(newValue, forKey: Key.branch); onChange?() }
    }

    /// Keep the overlay up even when another app's window overlaps Music.
    var alwaysShow: Bool {
        get { defaults.bool(forKey: Key.alwaysShow) }
        set { defaults.set(newValue, forKey: Key.alwaysShow); onChange?() }
    }
}
