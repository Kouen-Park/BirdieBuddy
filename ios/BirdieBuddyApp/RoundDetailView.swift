import SwiftUI

struct RoundDetailView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    let round: RoundSummary

    @State private var detail: RoundDetail?
    @State private var statistics: RoundStatistics?
    @State private var courseTees: [CourseTee] = []
    @State private var editedDate = Date.now
    @State private var editedTeeId: Int?
    @State private var errorMessage: String?
    @State private var statusMessage: String?
    @State private var isSaving = false
    @State private var isConfirmingDelete = false

    private var holes: [Hole] {
        (detail?.holes ?? []).sorted { $0.holeNumber < $1.holeNumber }
    }

    private var frontNine: [Hole] { holes.filter { $0.holeNumber <= 9 } }
    private var backNine: [Hole] { holes.filter { $0.holeNumber > 9 } }

    private var hasChanges: Bool {
        guard let detail else { return false }
        let originalDate = APIClient.dayFormatter.date(from: detail.date)
        let dateChanged = originalDate.map {
            !Calendar.current.isDate($0, inSameDayAs: editedDate)
        } ?? true
        return dateChanged || editedTeeId != detail.courseTeeId
    }

    var body: some View {
        Group {
            if let detail {
                List {
                    summarySection(detail)
                    scorecardSection("Front nine", holes: frontNine)
                    scorecardSection("Back nine", holes: backNine)
                    parTypeSection
                    editSection(detail)
                    messageSections
                    deleteSection
                }
            } else if let errorMessage {
                ContentUnavailableView("Could not load round", systemImage: "exclamationmark.triangle",
                                       description: Text(errorMessage))
            } else {
                ProgressView("Loading round…")
            }
        }
        .navigationTitle(round.courseName)
        .alert("Delete this round?", isPresented: $isConfirmingDelete) {
            Button("Cancel", role: .cancel) {}
            Button("Delete", role: .destructive) { Task { await deleteRound() } }
        } message: {
            Text("This permanently removes the round and its hole scores.")
        }
        .task { await load() }
    }

    // MARK: - Sections

    private func summarySection(_ detail: RoundDetail) -> some View {
        Section("Summary") {
            LabeledContent("Score") {
                Text("\(round.totalScore) (\(StatisticsView.signedInt(round.scoreToPar)))")
                    .font(.headline)
            }
            LabeledContent("Date", value: detail.date)
            LabeledContent("Tee", value: detail.tee)
            LabeledContent("Holes", value: "\(holes.count) of \(detail.expectedHoles)")

            if let statistics {
                LabeledContent("Putts", value: "\(statistics.totalPutts)")
                LabeledContent("Putts per hole", value: StatisticsView.number(statistics.averagePuttsPerHole, digits: 2))
                LabeledContent("GIR", value: StatisticsView.percent(statistics.girPercentage))
                if let fairway = statistics.fairwayPercentage {
                    LabeledContent("Fairways hit", value: StatisticsView.percent(fairway))
                }
                LabeledContent("Penalties", value: "\(statistics.totalPenalties)")
                scoreSpread(statistics)
            }
        }
    }

    private func scoreSpread(_ statistics: RoundStatistics) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Score spread").font(.subheadline.bold())
            Text("Eagles \(statistics.eagles) · Birdies \(statistics.birdies) · Pars \(statistics.pars)")
                .font(.subheadline).foregroundStyle(.secondary)
            Text("Bogeys \(statistics.bogeys) · Double or worse \(statistics.doubleBogeysOrWorse)")
                .font(.subheadline).foregroundStyle(.secondary)
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    @ViewBuilder
    private func scorecardSection(_ title: LocalizedStringKey, holes: [Hole]) -> some View {
        if !holes.isEmpty {
            Section {
                ForEach(holes) { hole in
                    HoleRow(hole: hole)
                }
                let par = holes.reduce(0) { $0 + $1.par }
                let score = holes.reduce(0) { $0 + $1.score }
                let putts = holes.reduce(0) { $0 + $1.putts }
                HStack {
                    Text("Total").font(.subheadline.bold())
                    Spacer()
                    Text("Par \(par)").foregroundStyle(.secondary)
                    Text("\(score)").font(.subheadline.bold())
                    Text("\(putts) putts").foregroundStyle(.secondary)
                }
                .accessibilityElement(children: .combine)
            } header: {
                Text(title)
            }
        }
    }

    @ViewBuilder
    private var parTypeSection: some View {
        if let statistics {
            Section("By par type") {
                ParTypeRow(label: "Par 3", stats: statistics.par3)
                ParTypeRow(label: "Par 4", stats: statistics.par4)
                ParTypeRow(label: "Par 5", stats: statistics.par5)
            }
        }
    }

    private func editSection(_ detail: RoundDetail) -> some View {
        Section("Edit") {
            DatePicker("Date", selection: $editedDate, displayedComponents: .date)
                .accessibilityLabel("Round date")
            if courseTees.isEmpty {
                LabeledContent("Tee", value: detail.tee)
            } else {
                Picker("Tee", selection: $editedTeeId) {
                    ForEach(courseTees) { tee in
                        Text(tee.name).tag(Optional(tee.id))
                    }
                }
            }
            Button(isSaving ? "Saving…" : "Save changes") { Task { await save() } }
                .disabled(isSaving || !hasChanges)
                .accessibilityLabel("Save round changes")
        }
    }

    @ViewBuilder
    private var messageSections: some View {
        if let statusMessage {
            Section { Text(statusMessage).foregroundStyle(.secondary) }
        }
        if let errorMessage {
            Section {
                Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                    .foregroundStyle(.red)
            }
        }
    }

    private var deleteSection: some View {
        Section {
            Button("Delete round", role: .destructive) { isConfirmingDelete = true }
                .disabled(isSaving)
                .accessibilityLabel("Delete this round")
        }
    }

    // MARK: - Loading and actions

    private func load() async {
        do {
            let loaded = try await appState.api.round(id: round.id)
            detail = loaded
            editedDate = APIClient.dayFormatter.date(from: loaded.date) ?? .now
            editedTeeId = loaded.courseTeeId
            // Statistics and tees are enrichment: a failure here must not hide the
            // scorecard the golfer opened this screen for.
            statistics = try? await appState.api.roundStatistics(roundId: round.id)
            if let course = try? await appState.api.course(id: loaded.courseId) {
                courseTees = course.tees
            }
        } catch {
            errorMessage = AppState.message(for: error)
        }
    }

    private func save() async {
        guard let detail else { return }
        isSaving = true
        errorMessage = nil
        statusMessage = nil
        do {
            let teeName = courseTees.first { $0.id == editedTeeId }?.name
            try await appState.api.updateRound(
                id: detail.id,
                date: editedDate,
                courseTeeId: editedTeeId,
                tee: teeName,
                expectedUpdatedAt: detail.updatedAt)
            await load()
            statusMessage = "Round updated"
        } catch {
            errorMessage = AppState.message(for: error)
        }
        isSaving = false
    }

    private func deleteRound() async {
        isSaving = true
        errorMessage = nil
        if await appState.deleteRound(id: round.id) {
            dismiss()
        } else {
            errorMessage = appState.errorMessage
        }
        isSaving = false
    }
}

/// One scorecard line. Kept narrow enough for an iPhone SE: par and score are
/// numbers, everything else is a compact badge.
private struct HoleRow: View {
    let hole: Hole

    private var toPar: Int { hole.score - hole.par }

    private var toParLabel: String {
        if toPar == 0 { return "E" }
        return toPar > 0 ? "+\(toPar)" : "\(toPar)"
    }

    private var toParColour: Color {
        switch toPar {
        case ..<0: return .red
        case 0: return .primary
        default: return .secondary
        }
    }

    var body: some View {
        HStack(spacing: 10) {
            Text("\(hole.holeNumber)")
                .font(.subheadline.monospacedDigit())
                .frame(width: 22, alignment: .leading)
            Text("Par \(hole.par)")
                .font(.caption)
                .foregroundStyle(.secondary)
                .frame(width: 46, alignment: .leading)
            Text("\(hole.score)")
                .font(.body.bold().monospacedDigit())
                .frame(width: 26, alignment: .trailing)
            Text(toParLabel)
                .font(.caption.monospacedDigit())
                .foregroundStyle(toParColour)
                .frame(width: 26, alignment: .leading)
            Spacer(minLength: 0)
            Text("\(hole.putts)p")
                .font(.caption.monospacedDigit())
                .foregroundStyle(.secondary)
            if hole.gir {
                Image(systemName: "target").font(.caption).foregroundStyle(.green)
            }
            if hole.fairwayHit == true {
                Image(systemName: "arrow.up.forward").font(.caption).foregroundStyle(.green)
            }
            if hole.penalty > 0 {
                Text("+\(hole.penalty)")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.orange)
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Hole \(hole.holeNumber), par \(hole.par)")
        .accessibilityValue(accessibilitySummary)
    }

    private var accessibilitySummary: String {
        var parts = ["score \(hole.score)", "\(toParLabel) to par", "\(hole.putts) putts"]
        if hole.gir { parts.append("green in regulation") }
        if hole.fairwayHit == true { parts.append("fairway hit") }
        if hole.penalty > 0 { parts.append("\(hole.penalty) penalty strokes") }
        return parts.joined(separator: ", ")
    }
}

private struct ParTypeRow: View {
    let label: LocalizedStringKey
    let stats: ParTypeStats

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(label).font(.subheadline.bold())
            if stats.holesPlayed == 0 {
                Text("No holes played").font(.caption).foregroundStyle(.secondary)
            } else {
                Text("\(stats.holesPlayed) holes · avg \(StatisticsView.number(stats.averageScore, digits: 2)) · \(StatisticsView.signed(stats.averageScoreToPar)) to par")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }
}
