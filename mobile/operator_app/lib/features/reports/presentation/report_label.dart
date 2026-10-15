import '../../../core/strings.dart';
import '../domain/report.dart';

/// The words for a delay reason (`Nimal-DelayReason`).
String reasonLabel(DelayReason reason) => switch (reason) {
  DelayReason.signalIssue => Strings.reasonSignal,
  DelayReason.technicalFault => Strings.reasonTechnical,
  DelayReason.obstruction => Strings.reasonObstruction,
  DelayReason.weather => Strings.reasonWeather,
  DelayReason.unknown => Strings.reasonUnknown,
};

/// One line describing a report, the same everywhere it is listed: "Departed Gampaha", "Arrived Gampaha",
/// "Delay: Signal issue, ~10 min".
String reportLabel(QueuedReport report) => switch (report.kind) {
  ReportKind.departed => Strings.departedLabel(report.stationName),
  ReportKind.arrived => Strings.arrivedLabel(report.stationName),
  ReportKind.delay => Strings.delayLabel(
    reasonLabel(report.delayReason ?? DelayReason.unknown),
    report.totalDelayMinutes == null
        ? Strings.delayDurationUnknownShort
        : Strings.delayTotalShort(report.totalDelayMinutes!),
  ),
};
