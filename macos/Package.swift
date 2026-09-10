// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "SakuraMusic",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "SakuraMusic",
            path: "Sources/SakuraMusic",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("QuartzCore"),
                .linkedFramework("ServiceManagement"),
            ]
        )
    ]
)
