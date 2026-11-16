// Command-line version (for testing): fitfix input.fit output.fit [time-zone]
import Foundation

@main
struct CLI {
    static func main() {
        let a = CommandLine.arguments
        guard a.count >= 3 else { print("usage: fitfix input.fit output.fit [time-zone]"); exit(2) }
        do {
            let tz = a.count > 3 ? TimeZone(identifier: a[3])! : .current
            let (data, r) = try patchFit(try Data(contentsOf: URL(fileURLWithPath: a[1])), timeZone: tz)
            try data.write(to: URL(fileURLWithPath: a[2]))
            print("ok source=\(r.sourceName) records=\(r.records) hr=\(r.heartRateSamples) power=\(r.powerSamples) addedCreator=\(r.addedCreatorDevice) fixedLocal=\(r.fixedLocalTimestamp) name=\(r.activityName ?? "-")")
        } catch {
            print("error: \(error.localizedDescription)"); exit(1)
        }
    }
}
