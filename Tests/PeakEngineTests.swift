import Foundation

@inline(__always)
func check(_ condition: @autoclosure () -> Bool, _ message: String) {
    guard condition() else {
        fputs("FAIL: \(message)\n", stderr)
        exit(1)
    }
}

func beijingDate(_ hour: Int, _ minute: Int, _ second: Int = 0, dayOffset: Int = 0) -> Date {
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = PeakEngine.beijingTimeZone
    let day = calendar.date(from: DateComponents(year: 2026, month: 8, day: 20 + dayOffset))!
    return day.addingTimeInterval(Double(hour * 3600 + minute * 60 + second))
}

@main
enum PeakEngineTests {
    static func main() {
        check(PeakEngine.phase(at: beijingDate(8, 59, 59)) == .offPeak, "08:59:59 is off-peak")
        check(PeakEngine.phase(at: beijingDate(9, 0)) == .peak, "09:00 is peak")
        check(PeakEngine.phase(at: beijingDate(11, 59, 59)) == .peak, "11:59:59 is peak")
        check(PeakEngine.phase(at: beijingDate(12, 0)) == .offPeak, "12:00 is off-peak")
        check(PeakEngine.phase(at: beijingDate(13, 59, 59)) == .offPeak, "13:59:59 is off-peak")
        check(PeakEngine.phase(at: beijingDate(14, 0)) == .peak, "14:00 is peak")
        check(PeakEngine.phase(at: beijingDate(17, 59, 59)) == .peak, "17:59:59 is peak")
        check(PeakEngine.phase(at: beijingDate(18, 0)) == .offPeak, "18:00 is off-peak")

        let evening = beijingDate(23, 30)
        let nextMorning = beijingDate(9, 0, dayOffset: 1)
        check(PeakEngine.nextBoundary(after: evening) == nextMorning, "evening transitions next day at 09:00")
        check(PeakEngine.snapshot(at: evening).secondsToNextBoundary == 9.5 * 3600, "evening countdown is 9.5 hours")

        let entries = WidgetTimelinePlan.entries(from: beijingDate(8, 0), days: 1)
        check(entries.map { $0.date } == [
            beijingDate(8, 0), beijingDate(9, 0), beijingDate(12, 0), beijingDate(14, 0), beijingDate(18, 0)
        ], "widget entries contain only the four daily transitions")
        check(PeakEngine.countdownText(3_661) == "1:01:01", "countdown formats hours")
        check(PeakEngine.countdownText(59) == "00:59", "countdown formats under one hour")
        print("PeakEngine tests passed")
    }
}