import SwiftUI
import AppKit
import UniformTypeIdentifiers

@main
struct WhooshToGarminApp: App {
    @NSApplicationDelegateAdaptor private var delegate: AppDelegate

    var body: some Scene {
        Window("Whoosh → Garmin", id: "main") {
            ContentView(model: delegate.model)
        }
        .windowResizability(.contentSize)
    }
}

/// Handles files dropped on the Dock icon / "Open With".
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let model = Model()
    func application(_ application: NSApplication, open urls: [URL]) { model.process(urls) }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

@MainActor
final class Model: ObservableObject {
    func process(_ urls: [URL]) {
        let failed = urls.filter { $0.pathExtension.lowercased() == "fit" }.contains { !Self.fix($0) }
        if failed { NSSound.beep() }
    }

    @discardableResult
    static func fix(_ url: URL) -> Bool {
        do {
            let (data, report) = try patchFit(try Data(contentsOf: url))
            let out = outputURL(in: url.deletingLastPathComponent(), name: report.activityName,
                                date: report.startDate ?? Date(), excluding: url)
            try data.write(to: out, options: .atomic)
            // The new file is safely written — permanently remove the original.
            if out.standardizedFileURL != url.standardizedFileURL {
                try? FileManager.default.removeItem(at: url)
            }
            return true
        } catch {
            return false
        }
    }

    /// "<activity name> - 2026-11-16 - fixed.fit"; appends (2), (3)… on collision
    static func outputURL(in dir: URL, name: String?, date: Date, excluding source: URL) -> URL {
        let f = DateFormatter()
        f.locale = Locale(identifier: "en_US_POSIX")
        f.dateFormat = "yyyy-MM-dd"
        let safe = name?.replacingOccurrences(of: "/", with: "-").replacingOccurrences(of: ":", with: "-")
        let base = [safe, f.string(from: date), "fixed"].compactMap { $0 }.joined(separator: " - ")
        var candidate = dir.appendingPathComponent("\(base).fit")
        var n = 2
        while FileManager.default.fileExists(atPath: candidate.path),
              candidate.standardizedFileURL != source.standardizedFileURL {
            candidate = dir.appendingPathComponent("\(base) (\(n)).fit")
            n += 1
        }
        return candidate
    }
}

struct ContentView: View {
    @ObservedObject var model: Model
    @State private var hovering = false

    var body: some View {
        VStack(spacing: 6) {
            Image(systemName: "arrow.down.doc").font(.system(size: 28, weight: .light))
            Text("Drop a MyWhoosh .fit file").font(.headline)
            Button("or choose…", action: pick).buttonStyle(.link)
        }
        .foregroundStyle(hovering ? Color.accentColor : .secondary)
        .frame(maxWidth: .infinity, minHeight: 140)
        .background(RoundedRectangle(cornerRadius: 12)
            .strokeBorder(style: StrokeStyle(lineWidth: 1.5, dash: [6]))
            .foregroundStyle(hovering ? Color.accentColor : Color.secondary.opacity(0.5)))
        .padding(20)
        .frame(width: 380)
        .fixedSize(horizontal: false, vertical: true)
        .contentShape(Rectangle())
        .dropDestination(for: URL.self) { urls, _ in
            model.process(urls)
            return true
        } isTargeted: { hovering = $0 }
    }

    private func pick() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [UTType(filenameExtension: "fit") ?? .data]
        panel.allowsMultipleSelection = true
        if panel.runModal() == .OK { model.process(panel.urls) }
    }
}
