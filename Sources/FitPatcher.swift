// FitPatcher — przygotowuje plik FIT z MyWhoosh tak, żeby Garmin Connect
// potraktował go jak jazdę z Garmin Forerunner 965 i policzył obciążenie.
//
// Zmiany:
//  1. file_id (0):       manufacturer = 1 (Garmin), product = 4315 (Forerunner 965)
//  2. device_info (23):  to samo dla wpisu device_index = 0 („creator”);
//                        jeśli takiego wpisu nie ma, jest dodawany zaraz po file_id
//  3. activity (34):     local_timestamp = timestamp + przesunięcie strefy czasowej
//                        (MyWhoosh zapisuje tam błędną wartość)
//
// Wszystko inne — próbki, okrążenia, sesja, pola deweloperskie — jest kopiowane
// bajt w bajt. Na końcu przeliczany jest rozmiar danych, CRC nagłówka i CRC pliku,
// a wynik jest ponownie wczytywany i porównywany z oryginałem.
//
// Tylko Foundation, bez UI — ten plik można przenieść 1:1 na inną platformę.

import Foundation

struct FitReport {
    var sourceManufacturer: Int?
    var sourceProduct: Int?
    var records = 0
    var heartRateSamples = 0
    var powerSamples = 0
    var addedCreatorDevice = false
    var fixedLocalTimestamp = false
    var startDate: Date?                         // time_created z file_id
    var activityName: String?                    // workout.wkt_name / sport.name / session.sport_profile_name

    var sourceName: String {
        switch sourceManufacturer {
        case nil: return "Nieznane źródło"
        case 331: return "MyWhoosh"
        case 1: return sourceProduct == Int(Target.product) ? "Garmin Forerunner 965" : "Garmin"
        case 32: return "Wahoo"
        case 89: return "Tacx"
        case 260: return "Zwift"
        case let m?: return "Producent \(m)"
        }
    }
}

enum FitError: LocalizedError {
    case notFit, damaged, notActivity, verificationFailed

    var errorDescription: String? {
        switch self {
        case .notFit: return "To nie jest plik FIT."
        case .damaged: return "Plik FIT jest ucięty lub uszkodzony."
        case .notActivity: return "To nie jest plik z treningiem."
        case .verificationFailed: return "Poprawiony plik nie przeszedł weryfikacji — nic nie zapisano."
        }
    }
}

private enum Target {
    static let manufacturer: UInt64 = 1     // Garmin
    static let product: UInt64 = 4315       // Forerunner 965
}

/// Numery komunikatów i pól z profilu FIT.
private enum Msg {
    static let fileId: UInt16 = 0, record: UInt16 = 20, deviceInfo: UInt16 = 23, activity: UInt16 = 34
}

// MARK: - Model strumienia FIT

private struct Field { var num: UInt8, size: UInt8, baseType: UInt8 }

private struct Definition {
    var bigEndian: Bool
    var global: UInt16
    var fields: [Field]
    var devFields: [Field]          // (numer, rozmiar, indeks danych deweloperskich)

    var fieldsSize: Int { fields.reduce(0) { $0 + Int($1.size) } }
    var dataSize: Int { fieldsSize + devFields.reduce(0) { $0 + Int($1.size) } }

    func range(of num: UInt8) -> Range<Int>? {
        var offset = 0
        for f in fields {
            if f.num == num { return offset..<offset + Int(f.size) }
            offset += Int(f.size)
        }
        return nil
    }

    func encoded(localType: UInt8) -> [UInt8] {
        var b: [UInt8] = [0x40 | (devFields.isEmpty ? 0 : 0x20) | localType, 0, bigEndian ? 1 : 0]
        b += bigEndian ? [UInt8(global >> 8), UInt8(global & 0xFF)] : [UInt8(global & 0xFF), UInt8(global >> 8)]
        b.append(UInt8(fields.count))
        for f in fields { b += [f.num, f.size, f.baseType] }
        if !devFields.isEmpty {
            b.append(UInt8(devFields.count))
            for f in devFields { b += [f.num, f.size, f.baseType] }
        }
        return b
    }
}

/// Komunikat danych razem z definicją, która go opisuje.
private struct Message {
    var header: UInt8
    var def: Definition
    var body: [UInt8]

    func uint(_ num: UInt8) -> UInt64? {
        guard let r = def.range(of: num), r.count <= 8 else { return nil }
        var v: UInt64 = 0
        for (i, byte) in body[r].enumerated() {
            v |= UInt64(byte) << (8 * UInt64(def.bigEndian ? r.count - 1 - i : i))
        }
        let invalid: UInt64 = r.count == 8 ? .max : (1 << (8 * UInt64(r.count))) - 1
        return v == invalid ? nil : v
    }

    mutating func setUInt(_ num: UInt8, _ value: UInt64) {
        guard let r = def.range(of: num), r.count <= 8 else { return }
        for i in 0..<r.count {
            body[r.lowerBound + (def.bigEndian ? r.count - 1 - i : i)] = UInt8(truncatingIfNeeded: value >> (8 * UInt64(i)))
        }
    }

    func string(_ num: UInt8) -> String? {
        guard let r = def.range(of: num) else { return nil }
        let bytes = body[r].prefix { $0 != 0 }
        let text = String(decoding: bytes, as: UTF8.self).trimmingCharacters(in: .whitespaces)
        return text.isEmpty ? nil : text
    }

    mutating func clear(_ num: UInt8) {          // pusty napis
        if let r = def.range(of: num) { for i in r { body[i] = 0 } }
    }
}

private enum Item {
    case definition(localType: UInt8, Definition)
    case message(Message)
}

private struct FitFile {
    var header: [UInt8]
    var items: [Item]
    var tail: [UInt8]               // dane za CRC (np. doklejony kolejny plik)
    var crcValid: Bool

    var messages: [Message] { items.compactMap { if case .message(let m) = $0 { return m } else { return nil } } }
}

// MARK: - Odczyt

private func readFit(_ bytes: [UInt8]) throws -> FitFile {
    guard bytes.count >= 14, bytes[0] == 12 || bytes[0] == 14,
          bytes[8..<12].elementsEqual(".FIT".utf8) else { throw FitError.notFit }
    let headerSize = Int(bytes[0])
    let dataSize = Int(bytes[4]) | Int(bytes[5]) << 8 | Int(bytes[6]) << 16 | Int(bytes[7]) << 24
    let end = headerSize + dataSize
    guard end + 2 <= bytes.count else { throw FitError.damaged }

    var defs: [UInt8: Definition] = [:]
    var items: [Item] = []
    var p = headerSize

    func take(_ n: Int) throws -> ArraySlice<UInt8> {
        guard p + n <= end else { throw FitError.damaged }
        defer { p += n }
        return bytes[p..<p + n]
    }
    func takeFields() throws -> [Field] {
        let count = Int(try take(1).first!)
        let raw = Array(try take(count * 3))
        return (0..<count).map { Field(num: raw[$0 * 3], size: raw[$0 * 3 + 1], baseType: raw[$0 * 3 + 2]) }
    }

    while p < end {
        let h = try take(1).first!
        let isCompressedTimestamp = h & 0x80 != 0
        if !isCompressedTimestamp && h & 0x40 != 0 {
            let fixed = Array(try take(4))           // reserved, architektura, numer globalny
            let big = fixed[1] == 1
            let global = big ? UInt16(fixed[2]) << 8 | UInt16(fixed[3]) : UInt16(fixed[3]) << 8 | UInt16(fixed[2])
            let fields = try takeFields()
            let dev = h & 0x20 != 0 ? try takeFields() : []
            let def = Definition(bigEndian: big, global: global, fields: fields, devFields: dev)
            defs[h & 0x0F] = def
            items.append(.definition(localType: h & 0x0F, def))
        } else {
            let localType = isCompressedTimestamp ? (h >> 5) & 0x03 : h & 0x0F
            guard let def = defs[localType] else { throw FitError.damaged }
            items.append(.message(Message(header: h, def: def, body: Array(try take(def.dataSize)))))
        }
    }

    let storedCRC = UInt16(bytes[end]) | UInt16(bytes[end + 1]) << 8
    return FitFile(header: Array(bytes[..<headerSize]), items: items,
                   tail: Array(bytes[(end + 2)...]), crcValid: fitCRC(bytes[..<end]) == storedCRC)
}

func fitCRC<C: Collection>(_ bytes: C) -> UInt16 where C.Element == UInt8 {
    let table: [UInt16] = [0x0000, 0xCC01, 0xD801, 0x1400, 0xF001, 0x3C00, 0x2800, 0xE401,
                           0xA001, 0x6C00, 0x7800, 0xB401, 0x5000, 0x9C01, 0x8801, 0x4400]
    var crc: UInt16 = 0
    for byte in bytes {
        for nibble in [byte & 0xF, byte >> 4] {
            let t = table[Int(crc & 0xF)]
            crc = (crc >> 4) ^ t ^ table[Int(nibble)]
        }
    }
    return crc
}

// MARK: - Poprawianie

/// Zwraca poprawioną kopię pliku i raport. `timeZone` służy do wyliczenia czasu lokalnego aktywności.
func patchFit(_ input: Data, timeZone: TimeZone = .current) throws -> (Data, FitReport) {
    let fit = try readFit([UInt8](input))
    let original = fit.messages

    let isActivity = original.contains { $0.def.global == Msg.record }
        && !original.contains { $0.def.global == Msg.fileId && ($0.uint(0) ?? 4) != 4 }
    guard isActivity else { throw FitError.notActivity }

    let hasCreator = original.contains { $0.def.global == Msg.deviceInfo && $0.uint(0) == 0 }
    var report = FitReport()
    // Nazwa aktywności: najpierw nazwa treningu, potem nazwa sportu, potem profil sesji.
    let nameSources: [(UInt16, UInt8)] = [(26, 8), (12, 3), (18, 110)]
    report.activityName = nameSources.lazy.compactMap { global, field in
        original.lazy.filter { $0.def.global == global }.compactMap { $0.string(field) }.first
    }.first
    var out = fit.header
    var usedLocalTypes = Set<UInt8>()
    var extraItems = 0
    // Pola producenta/produktu dopisane do definicji, w której ich brakowało.
    var widened: [UInt8: [UInt8]] = [:]         // typ lokalny → numery dopisanych pól

    for item in fit.items {
        switch item {
        case .definition(let localType, var def):
            let required: [UInt8] = def.global == Msg.fileId ? [1, 2] : def.global == Msg.deviceInfo ? [2, 4] : []
            let missing = required.filter { def.range(of: $0) == nil }
            def.fields += missing.map { Field(num: $0, size: 2, baseType: 0x84) }
            widened[localType] = missing.isEmpty ? nil : missing
            usedLocalTypes.insert(localType)
            out += def.encoded(localType: localType)

        case .message(var m):
            let localType = m.header & 0x80 != 0 ? (m.header >> 5) & 0x03 : m.header & 0x0F
            if let missing = widened[localType] {   // dopisane pola stoją przed polami deweloperskimi
                m.body.insert(contentsOf: [UInt8](repeating: 0xFF, count: missing.count * 2), at: m.def.fieldsSize)
                m.def.fields += missing.map { Field(num: $0, size: 2, baseType: 0x84) }
            }

            switch m.def.global {
            case Msg.fileId:
                if report.sourceManufacturer == nil {
                    report.sourceManufacturer = m.uint(1).map(Int.init)
                    report.sourceProduct = m.uint(2).map(Int.init)
                }
                if report.startDate == nil, let created = m.uint(4), created != 0xFFFF_FFFF {
                    report.startDate = Date(timeIntervalSince1970: TimeInterval(created) + 631_065_600)
                }
                m.setUInt(1, Target.manufacturer)
                m.setUInt(2, Target.product)
                m.clear(8)                          // product_name

            case Msg.deviceInfo where m.uint(0) == 0:
                m.setUInt(2, Target.manufacturer)
                m.setUInt(4, Target.product)
                m.clear(27)                         // product_name

            case Msg.activity:
                if let utc = m.uint(253), m.def.range(of: 5) != nil {
                    let unix = TimeInterval(utc) + 631_065_600      // epoka FIT: 1989-12-31
                    let offset = timeZone.secondsFromGMT(for: Date(timeIntervalSince1970: unix))
                    let local = UInt64(Int(utc) + offset)
                    if m.uint(5) != local {
                        m.setUInt(5, local)
                        report.fixedLocalTimestamp = true
                    }
                }

            case Msg.record:
                report.records += 1
                if let hr = m.uint(3), hr > 0 { report.heartRateSamples += 1 }
                if m.uint(7) != nil { report.powerSamples += 1 }

            default: break
            }
            out.append(m.header)
            out += m.body

            if m.def.global == Msg.fileId && !hasCreator && !report.addedCreatorDevice,
               let free = (0..<16).first(where: { !usedLocalTypes.contains($0) }) {
                // Typ lokalny nieużyty do tej pory: każde późniejsze użycie poprzedzi własna definicja.
                var def = Definition(bigEndian: false, global: Msg.deviceInfo, fields: [
                    Field(num: 0, size: 1, baseType: 0x02),         // device_index
                    Field(num: 2, size: 2, baseType: 0x84),         // manufacturer
                    Field(num: 4, size: 2, baseType: 0x84),         // product
                ], devFields: [])
                let created = m.uint(4)                              // time_created → timestamp
                if created != nil { def.fields.insert(Field(num: 253, size: 4, baseType: 0x86), at: 0) }
                var creator = Message(header: free, def: def, body: [UInt8](repeating: 0, count: def.dataSize))
                if let created { creator.setUInt(253, created) }
                creator.setUInt(0, 0)
                creator.setUInt(2, Target.manufacturer)
                creator.setUInt(4, Target.product)
                out += def.encoded(localType: free) + [creator.header] + creator.body
                report.addedCreatorDevice = true
                extraItems = 2
            }
        }
    }

    // Nagłówek: rozmiar danych i (dla nagłówka 14-bajtowego) jego CRC, potem CRC pliku.
    let dataSize = out.count - fit.header.count
    for i in 0..<4 { out[4 + i] = UInt8(truncatingIfNeeded: dataSize >> (8 * i)) }
    if fit.header.count == 14 {
        let crc = fitCRC(out[..<12])
        out[12] = UInt8(crc & 0xFF); out[13] = UInt8(crc >> 8)
    }
    let crc = fitCRC(out)
    out += [UInt8(crc & 0xFF), UInt8(crc >> 8)] + fit.tail

    try verify(out, against: fit, extraItems: extraItems, report: report)
    return (Data(out), report)
}

/// Wynik musi dać się wczytać, mieć poprawne CRC, urządzenie Garmina i nietknięte próbki.
private func verify(_ out: [UInt8], against original: FitFile, extraItems: Int, report: FitReport) throws {
    guard let result = try? readFit(out), result.crcValid,
          result.items.count == original.items.count + extraItems else { throw FitError.verificationFailed }
    let messages = result.messages
    let devicesOK = messages.filter { $0.def.global == Msg.fileId }.allSatisfy {
        $0.uint(1) == Target.manufacturer && $0.uint(2) == Target.product
    } && messages.contains {
        $0.def.global == Msg.deviceInfo && $0.uint(0) == 0
            && $0.uint(2) == Target.manufacturer && $0.uint(4) == Target.product
    }
    let untouched: (Message) -> Bool = { ![Msg.fileId, Msg.deviceInfo, Msg.activity].contains($0.def.global) }
    let samplesOK = messages.filter(untouched).map(\.body) == original.messages.filter(untouched).map(\.body)
    guard devicesOK, samplesOK else { throw FitError.verificationFailed }
}
