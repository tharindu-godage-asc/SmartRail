import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/strings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/tokens.g.dart';
import '../../../core/widgets/widgets.dart';
import '../domain/models.dart';
import 'journey_controller.dart';

const delayReasons = [
  Strings.reasonSignal,
  Strings.reasonTechnical,
  Strings.reasonObstruction,
  Strings.reasonWeather,
  Strings.reasonUnknown,
];

/// "Total delay" choices. A choice at or below the delay already running is not offered (AC-E03-04 6).
const delayChoices = [10, 15, 20];

List<int> offeredDelays(int runningLate) =>
    delayChoices.where((m) => m > runningLate).toList();

/// Step 1 of 2 (`Nimal-DelayReason`). The screen is built; sending a delay is not, because the server has no endpoint yet.
class DelayReasonScreen extends ConsumerStatefulWidget {
  const DelayReasonScreen({super.key});

  @override
  ConsumerState<DelayReasonScreen> createState() => _DelayReasonScreenState();
}

class _DelayReasonScreenState extends ConsumerState<DelayReasonScreen> {
  int _selected = 0;

  @override
  Widget build(BuildContext context) {
    final journey = ref.watch(journeyControllerProvider).value?.journey;

    return _DelayScaffold(
      subtitle: '${_trainLine(journey)} · ${Strings.stepOfTwo(1)}',
      title: Strings.delayReasonTitle,
      progress: 1,
      body: [
        for (var i = 0; i < delayReasons.length; i++)
          _Choice(
            label: delayReasons[i],
            selected: i == _selected,
            icon: i == _selected
                ? Icons.radio_button_checked
                : TokenIcons.stopUpcoming,
            onTap: () => setState(() => _selected = i),
          ),
      ],
      actions: [
        PrimaryButton(
          label: Strings.next,
          onPressed: () => context.push('/delay/estimate'),
        ),
        SecondaryButton(label: Strings.cancel, onPressed: () => context.pop()),
      ],
    );
  }
}

/// Step 2 of 2 (`Nimal-DelayEstimate`). "Send report" stays off until the server can take delay reports.
class DelayEstimateScreen extends ConsumerStatefulWidget {
  const DelayEstimateScreen({super.key});

  @override
  ConsumerState<DelayEstimateScreen> createState() =>
      _DelayEstimateScreenState();
}

class _DelayEstimateScreenState extends ConsumerState<DelayEstimateScreen> {
  /// null is "Unknown".
  int? _selected;

  @override
  Widget build(BuildContext context) {
    final view = ref.watch(journeyControllerProvider).value;
    final runningLate = view?.status?.delayMinutes ?? 0;
    final offered = offeredDelays(runningLate);

    return _DelayScaffold(
      subtitle: '${_trainLine(view?.journey)} · ${Strings.stepOfTwo(2)}',
      title: Strings.delayEstimateTitle,
      progress: 2,
      body: [
        Text.rich(
          TextSpan(
            text: 'Running ',
            children: [
              TextSpan(
                text: '+$runningLate min',
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
              const TextSpan(text: ' late now'),
            ],
          ),
          style: const TextStyle(fontSize: 16),
        ),
        for (final minutes in offered)
          _Choice(
            label: '+$minutes min',
            selected: _selected == minutes,
            icon: _selected == minutes
                ? Icons.radio_button_checked
                : TokenIcons.stopUpcoming,
            onTap: () => setState(() => _selected = minutes),
          ),
        _Choice(
          label: Strings.reasonUnknown,
          selected: _selected == null,
          icon: _selected == null
              ? Icons.radio_button_checked
              : TokenIcons.stopUpcoming,
          onTap: () => setState(() => _selected = null),
        ),
        const InfoLine(
          'Total delay against the timetable, not extra minutes. Options below your current delay are not offered.',
        ),
        const InfoLine(Strings.delayUnavailable, icon: Icons.call),
      ],
      actions: [
        // Disabled on purpose: AC-E03-04 needs a backend slice first (spec operator-app, Non-Goals).
        const PrimaryButton(label: Strings.sendReport, onPressed: null),
        SecondaryButton(label: Strings.back, onPressed: () => context.pop()),
      ],
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
