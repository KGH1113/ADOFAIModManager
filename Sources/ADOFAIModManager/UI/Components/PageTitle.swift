import SwiftUI

struct PageTitle: View {
    let title: String
    let subtitle: String
    var body: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.compact) {
            Text(verbatim: title).font(.largeTitle.bold())
            Text(verbatim: subtitle).font(.title3).foregroundStyle(.secondary)
        }
    }
}

extension View {
    @ViewBuilder
    func adaptiveGlass() -> some View {
        if #available(macOS 26.0, *) {
            self.glassEffect(.regular, in: .rect(cornerRadius: LayoutMetrics.cardCornerRadius))
        } else {
            self.background(.regularMaterial, in: RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius))
        }
    }
}
