#!/usr/bin/env swift
import AppKit

guard CommandLine.arguments.count == 2 else {
    fatalError("Usage: generate-assets.swift <output-directory>")
}

let output = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

func pngData(_ image: NSImage) -> Data {
    guard
        let tiff = image.tiffRepresentation,
        let bitmap = NSBitmapImageRep(data: tiff),
        let data = bitmap.representation(using: .png, properties: [:])
    else { fatalError("Could not render image") }
    return data
}

func writePNG(_ image: NSImage, named name: String) throws {
    let data = pngData(image)
    try data.write(to: output.appendingPathComponent(name), options: .atomic)
}

func resized(_ image: NSImage, to pixels: Int) -> NSImage {
    let size = NSSize(width: pixels, height: pixels)
    return NSImage(size: size, flipped: false) { rect in
        NSGraphicsContext.current?.imageInterpolation = .high
        image.draw(in: rect, from: NSRect(origin: .zero, size: image.size), operation: .copy, fraction: 1)
        return true
    }
}

func appendUInt32(_ value: UInt32, to data: inout Data) {
    var bigEndian = value.bigEndian
    withUnsafeBytes(of: &bigEndian) { data.append(contentsOf: $0) }
}

func writeICNS(_ image: NSImage, named name: String) throws {
    let variants: [(String, Int)] = [
        ("ic04", 16), ("ic05", 32), ("ic06", 64),
        ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024)
    ]
    let chunks = variants.map { type, pixels in (type, pngData(resized(image, to: pixels))) }
    let totalLength = 8 + chunks.reduce(0) { $0 + 8 + $1.1.count }
    var data = Data("icns".utf8)
    appendUInt32(UInt32(totalLength), to: &data)
    for (type, payload) in chunks {
        data.append(contentsOf: type.utf8)
        appendUInt32(UInt32(payload.count + 8), to: &data)
        data.append(payload)
    }
    try data.write(to: output.appendingPathComponent(name), options: .atomic)
}

func symbol(_ name: String, pointSize: CGFloat, weight: NSFont.Weight, color: NSColor) -> NSImage {
    let configuration = NSImage.SymbolConfiguration(pointSize: pointSize, weight: weight)
        .applying(NSImage.SymbolConfiguration(paletteColors: [color]))
    return NSImage(systemSymbolName: name, accessibilityDescription: nil)!
        .withSymbolConfiguration(configuration)!
}

let projectRoot = URL(fileURLWithPath: #filePath)
    .deletingLastPathComponent()
    .deletingLastPathComponent()
let iconMasterURL = projectRoot.appending(path: "Resources/AppIconMaster.png")
guard let iconMaster = NSImage(contentsOf: iconMasterURL) else {
    fatalError("Could not load app icon master at \(iconMasterURL.path)")
}
let icon = resized(iconMaster, to: 1024)
try writePNG(icon, named: "AppIcon-1024.png")
try writeICNS(icon, named: "AppIcon.icns")

let background = NSImage(size: NSSize(width: 660, height: 400), flipped: false) { rect in
    NSColor(calibratedWhite: 0.965, alpha: 1).setFill()
    rect.fill()

    // Draw the arrow as one filled shape so translucent strokes never overlap
    // and create a darker joint at the arrowhead.
    let arrow = NSBezierPath()
    arrow.move(to: NSPoint(x: 278, y: 199))
    arrow.line(to: NSPoint(x: 358, y: 199))
    arrow.line(to: NSPoint(x: 358, y: 186))
    arrow.line(to: NSPoint(x: 386, y: 205))
    arrow.line(to: NSPoint(x: 358, y: 224))
    arrow.line(to: NSPoint(x: 358, y: 211))
    arrow.line(to: NSPoint(x: 278, y: 211))
    arrow.close()
    NSColor(calibratedWhite: 0.48, alpha: 0.72).setFill()
    arrow.fill()
    return true
}
try writePNG(background, named: "dmg-background.png")
