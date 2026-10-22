import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/providers.dart';
import '../../core/l10n/strings.dart';
import '../../core/theme/app_theme.dart';
import '../../core/theme/tokens.g.dart';
import '../../core/widgets/widgets.dart';

/// The three service states an Operations override puts on a train (ADR-05): suspended, cancelled, terminated early. Overrides are not
/// built, so no endpoint returns these states yet (spec 10, boards 16 to 18): the screens exist as widgets, are shown from fixtures in the
/// debug-only design gallery and are covered by tests. Each one is icon plus words, never colour alone (NFR-30).
enum OverrideKind { suspended, cancelled, terminated }

/// How a stop is drawn in the stop list of a disrupted train.
enum FixtureMark { done, suspendedHere, endsHere, notServed, upcoming }

class FixtureStop {
  const FixtureStop(this.name, this.mark, {this.sub, this.time});

  final String name;
  final FixtureMark mark;

  /// The line under the name: "Departed", "Suspended here", "Ends here", "Not served by this train".
  final String? sub;

  /// Already formatted (HH:mm); null shows a dash.
  final String? time;
}

/// Everything one disrupted-train screen shows, so a test or the gallery can build it without a server.
class OverrideFixture {
  const OverrideFixture({
    required this.kind,
    required this.number,
    required this.from,
    required this.destination,
    required this.otherNames,
    required this.fresh,
    required this.stops,
    this.reasonCode,
    this.verifiedAt,
    this.terminatesAt,
  });

  final OverrideKind kind;
  final String number;
  final String from;
  final String destination;
  final String otherNames;

  /// "Updated 08:35", already formatted.
  final String fresh;
  final List<FixtureStop> stops;

  /// The approved reason code (SignalIssue ...) and the time Operations verified it.
  final String? reasonCode;
  final String? verifiedAt;

  /// The last station a terminated train serves.
  final String? terminatesAt;

  static const suspended = OverrideFixture(
    kind: OverrideKind.suspended,
    number: '8058',
    from: 'Veyangoda',
    destination: 'Colombo Fort',
    otherNames: 'කොළඹ කොටුව · கொழும்பு கோட்டை',
    fresh: '08:35',
    reasonCode: 'SignalIssue',
    verifiedAt: '08:35',
    stops: [
      FixtureStop(
        'Veyangoda',
        FixtureMark.done,
        sub: 'Departed',
        time: '08:28',
      ),
      FixtureStop('Gampaha', FixtureMark.suspendedHere, sub: 'Suspended here'),
      FixtureStop('Ragama', FixtureMark.upcoming),
      FixtureStop('Maradana', FixtureMark.upcoming),
      FixtureStop('Colombo Fort', FixtureMark.upcoming),
    ],
  );

  static const cancelled = OverrideFixture(
    kind: OverrideKind.cancelled,
    number: '8062',
    from: 'Veyangoda',
    destination: 'Colombo Fort',
    otherNames: 'කොළඹ කොටුව · கொழும்பு கோட்டை',
    fresh: '08:20',
    verifiedAt: '08:20',
    stops: [],
  );

  static const terminated = OverrideFixture(
    kind: OverrideKind.terminated,
    number: '8058',
    from: 'Veyangoda',
    destination: 'Colombo Fort',
    otherNames: 'කොළඹ කොටුව · கொழும்பு கோட்டை',
    fresh: '08:52',
    verifiedAt: '08:52',
    terminatesAt: 'Ragama',
    stops: [
      FixtureStop(
        'Veyangoda',
        FixtureMark.done,
        sub: 'Departed',
        time: '08:28',
      ),
      FixtureStop('Gampaha', FixtureMark.done, time: '08:38'),
      FixtureStop(
        'Ragama',
        FixtureMark.endsHere,
        sub: 'Ends here',
        time: '08:52',
      ),
      FixtureStop(
        'Maradana',
        FixtureMark.notServed,
        sub: 'Not served by this train',
      ),
      FixtureStop(
        'Colombo Fort',
        FixtureMark.notServed,
        sub: 'Not served by this train',
      ),
    ],
  );

  static OverrideFixture of(OverrideKind kind) => switch (kind) {
    OverrideKind.suspended => suspended,
    OverrideKind.cancelled => cancelled,
    OverrideKind.terminated => terminated,
  };
}

/// One train's live status when an override is in force: the state in words, the verified line, and what the passenger can do next.
class OverrideScreen extends ConsumerWidget {
  const OverrideScreen({required this.fixture, super.key});

  final OverrideFixture fixture;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final c = context.colors;
    final f = fixture;
    final following = ref
        .watch(followsProvider)
        .any((x) => x.trainNumber == f.number);
    final label = switch (f.kind) {
      OverrideKind.suspended => s[K.serviceSuspended],
      OverrideKind.cancelled => s[K.trainCancelled],
      OverrideKind.terminated => s.fmt(K.terminatesAtN, {
        'station': f.terminatesAt ?? '',
      }),
    };
    final body = switch (f.kind) {
      OverrideKind.suspended => s[K.suspendedBody],
      OverrideKind.cancelled => s.fmt(K.cancelledBody, {'n': f.number}),
      OverrideKind.terminated => s.fmt(K.terminatedBody, {
        'station': f.destination,
      }),
    };
    return Scaffold(
      appBar: InnerHeader(
        title: FittedBox(
          fit: BoxFit.scaleDown,
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.circle, size: 10),
              const SizedBox(width: 6),
              Text(
                s.fmt(K.updatedAt, {'time': f.fresh}),
                style: context.text.labelLarge,
              ),
            ],
          ),
        ),
        trailing: IconButton(
          tooltip: s[K.language],
          icon: const Icon(Icons.language),
          onPressed: () => context.push('/language?settings=1'),
        ),
      ),
      body: ListView(
        padding: const EdgeInsets.all(Space.md),
        children: [
          Text(
            '${s.fmt(K.trainN, {'n': f.number})} · ${f.from} → ${f.destination}',
            style: context.text.bodyMedium?.copyWith(color: c.textSecondary),
          ),
          Text(f.destination, style: context.text.headlineMedium),
          Text(
            f.otherNames,
            style: context.text.bodyMedium?.copyWith(color: c.textSecondary),
          ),
          const SizedBox(height: Space.md),
          SectionCard(
            child: MergeSemantics(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (f.kind == OverrideKind.suspended) ...[
                    Text(
                      s.fmt(K.trainN, {'n': f.number}),
                      style: context.text.bodyLarge?.copyWith(
                        color: c.textSecondary,
                      ),
                    ),
                    const SizedBox(height: Space.sm),
                  ],
                  DisruptedChip(label: label),
                  const SizedBox(height: Space.sm + 4),
                  if (f.reasonCode != null) ...[
                    Text(
                      s.fmt(K.reasonLine, {
                        'reason': _reasonName(s, f.reasonCode!),
                      }),
                      style: context.text.bodyLarge?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: Space.xs),
                  ],
                  if (f.kind != OverrideKind.suspended)
                    Text(body, style: context.text.bodyLarge),
                  if (f.verifiedAt != null) ...[
                    const SizedBox(height: Space.xs),
                    Row(
                      children: [
                        Icon(
                          Icons.verified_user_outlined,
                          size: 16,
                          color: c.textSecondary,
                        ),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            s.fmt(K.verifiedByAt, {'time': f.verifiedAt!}),
                            style: context.text.bodySmall?.copyWith(
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ],
                  if (f.kind == OverrideKind.suspended) ...[
                    Divider(height: Space.lg, color: c.borderDefault),
                    Text(
                      body,
                      style: context.text.bodyMedium?.copyWith(
                        color: c.textSecondary,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: Space.md),
          switch (f.kind) {
            OverrideKind.suspended => PrimaryButton(
              label: following ? s[K.stopFollowing] : s[K.follow],
              icon: following
                  ? Icons.bookmark_remove_outlined
                  : Icons.bookmark_add_outlined,
              onPressed: () {
                final n = ref.read(followsProvider.notifier);
                following ? n.unfollow(f.number) : n.follow(f.number);
              },
            ),
            OverrideKind.cancelled => PrimaryButton(
              label: s[K.seeOtherTrains],
              icon: Icons.train,
              onPressed: () => context.go('/trains'),
            ),
            OverrideKind.terminated => SizedBox(
              width: double.infinity,
              height: 52,
              child: OutlinedButton.icon(
                onPressed: () => context.go('/trains'),
                icon: const Icon(Icons.train, size: 20),
                label: Text(
                  s[K.seeOtherTrains],
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                style: OutlinedButton.styleFrom(
                  foregroundColor: c.brandAccent,
                  side: BorderSide(color: c.brandAccent, width: 1.5),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(Radii.md),
                  ),
                ),
              ),
            ),
          },
          if (f.stops.isNotEmpty) ...[
            const SizedBox(height: Space.md),
            _FixtureStopList(
              stops: f.stops,
              connected: f.kind == OverrideKind.terminated,
            ),
          ],
        ],
      ),
    );
  }
}

String _reasonName(Strings s, String code) => switch (code) {
  'SignalIssue' => s[K.reasonSignalIssue],
  'TechnicalFault' => s[K.reasonTechnicalFault],
  'Obstruction' => s[K.reasonObstruction],
  'Weather' => s[K.reasonWeather],
  _ => code,
};

/// The red, filled chip of a disrupted state: a cancel icon and the words (the "disrupted" status group of the design system).
class DisruptedChip extends StatelessWidget {
  const DisruptedChip({required this.label, super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        color: c.statusDisruptedFg,
        borderRadius: BorderRadius.circular(Radii.pill),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.cancel, size: 24, color: c.statusDisruptedOnFilled),
          const SizedBox(width: 8),
          Flexible(
            child: Text(
              label,
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: c.statusDisruptedOnFilled,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _FixtureStopList extends ConsumerWidget {
  const _FixtureStopList({required this.stops, required this.connected});

  final List<FixtureStop> stops;

  /// Draw the vertical line joining the stops (the terminated board).
  final bool connected;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            s[K.stops].toUpperCase(),
            style: context.text.bodySmall?.copyWith(
              fontWeight: FontWeight.w700,
              letterSpacing: .6,
            ),
          ),
          const SizedBox(height: Space.sm),
          for (var i = 0; i < stops.length; i++)
            _FixtureStopRow(
              stop: stops[i],
              first: i == 0,
              last: i == stops.length - 1,
              connected: connected,
            ),
        ],
      ),
    );
  }
}

class _FixtureStopRow extends StatelessWidget {
  const _FixtureStopRow({
    required this.stop,
    required this.first,
    required this.last,
    required this.connected,
  });

  final FixtureStop stop;
  final bool first;
  final bool last;
  final bool connected;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final (icon, color) = switch (stop.mark) {
      FixtureMark.done => (Icons.check, c.textSecondary),
      FixtureMark.suspendedHere => (Icons.cancel, c.statusDisruptedFg),
      FixtureMark.endsHere => (Icons.location_on, c.textPrimary),
      FixtureMark.notServed => (Icons.radio_button_unchecked, c.borderDefault),
      FixtureMark.upcoming => (Icons.radio_button_unchecked, c.textSecondary),
    };
    final dim = stop.mark == FixtureMark.notServed;
    final line = c.borderDefault;
    return IntrinsicHeight(
      child: Row(
        children: [
          SizedBox(
            width: 24,
            child: Column(
              children: [
                Expanded(
                  child: Container(
                    width: 2,
                    color: connected && !first ? line : Colors.transparent,
                  ),
                ),
                Icon(icon, size: 22, color: color),
                Expanded(
                  child: Container(
                    width: 2,
                    color: connected && !last ? line : Colors.transparent,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 6),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    stop.name,
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight:
                          stop.mark == FixtureMark.endsHere ||
                              stop.mark == FixtureMark.suspendedHere
                          ? FontWeight.w700
                          : FontWeight.w400,
                      color: dim ? c.textSecondary : null,
                    ),
                  ),
                  if (stop.sub != null)
                    Text(
                      stop.sub!,
                      style: context.text.bodySmall?.copyWith(
                        color: dim ? c.textSecondary : null,
                      ),
                    ),
                ],
              ),
            ),
          ),
          Center(
            child: Text(
              stop.time ?? '–',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w600,
                color: stop.time == null ? c.textSecondary : c.textPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
