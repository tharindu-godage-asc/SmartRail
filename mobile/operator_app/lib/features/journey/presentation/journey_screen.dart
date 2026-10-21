import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/providers.dart';
import '../../../core/strings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/tokens.g.dart';
import '../../../core/time.dart';
import '../../../core/widgets/widgets.dart';
import '../../reports/domain/report.dart';
import '../../reports/presentation/delivery_chip.dart';
import '../../reports/presentation/report_dispatcher.dart';
import '../domain/models.dart';
import '../domain/progress.dart';
import '../../reports/presentation/report_label.dart';
import 'help_screen.dart';
import 'journey_controller.dart';

/// The Journey tab: the one screen the operator works from (`Nimal-Journey-Departure`, `Nimal-Offline`, `Nimal-NoJourney`).
class JourneyScreen extends ConsumerWidget {
  const JourneyScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final view = ref.watch(journeyControllerProvider);

    return RefreshIndicator(
      onRefresh: () => ref.read(journeyControllerProvider.notifier).refresh(),
      child: switch (view) {
        AsyncData(:final value) when value.journey != null => _JourneyBody(
          value,
        ),
        AsyncData(:final value) when value.failed => _Message(
          title: Strings.loadFailed,
          body: Strings.loadFailedHelp,
          icon: TokenIcons.connectionOffline,
        ),
        AsyncData() => const _Message(
          title: Strings.noJourneyTitle,
          body: Strings.noJourneyBody,
          icon: Icons.train,
        ),
        AsyncError() => const _Message(
          title: Strings.loadFailed,
          body: Strings.loadFailedHelp,
          icon: TokenIcons.connectionOffline,
        ),
        _ => const Center(child: CircularProgressIndicator()),
      },
    );
  }
}

/// A centred message that can still be pulled to refresh, with Refresh and phone buttons (`Nimal-NoJourney`).
class _Message extends ConsumerWidget {
  const _Message({required this.title, required this.body, required this.icon});

  final String title;
  final String body;
  final IconData icon;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = context.colors;
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(Space.md),
        child: ConstrainedBox(
          constraints: BoxConstraints(
            minHeight: math.max(0, constraints.maxHeight - 2 * Space.md),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            spacing: 14,
            children: [
              Container(
                width: 96,
                height: 96,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: colors.bgPage,
                  border: Border.all(color: colors.borderDefault, width: 1.5),
                ),
                child: Icon(icon, size: 48, color: colors.textPrimary),
              ),
              Semantics(
                header: true,
                child: Text(
                  title,
                  textAlign: TextAlign.center,
                  style: context.text.headlineMedium,
                ),
              ),
              Text(
                body,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 16,
                  height: 1.5,
                  color: colors.textSecondary,
                ),
              ),
              const SizedBox(height: Space.sm),
              PrimaryButton(
                label: Strings.refresh,
                icon: Icons.refresh,
                onPressed: () =>
                    ref.read(journeyControllerProvider.notifier).refresh(),
              ),
              const PhoneDeskButton(),
            ],
          ),
        ),
      ),
    );
  }
}

class _JourneyBody extends ConsumerStatefulWidget {
  const _JourneyBody(this.view);

  final JourneyViewState view;

  @override
  ConsumerState<_JourneyBody> createState() => _JourneyBodyState();
}

class _JourneyBodyState extends ConsumerState<_JourneyBody> {
  /// True from the tap until the report is saved on the phone, so a double tap cannot make two reports (spec rule 9).
  bool _saving = false;

  /// Runs one report action once: the buttons stay off until it is saved, so a double tap cannot make two reports.
  /// A report cannot be taken back, so each stop report asks first (modal, Cancel or Confirm).
  Future<bool> _confirm(String title) async {
    final colors = context.colors;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: colors.bgPage,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(Radii.lg),
        ),
        title: Text(title, style: context.text.headlineMedium),
        content: const Text(Strings.confirmReportBody),
        actions: [
          OutlinedButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text(Strings.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text(Strings.confirmReport),
          ),
        ],
      ),
    );
    return ok ?? false;
  }

  Future<void> _act(Future<void> Function(ReportActions actions) action) async {
    if (_saving) {
      return;
    }
    setState(() => _saving = true);
    try {
      await action(ref.read(reportActionsProvider));
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final journey = widget.view.journey!;
    final progress = ref.watch(journeyProgressProvider);
    final reports =
        ref.watch(reportsProvider(journey.journeyId)).value ??
        const <QueuedReport>[];
    final online = ref.watch(isOnlineProvider).value ?? true;
    final offline = widget.view.offline || !online;
    final lastReport = reports.isEmpty ? null : reports.first;

    // The arrival at the final stop is reported: the journey is complete (AC-E03-03 4). "Done" puts it away.
    final next = progress?.next;
    if (next is JourneyFinished) {
      if (ref.watch(completedDismissedProvider) == journey.journeyId) {
        return const _Message(
          title: Strings.noJourneyTitle,
          body: Strings.noJourneyBody,
          icon: Icons.train,
        );
      }
      return _CompletedView(
        finished: next,
        reports: reports,
        onDone: () => ref
            .read(completedDismissedProvider.notifier)
            .dismiss(journey.journeyId),
      );
    }

    return Column(
      children: [
        if (offline) _OfflineBanner(lastUpdated: widget.view.fetchedAt),
        Expanded(
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(Space.md),
            children: [
              _Heading(journey),
              const SizedBox(height: 12),
              _ActionCard(
                action: progress?.next,
                saving: _saving,
                onDepart: (stop) async {
                  if (await _confirm(
                    Strings.confirmDepartedTitle(stop.stationName),
                  )) {
                    await _act((a) => a.departed(journey, stop));
                  }
                },
                onArrive: (stop) async {
                  if (await _confirm(
                    Strings.confirmArrivedTitle(stop.stationName),
                  )) {
                    await _act((a) => a.arrived(journey, stop));
                  }
                },
              ),
              const SizedBox(height: 12),
              SecondaryButton(
                label: Strings.reportDelay,
                icon: Icons.warning,
                onPressed: () => context.push('/delay/reason'),
              ),
              const SizedBox(height: 12),
              if (lastReport != null) ...[
                _LastReportCard(lastReport),
                const SizedBox(height: 12),
              ],
              _StopsCard(journey: journey, marks: progress?.marks ?? const {}),
            ],
          ),
        ),
      ],
    );
  }
}

class _OfflineBanner extends StatelessWidget {
  const _OfflineBanner({required this.lastUpdated});

  final DateTime? lastUpdated;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Semantics(
      liveRegion: true,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: Space.md, vertical: 12),
        color: colors.textPrimary,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          spacing: 12,
          children: [
            Icon(TokenIcons.connectionOffline, size: 22, color: colors.bgPage),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    Strings.offlineTitle,
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: colors.bgPage,
                    ),
                  ),
                  Text(
                    [
                      Strings.offlineBody,
                      if (lastUpdated != null)
                        Strings.lastUpdated(formatTime(lastUpdated!)),
                    ].join(' '),
                    style: TextStyle(
                      fontSize: 14,
                      height: 1.4,
                      color: colors.bgPage,
                    ),
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

class _Heading extends StatelessWidget {
  const _Heading(this.journey);

  final OperatorJourney journey;

  @override
  Widget build(BuildContext context) {
    final date = DateTime.parse(journey.operatingDate);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      spacing: 2,
      children: [
        // The API does not send the line name, so the date stands alone (the design adds "Main Line").
        Text(
          formatDay(DateTime.utc(date.year, date.month, date.day)),
          style: TextStyle(fontSize: 14, color: context.colors.textSecondary),
        ),
        Semantics(
          header: true,
          child: Text(
            Strings.trainTitle(
              journey.trainNumber,
              journey.firstStop.stationName,
              journey.finalStop.stationName,
            ),
            style: context.text.headlineMedium,
          ),
        ),
      ],
    );
  }
}

class _ActionCard extends StatelessWidget {
  const _ActionCard({
    required this.action,
    required this.saving,
    required this.onDepart,
    required this.onArrive,
  });

  final NextAction? action;
  final bool saving;
  final ValueChanged<OperatorStop> onDepart;
  final ValueChanged<OperatorStop> onArrive;

  @override
  Widget build(BuildContext context) {
    final secondary = TextStyle(
      fontSize: 15,
      color: context.colors.textSecondary,
    );
    return SectionCard(
      children: switch (action) {
        DepartFrom(:final stop) => [
          Text(Strings.nextDeparture(stop.stationName), style: secondary),
          PrimaryButton(
            label: Strings.departedButton(stop.stationName),
            onPressed: saving ? null : () => onDepart(stop),
          ),
        ],
        // An in-between stop: Arrived and Departed together, either one (spec 19). The final stop has only Arrived.
        ArriveAt(:final stop, canDepartInstead: true) => [
          Text(
            Strings.nextArrivalOrDeparture(stop.stationName),
            style: secondary,
          ),
          PrimaryButton(
            label: Strings.arrivedButton(stop.stationName),
            onPressed: saving ? null : () => onArrive(stop),
          ),
          SecondaryButton(
            label: Strings.departedButton(stop.stationName),
            onPressed: saving ? null : () => onDepart(stop),
          ),
        ],
        ArriveAt(:final stop) => [
          Text(Strings.nextArrival(stop.stationName), style: secondary),
          PrimaryButton(
            label: Strings.arrivedButton(stop.stationName),
            onPressed: saving ? null : () => onArrive(stop),
          ),
        ],
        JourneyFinished() || null => const [SizedBox.shrink()],
      },
    );
  }
}

class _LastReportCard extends StatelessWidget {
  const _LastReportCard(this.report);

  final QueuedReport report;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return SectionCard(
      title: Strings.lastReport,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Flexible(
              child: Text(
                reportLabel(report),
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                ),
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
          ],
        ),
        DeliveryChip(report.state),
        if (report.state == ReportState.pending)
          Text(
            Strings.notSentDetail,
            style: TextStyle(fontSize: 14, color: colors.textSecondary),
          ),
        if (report.state == ReportState.rejected) ...[
          if (report.lastReason == 'time_out_of_range')
            Text(
              Strings.checkPhoneClock,
              style: TextStyle(fontSize: 14, color: colors.textSecondary),
            ),
          Text(
            Strings.rejectedPhoneHelp,
            style: TextStyle(fontSize: 14, color: colors.textSecondary),
          ),
        ],
      ],
    );
  }
}

class _StopsCard extends StatelessWidget {
  const _StopsCard({required this.journey, required this.marks});

  final OperatorJourney journey;
  final Map<String, StopMark> marks;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return SectionCard(
      title: Strings.journey,
      children: [
        for (final stop in journey.stops)
          Builder(
            builder: (context) {
              final mark = marks[stop.serviceStopId] ?? StopMark.upcoming;
              final (icon, weight, color) = switch (mark) {
                StopMark.done => (
                  TokenIcons.stopPassed,
                  FontWeight.w600,
                  colors.textSecondary,
                ),
                StopMark.current => (
                  TokenIcons.stopCurrent,
                  FontWeight.w700,
                  colors.textPrimary,
                ),
                StopMark.upcoming => (
                  TokenIcons.stopUpcoming,
                  FontWeight.w400,
                  colors.textSecondary,
                ),
              };
              return Semantics(
                label:
                    '${stop.stationName}, ${switch (mark) {
                      StopMark.done => 'departed',
                      StopMark.current => 'next',
                      StopMark.upcoming => 'later',
                    }}',
                excludeSemantics: true,
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 3),
                  child: Row(
                    spacing: 12,
                    children: [
                      SizedBox(
                        width: 24,
                        child: Icon(icon, size: 22, color: color),
                      ),
                      Expanded(
                        child: Text(
                          stop.stationName,
                          style: TextStyle(fontSize: 16, fontWeight: weight),
                        ),
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
      ],
    );
  }
}

/// Remembers, for this run of the app, that the operator pressed Done on the completed screen.
class CompletedDismissed extends Notifier<String?> {
  @override
  String? build() => null;

  void dismiss(String journeyId) => state = journeyId;
}

final completedDismissedProvider =
    NotifierProvider<CompletedDismissed, String?>(CompletedDismissed.new);

/// The journey is complete (`Nimal-Completed`): where the train arrived, and how this operator's reports went.
class _CompletedView extends StatelessWidget {
  const _CompletedView({
    required this.finished,
    required this.reports,
    required this.onDone,
  });

  final JourneyFinished finished;
  final List<QueuedReport> reports;
  final VoidCallback onDone;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    int count(ReportState state) =>
        reports.where((r) => r.state == state).length;

    Widget tile(int value, String label) => Expanded(
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: colors.bgPage,
          border: Border.all(color: colors.borderDefault),
          borderRadius: BorderRadius.circular(Radii.lg),
        ),
        child: Column(
          spacing: 2,
          children: [
            Text(
              '$value',
              style: const TextStyle(
                fontSize: 32,
                fontWeight: FontWeight.w700,
                height: 1.1,
                fontFeatures: [FontFeature.tabularFigures()],
              ),
            ),
            Text(
              label,
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, color: colors.textSecondary),
            ),
          ],
        ),
      ),
    );

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(Space.md),
      children: [
        const SizedBox(height: Space.xl),
        Center(
          child: Container(
            width: 80,
            height: 80,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: colors.bgPage,
              border: Border.all(color: colors.borderDefault, width: 1.5),
            ),
            child: Icon(
              TokenIcons.deliveryDelivered,
              size: 40,
              color: colors.textPrimary,
            ),
          ),
        ),
        const SizedBox(height: 14),
        Semantics(
          header: true,
          child: Text(
            Strings.completedTitle,
            textAlign: TextAlign.center,
            style: context.text.headlineMedium,
          ),
        ),
        const SizedBox(height: 14),
        Text.rich(
          TextSpan(
            text: '${Strings.completedBody(finished.finalStop.stationName)} ',
            children: [
              if (finished.arrivedAt != null)
                TextSpan(
                  text: formatTime(finished.arrivedAt!),
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    color: colors.textPrimary,
                    fontFeatures: const [FontFeature.tabularFigures()],
                  ),
                ),
            ],
          ),
          textAlign: TextAlign.center,
          style: TextStyle(
            fontSize: 17,
            height: 1.5,
            color: colors.textSecondary,
          ),
        ),
        const SizedBox(height: Space.lg),
        Semantics(
          header: true,
          child: Text(
            Strings.yourReports.toUpperCase(),
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              letterSpacing: .6,
              color: colors.textSecondary,
            ),
          ),
        ),
        const SizedBox(height: 8),
        Row(
          spacing: 10,
          children: [
            tile(count(ReportState.delivered), Strings.countSent),
            tile(count(ReportState.pending), Strings.countWaiting),
            tile(count(ReportState.rejected), Strings.countRejected),
          ],
        ),
        const SizedBox(height: Space.lg),
        PrimaryButton(label: Strings.done, onPressed: onDone),
      ],
    );
  }
}
