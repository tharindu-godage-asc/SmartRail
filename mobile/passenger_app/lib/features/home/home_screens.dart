import 'package:clock/clock.dart';
import 'package:flutter/foundation.dart' show kDebugMode;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/providers.dart';
import '../../core/l10n/strings.dart';
import '../../core/theme/app_theme.dart';
import '../../core/theme/tokens.g.dart';
import '../../core/time.dart';
import '../../core/widgets/widgets.dart';
import '../../data/preferences.dart';
import '../../domain/logic.dart';
import '../search/search_screens.dart';

/// Home: the line status, then the followed train (AC-E06-04 1) or, with none, search first and no empty card (AC-E06-04 2).
class HomeTab extends ConsumerWidget {
  const HomeTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final follows = ref.watch(followsProvider);
    final line = ref.watch(lineStatusProvider);
    final c = context.colors;

    // G6: with no answer for the line, say so instead of leaving a hole or inventing "all normal".
    final lineText = line.data == null
        ? (line.loaded ? s[K.lineUnavailable] : null)
        : line.data!.inProgress == 0
        ? s[K.lineNone]
        : s[line.data!.statusKey == 'SomeDelayed'
              ? K.lineSomeDelayed
              : K.lineNormal];
    final lineKind = line.data?.statusKey == 'SomeDelayed'
        ? StatusKind.delayed
        : (line.data == null ? StatusKind.waiting : StatusKind.onTime);

    return ListView(
      padding: const EdgeInsets.all(Space.md),
      children: [
        Row(
          children: [
            Expanded(
              child: Text(s[K.appName], style: context.text.headlineMedium),
            ),
            IconButton(
              tooltip: s[K.language],
              icon: const Icon(Icons.language),
              onPressed: () => context.push('/language?settings=1'),
            ),
          ],
        ),
        if (lineText != null) ...[
          const SizedBox(height: Space.sm),
          Align(
            alignment: Alignment.centerLeft,
            child: StatusChip(kind: lineKind, label: lineText),
          ),
        ],
        const SizedBox(height: Space.lg),
        if (follows.isEmpty) ...[
          PrimaryButton(
            label: s[K.findTrain],
            icon: Icons.search,
            onPressed: () => context.go('/trains'),
          ),
        ] else ...[
          Text(
            s[K.yourTrain],
            style: context.text.bodySmall?.copyWith(
              fontWeight: FontWeight.w700,
              color: c.textSecondary,
            ),
          ),
          const SizedBox(height: Space.sm),
          for (final f in follows) ...[
            FollowedCard(follow: f),
            const SizedBox(height: Space.sm),
          ],
          const SizedBox(height: Space.sm),
          OutlinedButton.icon(
            onPressed: () => context.go('/trains'),
            icon: const Icon(Icons.search),
            label: Text(s[K.findTrain]),
          ),
        ],
      ],
    );
  }
}

/// A followed train: the ETA, the status and how fresh it is, with a ring instead of a dot when the information is old.
class FollowedCard extends ConsumerWidget {
  const FollowedCard({required this.follow, super.key});

  final Follow follow;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final stations = ref.watch(stationsProvider).value;
    final polled = ref.watch(trainStatusProvider(follow.trainNumber));
    final data = polled.data;
    // A follow ends when the journey is Completed (rule 10); the card shows "Arrived" for that one last moment.
    ref.listen(trainStatusProvider(follow.trainNumber), (_, next) {
      final d = next.data;
      if (d != null && d.completed) {
        ref.read(followsProvider.notifier).unfollow(follow.trainNumber);
      }
    });
    final link = polled.link(
      hasNetwork: hasNetwork(ref),
      now: clock.now().toUtc(),
    );
    final c = context.colors;
    final eta = data == null
        ? null
        : primaryEta(data, fromCode: follow.fromCode, toCode: follow.toCode);
    final kind = data == null ? null : kindOfStatus(data);
    final stopName = eta == null
        ? null
        : stationName(stations, eta.stop.code, lang);
    return InkWell(
      borderRadius: BorderRadius.circular(Radii.lg),
      onTap: () => context.push(
        '/train/${follow.trainNumber}?from=${follow.fromCode ?? ''}&to=${follow.toCode ?? ''}',
      ),
      child: SectionCard(
        child: data == null
            ? Text(
                polled.loaded && polled.lastSuccess != null
                    ? s[K.notFoundTitle]
                    : s.fmt(K.trainN, {'n': follow.trainNumber}),
              )
            : Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    s.fmt(K.trainN, {'n': follow.trainNumber}),
                    style: context.text.bodyMedium?.copyWith(
                      color: c.textSecondary,
                    ),
                  ),
                  if (stopName != null)
                    Text(
                      s.fmt(K.arrivesAt, {'station': stopName}),
                      style: context.text.labelLarge,
                    ),
                  const SizedBox(height: Space.sm),
                  Wrap(
                    alignment: WrapAlignment.spaceBetween,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    spacing: Space.sm,
                    runSpacing: Space.sm,
                    children: [
                      Text(
                        eta?.time == null ? '–' : formatTime(eta!.time!),
                        style: TextStyle(
                          fontSize: 40,
                          fontWeight: FontWeight.w700,
                          height: 1,
                          color: link == LinkStatus.live
                              ? c.textPrimary
                              : c.textSecondary,
                        ),
                      ),
                      StatusChip(
                        kind: kind!,
                        label: statusText(
                          s,
                          kind,
                          formatMinutes(data.delayMinutes),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: Space.sm),
                  Row(
                    children: [
                      Icon(
                        link == LinkStatus.live
                            ? Icons.circle
                            : Icons.circle_outlined,
                        size: 10,
                        color: c.textSecondary,
                      ),
                      const SizedBox(width: 6),
                      Text(
                        link == LinkStatus.live
                            ? s.fmt(K.updatedAt, {
                                'time': formatTime(
                                  data.lastEventAt ?? data.serverTime,
                                ),
                              })
                            : s.fmt(K.lastUpdate, {
                                'time': formatTime(data.serverTime),
                              }),
                        style: context.text.bodySmall,
                      ),
                    ],
                  ),
                ],
              ),
      ),
    );
  }
}

/// Alerts: the trains you follow. Messages from Operations do not exist yet, so that part says there are none (G3).
class AlertsTab extends ConsumerWidget {
  const AlertsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final follows = ref.watch(followsProvider);
    return ListView(
      padding: const EdgeInsets.all(Space.md),
      children: [
        Text(s[K.alertsTitle], style: context.text.headlineMedium),
        const SizedBox(height: Space.md),
        Text(
          s[K.trainsYouFollow],
          style: context.text.bodySmall?.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: Space.sm),
        if (follows.isEmpty)
          Text(s[K.noFollowed], style: context.text.bodyLarge)
        else
          for (final f in follows) ...[
            FollowedCard(follow: f),
            const SizedBox(height: Space.sm),
          ],
        const SizedBox(height: Space.lg),
        SectionCard(
          child: Row(
            children: [
              const Icon(Icons.notifications_none),
              const SizedBox(width: Space.sm),
              Expanded(child: Text(s[K.noAlerts])),
            ],
          ),
        ),
      ],
    );
  }
}

/// My tickets, as designed (`Passenger · My tickets (empty)`): a back arrow and title, an empty state, and the two buttons that lead to buying a
/// ticket. Ticketing and accounts are not in this version (spec 10 Q5), so the screen shows the design but the two buttons only say so.
class TicketsTab extends ConsumerWidget {
  const TicketsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final c = context.colors;
    void unavailable() {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(s[K.ticketsBody])));
    }

    return Column(
      children: [
        Container(
          height: 56,
          decoration: BoxDecoration(
            color: c.bgPage,
            border: Border(bottom: BorderSide(color: c.borderDefault)),
          ),
          child: Stack(
            alignment: Alignment.center,
            children: [
              Align(
                alignment: Alignment.centerLeft,
                child: IconButton(
                  tooltip: s[K.back],
                  icon: const Icon(Icons.arrow_back),
                  onPressed: () => context.go('/home'),
                ),
              ),
              Semantics(
                header: true,
                child: Text(
                  s[K.ticketsTitle],
                  style: context.text.labelLarge?.copyWith(fontSize: 18),
                ),
              ),
            ],
          ),
        ),
        Expanded(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: Space.lg),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 88,
                    height: 88,
                    decoration: BoxDecoration(
                      color: c.brandAccentSurface,
                      shape: BoxShape.circle,
                    ),
                    child: Icon(
                      Icons.confirmation_number_outlined,
                      size: 40,
                      color: c.brandAccent,
                    ),
                  ),
                  const SizedBox(height: Space.lg),
                  Text(
                    s[K.ticketsEmptyTitle],
                    textAlign: TextAlign.center,
                    style: context.text.headlineMedium,
                  ),
                  const SizedBox(height: Space.sm),
                  Text(
                    s[K.ticketsEmptyBody],
                    textAlign: TextAlign.center,
                    style: context.text.bodyMedium?.copyWith(
                      color: c.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(
            Space.md,
            Space.sm,
            Space.md,
            Space.lg,
          ),
          child: Column(
            children: [
              PrimaryButton(label: s[K.ticketsSignIn], onPressed: unavailable),
              const SizedBox(height: Space.sm + 4),
              SizedBox(
                width: double.infinity,
                height: 52,
                child: OutlinedButton(
                  onPressed: unavailable,
                  style: OutlinedButton.styleFrom(
                    foregroundColor: c.brandAccent,
                    side: BorderSide(color: c.brandAccent, width: 1.5),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(Radii.md),
                    ),
                  ),
                  child: Text(
                    s[K.ticketsBuy],
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Account (signed out) and Settings: language, followed trains with a way to remove them, and what is kept on the phone (G5).
class AccountTab extends ConsumerWidget {
  const AccountTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final follows = ref.watch(followsProvider);
    return ListView(
      padding: const EdgeInsets.all(Space.md),
      children: [
        Text(s[K.settingsTitle], style: context.text.headlineMedium),
        const SizedBox(height: Space.md),
        SectionCard(
          padding: EdgeInsets.zero,
          child: ListTile(
            minTileHeight: 56,
            title: Text(s[K.language]),
            subtitle: Text(lang.label),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.push('/language?settings=1'),
          ),
        ),
        const SizedBox(height: Space.md),
        Text(
          s[K.followedTrains],
          style: context.text.bodySmall?.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: Space.sm),
        if (follows.isEmpty)
          Text(s[K.noFollowed], style: context.text.bodyLarge)
        else
          SectionCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                for (final f in follows)
                  ListTile(
                    minTileHeight: 56,
                    title: Text(s.fmt(K.trainN, {'n': f.trainNumber})),
                    trailing: TextButton(
                      onPressed: () => ref
                          .read(followsProvider.notifier)
                          .unfollow(f.trainNumber),
                      child: Text(s[K.remove]),
                    ),
                  ),
              ],
            ),
          ),
        const SizedBox(height: Space.lg),
        Text(
          s[K.privacyTitle],
          style: context.text.bodySmall?.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: Space.sm),
        Text(s[K.privacyBody], style: context.text.bodyMedium),
        if (kDebugMode) ...[
          const SizedBox(height: Space.lg),
          SectionCard(
            padding: EdgeInsets.zero,
            child: ListTile(
              minTileHeight: 56,
              leading: const Icon(Icons.science_outlined),
              title: Text(s[K.galleryTitle]),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/gallery'),
            ),
          ),
        ],
      ],
    );
  }
}
