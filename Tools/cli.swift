// Wersja terminalowa (do testów): fitfix wejście.fit wyjście.fit [strefa]
import Foundation

@main
struct CLI {
    static func main() {
        let a = CommandLine.arguments
        guard a.count >= 3 else { print("użycie: fitfix wejście.fit wyjście.fit [strefa]"); exit(2) }
        do {
            let tz = a.count > 3 ? TimeZone(identifier: a[3])! : .current
            let (data, r) = try patchFit(try Data(contentsOf: URL(fileURLWithPath: a[1])), timeZone: tz)
            try data.write(to: URL(fileURLWithPath: a[2]))
            print("ok source=\(r.sourceName) records=\(r.records) hr=\(r.heartRateSamples) power=\(r.powerSamples) addedCreator=\(r.addedCreatorDevice) fixedLocal=\(r.fixedLocalTimestamp)")
        } catch {
            print("błąd: \(error.localizedDescription)"); exit(1)
        }
    }
}
