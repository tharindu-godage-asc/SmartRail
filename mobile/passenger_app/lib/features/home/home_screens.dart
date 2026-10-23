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

/// Home, as drawn (`Passenger · Home`): the logo bar, the line status, "Where to today?" with From, To, swap and Find trains, the followed
/// trains (AC-E06-04 1) and Quick start (the usual trip and its reverse, from what the phone remembers).
class HomeTab extends ConsumerWidget {
  const HomeTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final follows = ref.watch(followsProvider);
    final line = ref.watch(lineStatusProvider);
    final sel = ref.watch(selectionProvider);
    final stations = ref.watch(stationsProvider).value;
    final prefs = ref.watch(preferencesProvider);
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

    // Quick start: the origin and destination the phone remembers, and the way back. Phone-only, nothing from a server.
    final usualFrom = prefs.fromCode;
    final usualTo = prefs.usualTo;
    final hasUsual =
        usualFrom != null && usualTo != null && usualFrom != usualTo;
    String name(String code) => stationName(stations, code, lang);

    void useTrip(String from, String to) {
      ref.read(selectionProvider.notifier).use(from, to);
      context.push('/results');
    }

    return ListView(
      padding: EdgeInsets.zero,
      children: [
        Container(
          height: 56,
          padding: const EdgeInsets.symmetric(horizontal: Space.md),
          decoration: BoxDecoration(
            color: c.bgPage,
            border: Border(bottom: BorderSide(color: c.borderDefault)),
          ),
          child: Row(
            children: [
              Icon(Icons.train, color: c.brandAccent),
              const SizedBox(width: Space.sm),
              Expanded(
                child: Text(
                  s[K.appName],
                  style: context.text.labelLarge?.copyWith(fontSize: 18),
                ),
              ),
              IconButton(
                tooltip: s[K.language],
                icon: const Icon(Icons.language),
                onPressed: () => context.push('/language?settings=1'),
              ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(Space.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (lineText != null) ...[
                Align(
                  alignment: Alignment.centerLeft,
                  child: StatusChip(kind: lineKind, label: lineText),
                ),
                const SizedBox(height: Space.md),
              ],
              Semantics(
                header: true,
                child: Text(
                  s[K.whereToday],
                  style: context.text.headlineMedium,
                ),
              ),
              const SizedBox(height: Space.md),
              SectionCard(
                child: Column(
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Column(
                            children: [
                              _HomeField(
                                label: s[K.from],
                                value: sel.from == null
                                    ? s[K.chooseFrom]
                                    : name(sel.from!),
                                placeholder: sel.from == null,
                                onTap: () => context.push('/pick/from'),
                              ),
                              const SizedBox(height: Space.sm),
                              _HomeField(
                                label: s[K.to],
                                value: sel.to == null
                                    ? s[K.chooseTo]
                                    : name(sel.to!),
                                placeholder: sel.to == null,
                                onTap: () => context.push('/pick/to'),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(width: Space.sm),
                        Semantics(
                          button: true,
                          label: s[K.swapStations],
                          child: InkWell(
                            customBorder: const CircleBorder(),
                            onTap: sel.from == null && sel.to == null
                                ? null
                                : () => ref
                                      .read(selectionProvider.notifier)
                                      .swap(),
                            child: Container(
                              width: 48,
                              height: 48,
                              decoration: BoxDecoration(
                                shape: BoxShape.circle,
                                color: c.bgPage,
                                border: Border.all(color: c.borderDefault),
                              ),
                              child: Icon(
                                Icons.swap_vert,
                                color: c.brandAccent,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: Space.md),
                    PrimaryButton(
                      label: s[K.findTrains],
                      icon: Icons.search,
                      onPressed: sel.ready
                          ? () => context.push('/results')
                          : null,
                    ),
                  ],
                ),
              ),
              if (follows.isNotEmpty) ...[
                const SizedBox(height: Space.lg),
                _HomeSection(s[K.followingTitle]),
                const SizedBox(height: Space.sm),
                for (final f in follows) ...[
                  FollowedCard(follow: f),
                  const SizedBox(height: Space.sm),
                ],
              ],
              if (hasUsual) ...[
                const SizedBox(height: Space.lg),
                _HomeSection(s[K.quickStart]),
                const SizedBox(height: Space.xs),
                _QuickTrip(
                  title: '${name(usualFrom)} → ${name(usualTo)}',
                  subtitle: s[K.usualTrip],
                  onTap: () => useTrip(usualFrom, usualTo),
                ),
                Divider(height: 1, color: c.borderDefault),
                _QuickTrip(
                  title: '${name(usualTo)} → ${name(usualFrom)}',
                  subtitle: s[K.goingHome],
                  onTap: () => useTrip(usualTo, usualFrom),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

/// A small uppercase heading ("FOLLOWING", "QUICK START").
class _HomeSection extends StatelessWidget {
  const _HomeSection(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Semantics(
    header: true,
    child: Text(
      text.toUpperCase(),
      style: context.text.bodySmall?.copyWith(
        fontWeight: FontWeight.w700,
        letterSpacing: 0.6,
        color: context.colors.textSecondary,
      ),
    ),
  );
}

/// A From or To field of the Home card: the label on the left, the station beside it.
class _HomeField extends StatelessWidget {
  const _HomeField({
    required this.label,
    required this.value,
    required this.placeholder,
    required this.onTap,
  });

  final String label;
  final String value;
  final bool placeholder;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Semantics(
      button: true,
      label: '$label: ${placeholder ? '' : value}',
      child: InkWell(
        borderRadius: BorderRadius.circular(Radii.md),
        onTap: onTap,
        child: Container(
          constraints: const BoxConstraints(minHeight: 48),
          padding: const EdgeInsets.symmetric(horizontal: Space.md),
          decoration: BoxDecoration(
            color: c.bgPage,
            border: Border.all(color: c.borderDefault),
            borderRadius: BorderRadius.circular(Radii.md),
          ),
          child: ExcludeSemantics(
            child: Row(
              children: [
                SizedBox(
                  width: 48,
                  child: Text(
                    label,
                    style: context.text.bodySmall?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                Expanded(
                  child: Text(
                    value,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w600,
                      color: placeholder ? c.textSecondary : c.textPrimary,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _QuickTrip extends StatelessWidget {
  const _QuickTrip({
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return ListTile(
      contentPadding: EdgeInsets.zero,
      minTileHeight: 64,
      leading: Icon(Icons.history, color: c.brandAccent),
      title: Text(
        title,
        style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700),
      ),
      subtitle: Text(
        subtitle,
        style: TextStyle(fontSize: 13, color: c.textSecondary),
      ),
      trailing: Icon(Icons.chevron_right, color: c.textSecondary),
      onTap: onTap,
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
              PrimaryButton(
                label: s[K.ticketsSignIn],
                onPressed: () => context.push('/signin'),
              ),
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
