// swift-tools-version: 6.2
import PackageDescription

let package = Package(
    name: "ADOFAIModManager",
    defaultLocalization: "ko",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "ADOFAIModManager", targets: ["ADOFAIModManager"])
    ],
    targets: [
        .executableTarget(
            name: "ADOFAIModManager",
            resources: [.process("Resources")]
        ),
        .testTarget(
            name: "ADOFAIModManagerTests",
            dependencies: ["ADOFAIModManager"]
        )
    ]
)
