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

  Future<void> _depart(OperatorJourney journey, OperatorStop stop) async {
    if (_saving) {
      return;
    }
    setState(() => _saving = true);
    try {
      await ref.read(reportActionsProvider).departed(journey, stop);
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
                onDepart: (stop) => _depart(journey, stop),
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
  });

  final NextAction? action;
  final bool saving;
  final ValueChanged<OperatorStop> onDepart;

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
        NoMoreReports(:final finalStop) => [
          Text(
            Strings.noMoreReports,
            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
          ),
          Text(
            Strings.noMoreReportsDetail(finalStop.stationName),
            style: secondary,
          ),
        ],
        null => const [SizedBox.shrink()],
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
                Strings.departedLabel(report.stationName),
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
