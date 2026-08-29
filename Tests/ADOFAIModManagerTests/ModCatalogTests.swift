import Foundation
import Testing
@testable import ADOFAIModManager

@Test func remoteModDecodingAcceptsMissingOptionalFields() throws {
    let data = Data(#"[{"id":"one","name":"Example","uploadedTimestamp":123,"parsedDownload":"https://example.com/mod.zip"}]"#.utf8)
    let mods = try JSONDecoder().decode([RemoteMod].self, from: data)

    #expect(mods.count == 1)
    #expect(mods[0].name == "Example")
    #expect(mods[0].version == nil)
    #expect(mods[0].description.isEmpty)
    #expect(!mods[0].hideFromSearch)
}

@Test func remoteModClassifiesDirectWebsiteAndAmbiguousLinks() {
    #expect(remoteMod(url: "https://example.com/releases/mod.ZIP").action == .downloadAndInstall)
    #expect(remoteMod(url: "https://github.com/example/mod/releases/tag/v1").action == .openWebsite)
    #expect(remoteMod(url: "https://youtu.be/example").action == .openWebsite)
    #expect(remoteMod(url: "https://drive.google.com/uc?id=example").action == .downloadAndInspect)
}

@Test func remoteModSearchesNameAuthorAndDescription() {
    let mod = RemoteMod(
        id: "one",
        name: "Rhythm Helper",
        description: "Improves editor timing",
        cachedUsername: "ADOFAI Creator"
    )

    #expect(mod.matches(searchQuery: "rhythm"))
    #expect(mod.matches(searchQuery: "creator"))
    #expect(mod.matches(searchQuery: "TIMING"))
    #expect(!mod.matches(searchQuery: "unrelated"))
}

@Test func catalogTemporaryFilesAreRemovedOnlyFromOwnedDirectory() throws {
    let owned = try CatalogDownloadStorage.destinationURL()
    try Data([0x50, 0x4B, 0x03, 0x04]).write(to: owned)
    CatalogDownloadStorage.remove(owned)
    #expect(!FileManager.default.fileExists(atPath: owned.path))

    let external = FileManager.default.temporaryDirectory
        .appending(path: "external-\(UUID().uuidString).zip")
    try Data().write(to: external)
    defer { try? FileManager.default.removeItem(at: external) }
    CatalogDownloadStorage.remove(external)
    #expect(FileManager.default.fileExists(atPath: external.path))
}

@Test func catalogUIExplicitlyIdentifiesWebsiteActions() throws {
    let root = URL(filePath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
    let source = try String(
        contentsOf: root.appending(path: "Sources/ADOFAIModManager/Features/Catalog/ModCatalogView.swift"),
        encoding: .utf8
    )

    #expect(source.contains("웹사이트에서 받기"))
    #expect(source.contains("arrow.up.right.square"))
    #expect(source.contains("기본 웹 브라우저에서 열립니다."))
    #expect(source.contains("모드 다운로드 웹사이트 열기"))
    #expect(source.contains("HStack(spacing: 0)"))
    #expect(source.contains("frame(width: LayoutMetrics.catalogListWidth)"))
    #expect(source.contains("padding(.leading, LayoutMetrics.compact)"))
    #expect(!source.contains("HSplitView"))
}

@Test func catalogClientFiltersHiddenModsAndSortsNewestFirst() async throws {
    let endpoint = URL(string: "https://catalog-test.example/mods")!
    let json = Data(#"""
    [
        {"id":"old","name":"Old","uploadedTimestamp":10,"parsedDownload":"https://example.com/old.zip"},
        {"id":"hidden","name":"Hidden","uploadedTimestamp":30,"hideFromSearch":true,"parsedDownload":"https://example.com/hidden.zip"},
        {"id":"new","name":"New","uploadedTimestamp":20,"parsedDownload":"https://example.com/new.zip"}
    ]
    """#.utf8)
    StubURLProtocol.register(url: endpoint, data: json)
    let client = ModCatalogClient(session: stubSession(), endpoint: endpoint)

    let mods = try await client.fetchMods()
    #expect(mods.map(\.id) == ["new", "old"])
}

@Test func catalogClientAcceptsZipSignatureAndRejectsHTML() async throws {
    let zipURL = URL(string: "https://download-test.example/mod.zip")!
    let htmlURL = URL(string: "https://download-test.example/release")!
    StubURLProtocol.register(url: zipURL, data: Data([0x50, 0x4B, 0x03, 0x04, 0x00]))
    StubURLProtocol.register(url: htmlURL, data: Data("<html></html>".utf8), headers: ["Content-Type": "text/html"])
    let client = ModCatalogClient(session: stubSession())

    let downloaded = try await client.download(remoteMod(url: zipURL.absoluteString))
    defer { CatalogDownloadStorage.remove(downloaded) }
    #expect(FileManager.default.fileExists(atPath: downloaded.path))

    await #expect(throws: ModCatalogError.notZip) {
        try await client.download(remoteMod(url: htmlURL.absoluteString))
    }
}

@Test func discordMessageParserBuildsNativeBlocks() {
    let message = """
    ## Features
    - **Fast** loading
    - `Safe` install

    -# Send feedback in the thread

    > ⚠️ **Version 2 or newer**

    ```cs
    public class Mod {}
    ```

    | Option | Description |
    |---|---|
    | Enabled | Turns it on |
    """

    #expect(DiscordMessageParser.parse(message) == [
        .heading(level: 2, text: "Features"),
        .unorderedList(["**Fast** loading", "`Safe` install"]),
        .subtext("Send feedback in the thread"),
        .quote([.paragraph("⚠️ **Version 2 or newer**")]),
        .codeBlock(language: "cs", code: "public class Mod {}"),
        .table([
            ["Option", "Description"],
            ["Enabled", "Turns it on"]
        ])
    ])
}

@Test func discordInlineParserFormatsTokensAndKeepsMalformedSource() {
    let rendered = DiscordInlineParser.parse(
        "**Bold** ~~old~~ __under__ ||spoiler|| <@123> <:party:456> <https://example.com>"
    )
    #expect(String(rendered.characters) == "Bold old under spoiler @user :party: https://example.com")
    #expect(rendered.runs.contains { $0.inlinePresentationIntent?.contains(.stronglyEmphasized) == true })
    #expect(rendered.runs.contains { $0.strikethroughStyle != nil })
    #expect(rendered.runs.contains { $0.underlineStyle != nil })
    #expect(rendered.runs.contains { $0.link == URL(string: "https://example.com") })

    let malformed = DiscordInlineParser.parse("unfinished **bold and `code")
    #expect(String(malformed.characters) == "unfinished **bold and `code")
}

private func remoteMod(url: String) -> RemoteMod {
    RemoteMod(id: UUID().uuidString, name: "Example", parsedDownload: url)
}

private func stubSession() -> URLSession {
    let configuration = URLSessionConfiguration.ephemeral
    configuration.protocolClasses = [StubURLProtocol.self]
    return URLSession(configuration: configuration)
}

private final class StubURLProtocol: URLProtocol, @unchecked Sendable {
    private struct Stub: Sendable {
        let statusCode: Int
        let headers: [String: String]
        let data: Data
    }

    private static let lock = NSLock()
    nonisolated(unsafe) private static var stubs: [URL: Stub] = [:]

    static func register(
        url: URL,
        statusCode: Int = 200,
        data: Data,
        headers: [String: String] = ["Content-Type": "application/octet-stream"]
    ) {
        lock.withLock {
            stubs[url] = Stub(statusCode: statusCode, headers: headers, data: data)
        }
    }

    override class func canInit(with request: URLRequest) -> Bool { true }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let url = request.url,
              let stub = Self.lock.withLock({ Self.stubs[url] }) else {
            client?.urlProtocol(self, didFailWithError: URLError(.resourceUnavailable))
            return
        }
        let response = HTTPURLResponse(
            url: url,
            statusCode: stub.statusCode,
            httpVersion: "HTTP/1.1",
            headerFields: stub.headers
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: stub.data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}
