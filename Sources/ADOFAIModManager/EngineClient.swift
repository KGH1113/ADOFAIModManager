import Foundation

enum EngineClientError: Error {
    case helperMissing
    case launchFailed(String)
    case invalidResponse(String)

    var userMessageKey: String {
        switch self {
        case .helperMissing:
            "앱에 필요한 구성 요소가 없습니다. 앱을 다시 설치해 주세요."
        case .launchFailed:
            "작업을 시작하지 못했습니다. 앱을 다시 열고 시도해 주세요."
        case .invalidResponse:
            "작업 결과를 확인하지 못했습니다. 다시 시도해 주세요."
        }
    }

    var diagnosticDescription: String {
        switch self {
        case .helperMissing:
            "UMMInstallerEngine is missing from the app bundle."
        case .launchFailed(let message):
            "UMMInstallerEngine launch failed: \(message)"
        case .invalidResponse(let message):
            "UMMInstallerEngine returned an invalid response: \(message)"
        }
    }
}

struct EngineClient: Sendable {
    func run(_ request: EngineRequest) async throws -> EngineResponse {
        try await Task.detached(priority: .userInitiated) {
            let executable = try Self.helperURL()
            let process = Process()
            let input = Pipe()
            let output = Pipe()
            let errors = Pipe()
            process.executableURL = executable
            process.standardInput = input
            process.standardOutput = output
            process.standardError = errors
            process.environment = ProcessInfo.processInfo.environment

            do {
                try process.run()
            } catch {
                throw EngineClientError.launchFailed(error.localizedDescription)
            }

            let body = try JSONEncoder().encode(request)
            try input.fileHandleForWriting.write(contentsOf: body)
            try input.fileHandleForWriting.close()
            let data = output.fileHandleForReading.readDataToEndOfFile()
            let errorData = errors.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()

            guard !data.isEmpty else {
                let detail = String(decoding: errorData, as: UTF8.self)
                throw EngineClientError.invalidResponse(detail.isEmpty ? "empty response" : detail)
            }

            do {
                return try JSONDecoder().decode(EngineResponse.self, from: data)
            } catch {
                let raw = String(decoding: data, as: UTF8.self)
                throw EngineClientError.invalidResponse("\(error.localizedDescription) — \(raw)")
            }
        }.value
    }

    private static func helperURL() throws -> URL {
        if let override = ProcessInfo.processInfo.environment["ADOFAI_ENGINE_PATH"] {
            return URL(filePath: override)
        }
        let bundled = Bundle.main.bundleURL
            .appending(path: "Contents/Helpers/UMMInstallerEngine")
        guard FileManager.default.isExecutableFile(atPath: bundled.path) else {
            throw EngineClientError.helperMissing
        }
        return bundled
    }
}
