import SwiftUI

/// Native equivalents of the tokens in `wwwroot/css/styles.css`.
enum BirdieTheme {
    static let ink = Color(red: 16/255, green: 37/255, blue: 35/255)
    static let muted = Color(red: 96/255, green: 115/255, blue: 111/255)
    static let night = Color(red: 14/255, green: 33/255, blue: 31/255)
    static let fairway = Color(red: 44/255, green: 113/255, blue: 104/255)
    static let fairwayDark = Color(red: 32/255, green: 86/255, blue: 79/255)
    static let mint = Color(red: 220/255, green: 239/255, blue: 232/255)
    static let canvas = Color(red: 241/255, green: 245/255, blue: 242/255)
    static let paper = Color(red: 252/255, green: 253/255, blue: 251/255)
    static let sun = Color(red: 230/255, green: 184/255, blue: 92/255)
    static let flag = Color(red: 229/255, green: 106/255, blue: 78/255)
    static let sky = Color(red: 119/255, green: 169/255, blue: 189/255)
    static let iris = Color(red: 135/255, green: 146/255, blue: 223/255)
    static let chartPutts = Color(red: 90/255, green: 122/255, blue: 146/255)
    static let line = Color(red: 214/255, green: 225/255, blue: 220/255)

    static func body(_ size: CGFloat = 16, weight: Font.Weight = .regular) -> Font {
        let name: String
        switch weight {
        case .bold, .heavy, .black: name = "Outfit-Bold"
        case .semibold: name = "Outfit-SemiBold"
        case .medium: name = "Outfit-Medium"
        default: name = "Outfit-Regular"
        }
        return .custom(name, size: size, relativeTo: .body)
    }

    static func display(_ size: CGFloat = 28) -> Font {
        .custom("SpaceGrotesk-Bold", size: size, relativeTo: .largeTitle)
    }

    static func mono(_ size: CGFloat = 16) -> Font {
        .custom("DMMono-Medium", size: size, relativeTo: .body)
    }
}

private struct BirdieCard: ViewModifier {
    var padding: CGFloat

    func body(content: Content) -> some View {
        content
            .padding(padding)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(BirdieTheme.paper, in: RoundedRectangle(cornerRadius: 16))
            .overlay(RoundedRectangle(cornerRadius: 16).stroke(BirdieTheme.line, lineWidth: 1))
            .shadow(color: BirdieTheme.night.opacity(0.06), radius: 14, y: 7)
    }
}

extension View {
    func birdieCard(padding: CGFloat = 18) -> some View {
        modifier(BirdieCard(padding: padding))
    }

    func birdieListStyle() -> some View {
        self
            .scrollContentBackground(.hidden)
            .background(BirdieTheme.canvas)
            .tint(BirdieTheme.fairwayDark)
    }
}

struct BirdiePageHeader: View {
    let title: LocalizedStringKey
    let subtitle: LocalizedStringKey?

    init(_ title: LocalizedStringKey, subtitle: LocalizedStringKey? = nil) {
        self.title = title
        self.subtitle = subtitle
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("BIRDIE BUDDY / FIELD NOTES")
                .font(BirdieTheme.mono(10))
                .tracking(2)
                .foregroundStyle(BirdieTheme.fairway)
            Text(title)
                .font(BirdieTheme.display(32))
                .foregroundStyle(BirdieTheme.ink)
                .accessibilityAddTraits(.isHeader)
            if let subtitle {
                Text(subtitle)
                    .font(BirdieTheme.body(14))
                    .foregroundStyle(BirdieTheme.muted)
            }
            Rectangle().fill(BirdieTheme.line).frame(height: 1).padding(.top, 8)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct BirdieMetricCard: View {
    let label: LocalizedStringKey
    let value: String
    var accent: Color = BirdieTheme.fairway

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Capsule().fill(accent).frame(height: 4)
            Text(label)
                .font(BirdieTheme.body(11, weight: .bold))
                .foregroundStyle(BirdieTheme.muted)
            Text(value)
                .font(BirdieTheme.mono(25))
                .foregroundStyle(BirdieTheme.ink)
                .minimumScaleFactor(0.8)
                .lineLimit(1)
        }
        .birdieCard(padding: 14)
        .accessibilityElement(children: .combine)
    }
}

struct BirdieCounter: View {
    let label: LocalizedStringKey
    @Binding var value: Int
    let range: ClosedRange<Int>
    var compact = false

    var body: some View {
        HStack(spacing: 10) {
            Text(label)
                .font(BirdieTheme.body(compact ? 15 : 17, weight: .semibold))
                .foregroundStyle(BirdieTheme.ink)
            Spacer(minLength: 4)
            Button { change(-1) } label: { counterButton("minus") }
                .disabled(value <= range.lowerBound)
            Text("\(value)")
                .font(BirdieTheme.mono(compact ? 25 : 37))
                .monospacedDigit()
                .frame(minWidth: compact ? 32 : 43)
                .foregroundStyle(BirdieTheme.ink)
            Button { change(1) } label: { counterButton("plus") }
                .disabled(value >= range.upperBound)
        }
        .padding(.vertical, compact ? 7 : 12)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(Text(label))
        .accessibilityValue("\(value)")
        .accessibilityAdjustableAction { direction in
            switch direction {
            case .increment: change(1)
            case .decrement: change(-1)
            @unknown default: break
            }
        }
    }

    private func change(_ delta: Int) {
        value = min(max(value + delta, range.lowerBound), range.upperBound)
    }

    private func counterButton(_ symbol: String) -> some View {
        Image(systemName: symbol)
            .font(.system(size: compact ? 17 : 21, weight: .medium))
            .frame(width: compact ? 44 : 48, height: compact ? 44 : 48)
            .background(BirdieTheme.canvas, in: Circle())
            .overlay(Circle().stroke(BirdieTheme.line, lineWidth: 1))
            .foregroundStyle(BirdieTheme.ink)
    }
}
