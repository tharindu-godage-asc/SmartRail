import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:go_router/go_router.dart';

import '../../../app/providers.dart';
import '../../../core/strings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/tokens.g.dart';
import '../../../core/time.dart';
import '../../../core/widgets/widgets.dart';
import '../../journey/presentation/help_screen.dart';
import '../../history/presentation/history_screens.dart';
import '../../journey/domain/local_cache.dart';
import '../domain/report.dart';
import '../domain/report_queue.dart';
import 'delivery_chip.dart';
import 'report_label.dart';

/// Every report on this phone, newest first, live: a state change shows at once (spec 12 rule 6). It reads the queue, so it works
/// offline and never calls the server.
final allReportsProvider = StreamProvider<List<QueuedReport>>(
  (ref) => ref.watch(reportQueueProvider).watchAll(),
);

/// Delivered reports older than this are removed from the phone: the server has them (spec 12 rule 7). Waiting and rejected
/// ones are never removed (FR-31).
const deliveredRetention = Duration(days: 7);

Future<int> purgeOldDelivered(ReportQueue queue, DateTime now) =>
    queue.deleteDeliveredBefore(now.toUtc().subtract(deliveredRetention));

/// The reports of one journey, with its heading (spec 12, `Nimal-Reports`).
class ReportGroup {
  ReportGroup(this.journeyId, this.reports, this.summary);

  final String journeyId;

  /// Newest first.
  final List<QueuedReport> reports;
  final JourneySummary? summary;

  int get waiting =>
      reports.where((r) => r.state == ReportState.pending).length;

  DateTime get latest => reports.first.occurredAt;
}

/// Groups the reports by journey, the journey with the newest report first. The reports of the list come newest first already.
List<ReportGroup> groupReports(
  List<QueuedReport> reports,
  Map<String, JourneySummary> summaries,
) {
  final byJourney = <String, List<QueuedReport>>{};
  for (final report in reports) {
    byJourney.putIfAbsent(report.journeyId, () => []).add(report);
  }
  final groups = [
    for (final entry in byJourney.entries)
      ReportGroup(
        entry.key,
        entry.value..sort((a, b) => b.occurredAt.compareTo(a.occurredAt)),
        summaries[entry.key],
      ),
  ]..sort((a, b) => b.latest.compareTo(a.latest));
  return groups;
}

/// "Oct 19", from the journey's operating day, or from its newest report when the journey is not known.
String groupDate(ReportGroup group) {
  final day = group.summary?.operatingDate;
  final parsed = day == null ? null : DateTime.tryParse(day);
  final date = parsed ?? inColombo(group.latest);
  return DateFormat('MMM d').format(date);
}

final journeySummariesProvider = FutureProvider<Map<String, JourneySummary>>((
  ref,
) {
  // Read again whenever the reports change: a first report on a new journey has just saved its summary.
  ref.watch(allReportsProvider);
  return ref.read(localCacheProvider).readJourneySummaries();
});

/// The Reports tab, "My reports" (`Nimal-Reports`): one collapsible group per journey. The newest journey starts open.
class ReportsScreen extends ConsumerStatefulWidget {
  const ReportsScreen({super.key});

  @override
  ConsumerState<ReportsScreen> createState() => _ReportsScreenState();
}

class _ReportsScreenState extends ConsumerState<ReportsScreen> {
  /// Journeys the operator opened or closed by hand; the others follow the default (only the newest is open).
  final Map<String, bool> _toggled = {};

  /// The segment: My reports (from the phone) or Previous journeys (from the server).
  bool _previous = false;

  @override
  void initState() {
    super.initState();
    purgeOldDelivered(
      ref.read(reportQueueProvider),
      ref.read(phoneNowProvider)(),
    );
  }

  @override
  Widget build(BuildContext context) {
    final reports = ref.watch(allReportsProvider).value ?? const [];
    final summaries = ref.watch(journeySummariesProvider).value ?? const {};
    final groups = groupReports(reports, summaries);
    final colors = context.colors;
    return ListView(
      padding: const EdgeInsets.all(Space.md),
      children: [
        Semantics(
          header: true,
          child: Text(Strings.reportsTitle, style: context.text.headlineMedium),
        ),
        const SizedBox(height: 12),
        SizedBox(
          width: double.infinity,
          child: SegmentedButton<bool>(
            showSelectedIcon: true,
            style: ButtonStyle(
              backgroundColor: WidgetStateProperty.resolveWith(
                (states) => states.contains(WidgetState.selected)
                    ? colors.textPrimary
                    : null,
              ),
              foregroundColor: WidgetStateProperty.resolveWith(
                (states) => states.contains(WidgetState.selected)
                    ? colors.bgPage
                    : colors.textPrimary,
              ),
            ),
            segments: const [
              ButtonSegment(
                value: false,
                label: Text(Strings.segmentMyReports),
              ),
              ButtonSegment(value: true, label: Text(Strings.segmentPrevious)),
            ],
            selected: {_previous},
            onSelectionChanged: (value) =>
                setState(() => _previous = value.first),
          ),
        ),
        const SizedBox(height: 12),
        if (_previous)
          const PreviousJourneysList()
        else if (groups.isEmpty)
          const InfoLine(Strings.noReportsYet)
        else ...[
          for (var i = 0; i < groups.length; i++) ...[
            ReportGroupCard(
              group: groups[i],
              expanded: _toggled[groups[i].journeyId] ?? i == 0,
              onToggle: () => setState(
                () => _toggled[groups[i].journeyId] =
                    !(_toggled[groups[i].journeyId] ?? i == 0),
              ),
            ),
            const SizedBox(height: 10),
          ],
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            spacing: 8,
            children: [
              Icon(Icons.info, size: 20, color: colors.textSecondary),
              Expanded(
                child: Text(
                  Strings.reportsTapHint,
                  style: TextStyle(fontSize: 14, color: colors.textSecondary),
                ),
              ),
            ],
          ),
        ],
      ],
    );
  }
}

/// One journey's heading and, when open, its reports.
class ReportGroupCard extends StatelessWidget {
  const ReportGroupCard({
    required this.group,
    required this.expanded,
    required this.onToggle,
    super.key,
  });

  final ReportGroup group;
  final bool expanded;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final summary = group.summary;
    final heading = summary == null
        ? Strings.journey
        : '${summary.from} → ${summary.to}';
    final train = summary == null
        ? ''
        : '${Strings.trainNumber(summary.trainNumber)} · ';
    final subtitle =
        '$train${Strings.reportCount(group.reports.length)}'
        '${group.waiting > 0 ? ' · ${Strings.waitingCount(group.waiting)}' : ''}';
    return Container(
      decoration: BoxDecoration(
        color: colors.bgPage,
        border: Border.all(color: colors.borderDefault),
        borderRadius: BorderRadius.circular(Radii.lg),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          Semantics(
            button: true,
            expanded: expanded,
            label: '$heading, $subtitle, ${groupDate(group)}',
            excludeSemantics: true,
            child: InkWell(
              onTap: onToggle,
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 14,
                  vertical: 12,
                ),
                child: Row(
                  spacing: 10,
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        spacing: 2,
                        children: [
                          Text(
                            heading,
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          Text(
                            subtitle,
                            style: TextStyle(
                              fontSize: 14,
                              color: colors.textSecondary,
                            ),
                          ),
                        ],
                      ),
                    ),
                    Text(
                      groupDate(group),
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    Icon(
                      expanded ? Icons.expand_less : Icons.expand_more,
                      size: 24,
                    ),
                  ],
                ),
              ),
            ),
          ),
          if (expanded)
            for (final report in group.reports)
              ReportRow(report: report, inGroup: true),
        ],
      ),
    );
  }
}

/// One report in the list: what it is, its delivery chip, and when it happened.
class ReportRow extends StatelessWidget {
  const ReportRow({required this.report, this.inGroup = false, super.key});

  final QueuedReport report;

  /// Inside a journey group the row is flat, with a line above it, as drawn.
  final bool inGroup;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final label = reportLabel(report);
    return Semantics(
      button: true,
      // Reading order: the report, its delivery state, then the time (NFR-32).
      label:
          '$label, ${deliveryWords(report.state)}, ${formatTime(report.occurredAt)}',
      excludeSemantics: true,
      child: InkWell(
        borderRadius: inGroup ? null : BorderRadius.circular(Radii.lg),
        onTap: () => context.push('/report/${report.clientEventId}'),
        child: Container(
          constraints: const BoxConstraints(minHeight: 64),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
          decoration: BoxDecoration(
            color: inGroup ? colors.bgSurface : colors.bgPage,
            border: inGroup
                ? Border(top: BorderSide(color: colors.borderDefault))
                : Border.all(color: colors.borderDefault),
            borderRadius: inGroup ? null : BorderRadius.circular(Radii.lg),
          ),
          child: Row(
            spacing: 10,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  spacing: 6,
                  children: [
                    Text(
                      label,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    DeliveryChip(report.state),
                  ],
                ),
              ),
              Text(
                formatTime(report.occurredAt),
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                  fontFeatures: [FontFeature.tabularFigures()],
                ),
              ),
              const Icon(Icons.chevron_right, size: 22),
            ],
          ),
        ),
      ),
    );
  }
}

/// The words a screen reader hears for a delivery state.
String deliveryWords(ReportState state) => switch (state) {
  ReportState.delivered => Strings.delivered,
  ReportState.pending => Strings.notSentYet,
  ReportState.rejected => Strings.notAccepted,
};

/// The detail of one report (`Nimal-RejectedDetail` for a refused one; the same pattern for the others).
///
/// The drawn board says "A controller will review it", but the server does not keep a refused report for a controller (spec 07 Q1),
/// so this screen says what is true instead (spec 12 rule 5, Q1).
class ReportDetailScreen extends ConsumerWidget {
  const ReportDetailScreen({required this.clientEventId, super.key});

  final String clientEventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = context.colors;
    final reports = ref.watch(allReportsProvider).value ?? const [];
    final report = reports
        .where((r) => r.clientEventId == clientEventId)
        .firstOrNull;

    final (title, lines) = switch (report?.state) {
      ReportState.rejected => (
        Strings.notAcceptedTitle,
        [
          Strings.rejectedDetailBody,
          if (report?.lastReason == 'time_out_of_range')
            Strings.checkPhoneClock,
          Strings.rejectedDetailAdvice,
        ],
      ),
      ReportState.pending => (Strings.notSentYet, [Strings.notSentDetail]),
      ReportState.delivered => (
        Strings.deliveredTitle,
        [Strings.deliveredDetail],
      ),
      null => (Strings.reportMissingTitle, [Strings.reportMissingBody]),
    };

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            Container(
              height: 56,
              decoration: BoxDecoration(
                color: colors.bgPage,
                border: Border(bottom: BorderSide(color: colors.borderDefault)),
              ),
              child: Row(
                children: [
                  IconButton(
                    tooltip: Strings.back,
                    icon: const Icon(Icons.arrow_back),
                    onPressed: () => context.pop(),
                  ),
                  const Expanded(
                    child: Center(
                      child: Text(
                        Strings.reportDetailsTitle,
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 48),
                ],
              ),
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.all(Space.md),
                children: [
                  if (report != null) ...[
                    Align(
                      alignment: Alignment.centerLeft,
                      child: DeliveryChip(report.state),
                    ),
                    const SizedBox(height: 12),
                  ],
                  Semantics(
                    header: true,
                    child: Text(title, style: context.text.headlineMedium),
                  ),
                  if (report != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      '${reportLabel(report)} · ${formatTime(report.occurredAt)}',
                      style: const TextStyle(
                        fontSize: 17,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                  const SizedBox(height: 12),
                  SectionCard(
                    children: [
                      for (final line in lines)
                        Text(
                          line,
                          style: const TextStyle(fontSize: 16, height: 1.5),
                        ),
                    ],
                  ),
                  const SizedBox(height: 24),
                  if (report?.state == ReportState.rejected) ...[
                    const PhoneDeskButton(),
                    const SizedBox(height: 10),
                  ],
                  PrimaryButton(
                    label: Strings.backToReports,
                    onPressed: () => context.pop(),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
