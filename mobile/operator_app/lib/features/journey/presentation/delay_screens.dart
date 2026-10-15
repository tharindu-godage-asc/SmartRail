import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/strings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/tokens.g.dart';
import '../../../core/widgets/widgets.dart';
import '../../reports/domain/report.dart';
import '../../reports/presentation/delivery_chip.dart';
import '../../reports/presentation/report_dispatcher.dart';
import '../../reports/presentation/report_label.dart';
import '../domain/models.dart';
import 'help_screen.dart';
import 'journey_controller.dart';

/// The reasons in the order the screen lists them (`Nimal-DelayReason`).
const delayReasonOrder = [
  DelayReason.signalIssue,
  DelayReason.technicalFault,
  DelayReason.obstruction,
  DelayReason.weather,
  DelayReason.unknown,
];

/// The labels as listed, kept for the tests and the screen.
final delayReasons = [
  for (final reason in delayReasonOrder) reasonLabel(reason),
];

/// "Total delay" choices decided with the owner (spec 06, Q6 and 15 Oct): +10, +15, +20, +30, +45, +1 hour and Unknown. A choice at or below
/// the delay already running is not offered (AC-E03-04 6). The server accepts 1 to 240, so no server change is needed.
const delayChoices = [10, 15, 20, 30, 45, 60];

List<int> offeredDelays(int runningLate) =>
    delayChoices.where((m) => m > runningLate).toList();

/// The reason chosen on step 1, kept for step 2.
class DelayReasonDraft extends Notifier<DelayReason> {
  @override
  DelayReason build() => DelayReason.signalIssue;

  void choose(DelayReason reason) => state = reason;
}

final delayReasonDraftProvider =
    NotifierProvider<DelayReasonDraft, DelayReason>(DelayReasonDraft.new);

/// Step 1 of 2 (`Nimal-DelayReason`).
class DelayReasonScreen extends ConsumerStatefulWidget {
  const DelayReasonScreen({super.key});

  @override
  ConsumerState<DelayReasonScreen> createState() => _DelayReasonScreenState();
}

class _DelayReasonScreenState extends ConsumerState<DelayReasonScreen> {
  DelayReason _selected = DelayReason.signalIssue;

  @override
  Widget build(BuildContext context) {
    final journey = ref.watch(journeyControllerProvider).value?.journey;

    return _DelayScaffold(
      subtitle: '${_trainLine(journey)} · ${Strings.stepOfTwo(1)}',
      title: Strings.delayReasonTitle,
      progress: 1,
      body: [
        for (final reason in delayReasonOrder)
          _Choice(
            label: reasonLabel(reason),
            selected: reason == _selected,
            icon: reason == _selected
                ? Icons.radio_button_checked
                : TokenIcons.stopUpcoming,
            onTap: () => setState(() => _selected = reason),
          ),
      ],
      actions: [
        PrimaryButton(
          label: Strings.next,
          onPressed: () {
            ref.read(delayReasonDraftProvider.notifier).choose(_selected);
            context.push('/delay/estimate');
          },
        ),
        SecondaryButton(label: Strings.cancel, onPressed: () => context.pop()),
      ],
    );
  }
}

/// Step 2 of 2 (`Nimal-DelayEstimate`): the total, or Unknown, then Send report.
class DelayEstimateScreen extends ConsumerStatefulWidget {
  const DelayEstimateScreen({super.key});

  @override
  ConsumerState<DelayEstimateScreen> createState() =>
      _DelayEstimateScreenState();
}

class _DelayEstimateScreenState extends ConsumerState<DelayEstimateScreen> {
  /// What the operator picked; until they pick, the first option offered is shown selected, as in the design.
  bool _picked = false;

  /// null is "Unknown".
  int? _choice;

  /// True from the tap until the report is saved, so a double tap cannot make two reports.
  bool _sending = false;

  Future<void> _send(OperatorJourney journey, int? total) async {
    if (_sending) {
      return;
    }
    setState(() => _sending = true);
    final report = await ref
        .read(reportActionsProvider)
        .delayed(
          journey,
          reason: ref.read(delayReasonDraftProvider),
          totalMinutes: total,
        );
    if (mounted) {
      context.pushReplacement('/delay/sent', extra: report.clientEventId);
    }
  }

  @override
  Widget build(BuildContext context) {
    final view = ref.watch(journeyControllerProvider).value;
    final journey = view?.journey;
    final runningLate = view?.status?.delayMinutes ?? 0;
    final known = view?.status?.delayKnown ?? true;
    final offered = offeredDelays(runningLate);
    final selected = _picked
        ? _choice
        : (offered.isEmpty ? null : offered.first);

    return _DelayScaffold(
      subtitle: '${_trainLine(journey)} · ${Strings.stepOfTwo(2)}',
      title: Strings.delayEstimateTitle,
      progress: 2,
      body: [
        Text(
          known ? Strings.runningLate(runningLate) : Strings.runningLateUnknown,
          style: const TextStyle(fontSize: 16),
        ),
        for (final minutes in offered)
          _Choice(
            label: Strings.plusMinutes(minutes),
            selected: selected == minutes,
            icon: selected == minutes
                ? Icons.radio_button_checked
                : TokenIcons.stopUpcoming,
            onTap: () => setState(() {
              _picked = true;
              _choice = minutes;
            }),
          ),
        _Choice(
          label: Strings.reasonUnknown,
          selected: selected == null,
          icon: selected == null
              ? Icons.radio_button_checked
              : TokenIcons.stopUpcoming,
          onTap: () => setState(() {
            _picked = true;
            _choice = null;
          }),
        ),
        const InfoLine(Strings.delayTotalHelp),
      ],
      actions: [
        PrimaryButton(
          label: Strings.sendReport,
          onPressed: journey == null || _sending
              ? null
              : () => _send(journey, selected),
        ),
        SecondaryButton(label: Strings.back, onPressed: () => context.pop()),
      ],
    );
  }
}

/// What happened to the delay just sent: Delivered (`Nimal-DelaySent`), saved on the phone (`Nimal-DelayNotSent`), or refused.
/// It follows the report, so it changes by itself when a saved report is delivered later.
class DelaySentScreen extends ConsumerWidget {
  const DelaySentScreen({required this.clientEventId, super.key});

  final String clientEventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = context.colors;
    final journeyId = ref
        .watch(journeyControllerProvider)
        .value
        ?.journey
        ?.journeyId;
    final reports = journeyId == null
        ? const <QueuedReport>[]
        : ref.watch(reportsProvider(journeyId)).value ?? const <QueuedReport>[];
    final report = reports
        .where((r) => r.clientEventId == clientEventId)
        .firstOrNull;
    final waiting = reports.where((r) => r.state == ReportState.pending).length;

    if (report == null) {
      return const Scaffold(body: SafeArea(child: AppHeader()));
    }

    final reason = reasonLabel(report.delayReason ?? DelayReason.unknown);
    final total = report.totalDelayMinutes;
    final summary = Strings.delaySummary(
      reason,
      total == null ? Strings.durationUnknown : Strings.aboutLate(total),
    );
    final (icon, title, detail) = switch (report.state) {
      ReportState.delivered => (
        TokenIcons.deliveryDelivered,
        Strings.delaySentTitle,
        Strings.delaySentDetail(
          total == null
              ? Strings.delayShownUnknown
              : Strings.delayShownKnown(total),
        ),
      ),
      ReportState.pending => (
        TokenIcons.deliveryNotSent,
        Strings.delaySavedTitle,
        Strings.delaySavedDetail,
      ),
      ReportState.rejected => (
        TokenIcons.deliveryRejected,
        Strings.delayRejectedTitle,
        Strings.delayRejectedDetail,
      ),
    };

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            const AppHeader(),
            if (report.state == ReportState.pending)
              Semantics(
                liveRegion: true,
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(
                    horizontal: Space.md,
                    vertical: 12,
                  ),
                  color: colors.textPrimary,
                  child: Row(
                    spacing: 12,
                    children: [
                      Icon(
                        TokenIcons.connectionOffline,
                        size: 22,
                        color: colors.bgPage,
                      ),
                      Expanded(
                        child: Text(
                          Strings.waitingReports(waiting),
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                            color: colors.bgPage,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.all(Space.md),
                children: [
                  const SizedBox(height: Space.lg),
                  Center(
                    child: Container(
                      width: 80,
                      height: 80,
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        color: colors.bgPage,
                        border: Border.all(
                          color: colors.borderDefault,
                          width: 1.5,
                        ),
                      ),
                      child: Icon(icon, size: 40, color: colors.textPrimary),
                    ),
                  ),
                  const SizedBox(height: 14),
                  Center(child: DeliveryChip(report.state)),
                  const SizedBox(height: 14),
                  Semantics(
                    header: true,
                    child: Text(
                      title,
                      textAlign: TextAlign.center,
                      style: context.text.headlineMedium,
                    ),
                  ),
                  const SizedBox(height: 14),
                  Text(
                    summary,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w600,
                      height: 1.4,
                    ),
                  ),
                  const SizedBox(height: 14),
                  Text(
                    detail,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: 15,
                      height: 1.5,
                      color: colors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(
                Space.md,
                0,
                Space.md,
                Space.md,
              ),
              child: Column(
                spacing: 10,
                children: [
                  PrimaryButton(
                    label: Strings.backToJourney,
                    onPressed: () => context.go('/'),
                  ),
                  if (report.state != ReportState.delivered)
                    const PhoneDeskButton(),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

String _trainLine(OperatorJourney? journey) => journey == null
    ? ''
    : Strings.trainTitle(
        journey.trainNumber,
        journey.firstStop.stationName,
        journey.finalStop.stationName,
      );

class _DelayScaffold extends StatelessWidget {
  const _DelayScaffold({
    required this.subtitle,
    required this.title,
    required this.progress,
    required this.body,
    required this.actions,
  });

  final String subtitle;
  final String title;
  final int progress;
  final List<Widget> body;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            const AppHeader(),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.all(Space.md),
                children: [
                  Text(
                    subtitle,
                    style: TextStyle(fontSize: 14, color: colors.textSecondary),
                  ),
                  const SizedBox(height: 2),
                  Semantics(
                    header: true,
                    child: Text(title, style: context.text.headlineMedium),
                  ),
                  const SizedBox(height: 14),
                  Row(
                    spacing: 6,
                    children: [
                      for (var step = 1; step <= 2; step++)
                        Expanded(
                          child: Container(
                            height: 6,
                            decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(3),
                              color: step <= progress
                                  ? colors.textPrimary
                                  : colors.borderDefault,
                            ),
                          ),
                        ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  for (final widget in body) ...[
                    widget,
                    const SizedBox(height: 10),
                  ],
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(
                Space.md,
                0,
                Space.md,
                Space.md,
              ),
              child: Column(spacing: 10, children: actions),
            ),
          ],
        ),
      ),
    );
  }
}

class _Choice extends StatelessWidget {
  const _Choice({
    required this.label,
    required this.selected,
    required this.icon,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Semantics(
      inMutuallyExclusiveGroup: true,
      checked: selected,
      button: true,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(Radii.md),
        child: Container(
          constraints: const BoxConstraints(minHeight: 56),
          padding: const EdgeInsets.symmetric(horizontal: 14),
          decoration: BoxDecoration(
            color: colors.bgPage,
            borderRadius: BorderRadius.circular(Radii.md),
            border: Border.all(
              color: selected ? colors.textPrimary : colors.borderDefault,
              width: selected ? 2 : 1.5,
            ),
          ),
          child: Row(
            spacing: 12,
            children: [
              Icon(
                icon,
                size: 24,
                color: selected ? colors.textPrimary : colors.textSecondary,
              ),
              Expanded(
                child: Text(
                  label,
                  style: TextStyle(
                    fontSize: 17,
                    fontWeight: selected ? FontWeight.w700 : FontWeight.w400,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
