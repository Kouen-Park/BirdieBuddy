import Foundation
import MetricKit

/// One stored diagnostic payload on disk.
struct DiagnosticReport: Identifiable, Equatable {
    let id: String
    let url: URL
    let capturedAt: Date

    var fileName: String { url.lastPathComponent }
}

/// Where MetricKit payloads are kept. Free of actor isolation because MetricKit's
/// callbacks are not guaranteed to arrive on the main thread.
enum DiagnosticStore {
    static let retainedReportCount = 10

    static var directory: URL? {
        guard let base = try? FileManager.default.url(
            for: .applicationSupportDirectory, in: .userDomainMask,
            appropriateFor: nil, create: true) else { return nil }
        let directory = base.appendingPathComponent("Diagnostics", isDirectory: true)
        if !FileManager.default.fileExists(atPath: directory.path) {
            try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        }
        return directory
    }

    static func store(_ payload: Data, capturedAt: Date) {
        guard let directory else { return }
        let stamp = fileStampFormatter.string(from: capturedAt)
        try? payload.write(to: directory.appendingPathComponent("diagnostic-\(stamp).json"), options: .atomic)
        prune()
    }

    static func storedReports() -> [DiagnosticReport] {
        guard let directory,
              let urls = try? FileManager.default.contentsOfDirectory(
                at: directory, includingPropertiesForKeys: [.contentModificationDateKey]) else { return [] }
        return urls
            .filter { $0.pathExtension == "json" }
            .map { url in
                let modified = (try? url.resourceValues(forKeys: [.contentModificationDateKey]))?
                    .contentModificationDate ?? .distantPast
                return DiagnosticReport(id: url.lastPathComponent, url: url, capturedAt: modified)
            }
            .sorted { $0.capturedAt > $1.capturedAt }
    }

    static func deleteAll() {
        storedReports().forEach { try? FileManager.default.removeItem(at: $0.url) }
    }

    private static func prune() {
        storedReports().dropFirst(retainedReportCount).forEach {
            try? FileManager.default.removeItem(at: $0.url)
        }
    }

    private static let fileStampFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(identifier: "UTC")
        formatter.dateFormat = "yyyyMMdd-HHmmss"
        return formatter
    }()
}

/// Captures crash, hang, CPU, and disk-write diagnostics through MetricKit and keeps
/// them on the device, so a beta tester can hand over an actual report instead of
/// "it just closed".
///
/// MetricKit delivers a payload on a launch AFTER the event, at most once a day, so a
/// report appears on a later cold start rather than immediately. Nothing leaves the
/// device: payloads are written to Application Support and shared only when the golfer
/// chooses to. Xcode's Organizer stays the aggregated view for App Store builds. If
/// these payloads are ever uploaded automatically, the privacy manifest must declare
/// crash-data collection first.
///
/// This deliberately adds no third-party SDK, matching the client's no-dependency rule.
@MainActor
final class CrashDiagnosticsReporter: ObservableObject {
    static let shared = CrashDiagnosticsReporter()

    @Published private(set) var reports: [DiagnosticReport] = []

    private let subscriber = DiagnosticSubscriber()

    private init() {}

    /// Call once at launch.
    func start() {
        subscriber.onStored = {
            Task { @MainActor in CrashDiagnosticsReporter.shared.reloadReports() }
        }
        MXMetricManager.shared.add(subscriber)
        reloadReports()
    }

    func reloadReports() {
        reports = DiagnosticStore.storedReports()
    }

    func deleteAll() {
        DiagnosticStore.deleteAll()
        reloadReports()
    }
}

/// MetricKit requires an `NSObject` subscriber, so its Objective-C surface is kept apart
/// from the observable model above.
private final class DiagnosticSubscriber: NSObject, MXMetricManagerSubscriber {
    var onStored: (@Sendable () -> Void)?

    func didReceive(_ payloads: [MXMetricPayload]) {
        // Performance metrics are not consumed yet; crash and hang diagnostics are.
    }

    func didReceive(_ payloads: [MXDiagnosticPayload]) {
        var stored = false
        for payload in payloads {
            let hasDiagnostics = payload.crashDiagnostics?.isEmpty == false
                || payload.hangDiagnostics?.isEmpty == false
                || payload.cpuExceptionDiagnostics?.isEmpty == false
                || payload.diskWriteExceptionDiagnostics?.isEmpty == false
            guard hasDiagnostics else { continue }
            DiagnosticStore.store(payload.jsonRepresentation(), capturedAt: payload.timeStampEnd)
            stored = true
        }
        if stored { onStored?() }
    }
}
