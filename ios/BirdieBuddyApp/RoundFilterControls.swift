import SwiftUI

/// Preset ranges instead of two free-form date pickers. The server takes
/// `from`/`to`, but a golfer thinks in "last 30 days", not in two dates.
enum FilterPeriod: String, CaseIterable, Identifiable {
    case all, month, quarter, year

    var id: String { rawValue }

    var label: LocalizedStringKey {
        switch self {
        case .all: return "All time"
        case .month: return "30 days"
        case .quarter: return "90 days"
        case .year: return "1 year"
        }
    }

    var days: Int? {
        switch self {
        case .all: return nil
        case .month: return 30
        case .quarter: return 90
        case .year: return 365
        }
    }
}

/// The filter selection shared by the round list and the statistics overview, so
/// both screens offer the same choices and build the same server query.
struct RoundFilterSelection: Equatable {
    var holeCount: Int?
    var period: FilterPeriod = .all
    var courseId: Int?

    var asFilter: RoundFilter {
        var filter = RoundFilter()
        filter.courseId = courseId
        filter.holeCount = holeCount
        if let days = period.days {
            filter.from = Calendar.current.date(byAdding: .day, value: -days, to: .now)
        }
        return filter
    }
}

struct RoundFilterControls: View {
    @Binding var selection: RoundFilterSelection
    let courses: [CourseSummary]
    let isUpdating: Bool

    var body: some View {
        Picker("Round length", selection: $selection.holeCount) {
            Text("All").tag(Optional<Int>.none)
            Text("9 holes").tag(Optional(9))
            Text("18 holes").tag(Optional(18))
        }
        Picker("Period", selection: $selection.period) {
            ForEach(FilterPeriod.allCases) { option in
                Text(option.label).tag(option)
            }
        }
        if !courses.isEmpty {
            Picker("Course", selection: $selection.courseId) {
                Text("All").tag(Optional<Int>.none)
                ForEach(courses) { course in
                    Text(course.name).tag(Optional(course.id))
                }
            }
        }
        if isUpdating {
            HStack {
                ProgressView().controlSize(.small)
                Text("Updating…").foregroundStyle(.secondary)
            }
        }
    }
}
