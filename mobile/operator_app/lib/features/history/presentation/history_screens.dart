import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/strings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/tokens.g.dart';
import '../../../core/time.dart';
import '../../../core/widgets/widgets.dart';
import '../domain/history.dart';
import 'history_controller.dart';

String _dayLabel(DateTime day) => DateFormat('EEE d MMM').format(day);

/// "Previous journeys" (spec 12): the journeys he worked on earlier, newest first, from the server. A copy of the first page is kept on
/// the phone for when there is no connection.
class PreviousJourneysList extends ConsumerWidget {
  const PreviousJourneysList({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(historyControllerProvider);
    final colors = context.colors;

    if (!state.hasData) {
      if (state.loading) {
        return const Padding(
          padding: EdgeInsets.all(Space.lg),
          child: Center(child: CircularProgressIndicator()),
        );
      }
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        spacing: 12,
        children: [
          const InfoLine(
            Strings.previousNeedsConnection,
            icon: Icons.cloud_off,
          ),
          SecondaryButton(
            label: Strings.tryAgain,
            onPressed: () =>
                ref.read(historyControllerProvider.notifier).refresh(),
          ),
        ],
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      spacing: 10,
      children: [
        if (state.failed || state.fromCache)
          InfoLine(
            Strings.lastUpdated(formatTime(state.serverTime!)),
            icon: Icons.cloud_off,
          ),
        if (state.journeys.isEmpty)
          const InfoLine(Strings.noPreviousJourneys)
        else
          for (final journey in state.journeys) _JourneyCard(journey: journey),
        if (state.next != null)
          SecondaryButton(
            label: Strings.showEarlier,
            onPressed: state.loading
                ? null
                : () => ref
                      .read(historyControllerProvider.notifier)
                      .showEarlier(),
          ),
        if (state.failed && !state.fromCache)
          Text(
            Strings.previousLoadFailed,
            style: TextStyle(fontSize: 14, color: colors.textSecondary),
          ),
      ],
    );
  }
}

class _JourneyCard extends StatelessWidget {
  const _JourneyCard({required this.journey});

  final PastJourney journey;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final late = journey.arrivalDelayMinutes;
    final detail = [
      _dayLabel(journey.operatingDate),
      journey.completed ? Strings.statusCompleted : Strings.statusNotFinished,
      Strings.reportCount(journey.myReportCount),
      if (late != null && late > 0) Strings.arrivedLate(late),
    ].join(' · ');
    final heading =
        '${Strings.trainNumber(journey.trainNumber)} ${journey.fromName} → ${journey.toName}';
    return Semantics(
      button: true,
      label: '$heading, $detail',
      excludeSemantics: true,
      child: InkWell(
        borderRadius: BorderRadius.circular(Radii.lg),
        onTap: () => context.push('/history/${journey.journeyId}'),
        child: Container(
          constraints: const BoxConstraints(minHeight: 64),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          decoration: BoxDecoration(
            color: colors.bgPage,
            border: Border.all(color: colors.borderDefault),
            borderRadius: BorderRadius.circular(Radii.lg),
          ),
          child: Row(
            spacing: 10,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  spacing: 4,
                  children: [
                    Text(
                      heading,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    Text(
                      detail,
                      style: TextStyle(
                        fontSize: 14,
                        color: colors.textSecondary,
                      ),
                    ),
                  ],
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

/// The record of one past journey, read-only: its stops with scheduled and actual times, and his own reports (spec 12 rule 8).
class JourneyRecordScreen extends ConsumerWidget {
  const JourneyRecordScreen({required this.journeyId, super.key});

  final String journeyId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = context.colors;
    final record = ref.watch(journeyRecordProvider(journeyId));
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
                        Strings.journeyRecordTitle,
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
              child: record.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (_, _) => Padding(
                  padding: const EdgeInsets.all(Space.md),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    spacing: 12,
                    children: [
                      const InfoLine(
                        Strings.recordNeedsConnection,
                        icon: Icons.cloud_off,
                      ),
                      SecondaryButton(
                        label: Strings.tryAgain,
                        onPressed: () =>
                            ref.invalidate(journeyRecordProvider(journeyId)),
                      ),
                    ],
                  ),
                ),
                data: (data) => data == null
                    ? const Padding(
                        padding: EdgeInsets.all(Space.md),
                        child: InfoLine(Strings.recordNotFound),
                      )
                    : _RecordBody(record: data),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RecordBody extends StatelessWidget {
  const _RecordBody({required this.record});

  final JourneyRecord record;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    String time(DateTime? t) => t == null ? '–' : formatTime(t);
    return ListView(
      padding: const EdgeInsets.all(Space.md),
      children: [
        Semantics(
          header: true,
          child: Text(
            '${Strings.trainNumber(record.trainNumber)} · ${_dayLabel(record.operatingDate)}',
            style: context.text.headlineMedium,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          record.status == 'Completed'
              ? Strings.statusCompleted
              : Strings.statusNotFinished,
          style: TextStyle(fontSize: 14, color: colors.textSecondary),
        ),
        const SizedBox(height: 12),
        SectionCard(
          title: Strings.stopsTitle,
          children: [
            for (final stop in record.stops)
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                spacing: 8,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          stop.stationName,
                          style: const TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        Text(
                          stop.confirmation == 'Inferred'
                              ? Strings.stopPassedInferred
                              : stop.confirmation == 'Pending'
                              ? Strings.stopNotReached
                              : Strings.stopReported,
                          style: TextStyle(
                            fontSize: 13,
                            color: colors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(
                        '${Strings.timeArrival} ${time(stop.actualArrival)} (${time(stop.scheduledArrival)})',
                        style: const TextStyle(
                          fontSize: 13,
                          fontFeatures: [FontFeature.tabularFigures()],
                        ),
                      ),
                      Text(
                        '${Strings.timeDeparture} ${time(stop.actualDeparture)} (${time(stop.scheduledDeparture)})',
                        style: const TextStyle(
                          fontSize: 13,
                          fontFeatures: [FontFeature.tabularFigures()],
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            Text(
              Strings.timesLegend,
              style: TextStyle(fontSize: 12, color: colors.textSecondary),
            ),
          ],
        ),
        const SizedBox(height: 12),
        SectionCard(
          title: Strings.yourReports,
          children: [
            if (record.myReports.isEmpty)
              const Text(Strings.noReportsInRecord)
            else
              for (final report in record.myReports)
                Row(
                  spacing: 8,
                  children: [
                    Expanded(
                      child: Text(
                        _reportText(report),
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                    Text(
                      formatTime(report.occurredAt),
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        fontFeatures: [FontFeature.tabularFigures()],
                      ),
                    ),
                  ],
                ),
          ],
        ),
        const SizedBox(height: 12),
        const InfoLine(Strings.recordReadOnly),
      ],
    );
  }

  String _reportText(RecordReport r) => switch (r.type) {
    'Departed' => Strings.departedLabel(r.stationName ?? ''),
    'Arrived' => Strings.arrivedLabel(r.stationName ?? ''),
    _ => Strings.delayLabel(
      _reason(r.delayReason),
      r.totalDelayMinutes == null
          ? Strings.delayDurationUnknownShort
          : Strings.delayTotalShort(r.totalDelayMinutes!),
    ),
  };

  String _reason(String? code) => switch (code) {
    'SignalIssue' => Strings.reasonSignal,
    'TechnicalFault' => Strings.reasonTechnical,
    'Obstruction' => Strings.reasonObstruction,
    'Weather' => Strings.reasonWeather,
    _ => Strings.reasonUnknown,
  };
}
