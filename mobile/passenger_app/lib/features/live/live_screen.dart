import 'package:clock/clock.dart';
import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/providers.dart';
import '../../core/l10n/strings.dart';
import '../../core/theme/app_theme.dart';
import '../../core/theme/tokens.g.dart';
import '../../core/time.dart';
import '../../core/widgets/widgets.dart';
import '../../data/models.dart';
import '../../domain/logic.dart';
import '../food/food_menu_screen.dart';
import '../search/search_screens.dart';

/// One train's live status (AC-E06-02, AC-E06-03). The primary ETA is at the passenger's station until the train leaves it, then at the
/// destination; one stop list shows progress and ETA together. No reason is shown: nothing verifies one yet (D-10).
class LiveScreen extends ConsumerStatefulWidget {
  const LiveScreen({required this.number, this.from, this.to, super.key});

  final String number;
  final String? from;
  final String? to;

  @override
  ConsumerState<LiveScreen> createState() => _LiveScreenState();
}

class _LiveScreenState extends ConsumerState<LiveScreen> {
  void _announce(Polled<TrainStatus?>? before, Polled<TrainStatus?> after) {
    final data = after.data;
    if (data == null) return;
    final network = ref.read(hasNetworkProvider).value ?? true;
    final now = clock.now().toUtc();
    final a = announcementFor(
      beforeKind: before?.data == null ? null : kindOfStatus(before!.data!),
      afterKind: kindOfStatus(data),
      beforeLink: before?.link(hasNetwork: network, now: now),
      afterLink: after.link(hasNetwork: network, now: now),
    );
    if (a == null) return;
    final s = ref.read(stringsProvider);
    final text = switch (a) {
      Announcement.delayed => s.fmt(K.delayedFor, {
        't': formatMinutes(data.delayMinutes),
      }),
      Announcement.waiting => s[K.waiting],
      Announcement.onTime => s[K.onTime],
      Announcement.arrived => s[K.arrived],
      Announcement.offline => s.fmt(K.offlineBanner, {
        'time': formatTime(data.serverTime),
      }),
      Announcement.back => s.fmt(K.updatedAt, {
        'time': formatTime(data.lastEventAt ?? data.serverTime),
      }),
    };
    SemanticsService.sendAnnouncement(
      View.of(context),
      text,
      Directionality.of(context),
    );
  }

  @override
  Widget build(BuildContext context) {
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final polled = ref.watch(trainStatusProvider(widget.number));
    ref.listen<Polled<TrainStatus?>>(trainStatusProvider(widget.number), (
      before,
      after,
    ) {
      _announce(before, after);
      if (after.data?.completed == true) {
        ref
            .read(followsProvider.notifier)
            .unfollow(widget.number); // a finished journey is not followed
      }
    });
    final stations = ref.watch(stationsProvider).value;
    final network = hasNetwork(ref);
    final data = polled.data;
    final link = polled.link(hasNetwork: network, now: clock.now().toUtc());
    final c = context.colors;

    // The freshness line is in the body, not the header: it can wrap at 200 % text instead of overflowing (AC-E06-05 3).
    PreferredSizeWidget header() => InnerHeader(
      title: const SizedBox.shrink(),
      trailing: IconButton(
        tooltip: s[K.language],
        icon: const Icon(Icons.language),
        onPressed: () => context.push('/language?settings=1'),
      ),
    );

    if (data == null) {
      final notFound = polled.loaded && polled.lastSuccess != null;
      return Scaffold(
        appBar: header(),
        body: notFound
            ? Notice(
                icon: Icons.search_off,
                title: s[K.notFoundTitle],
                body: s[K.notFoundBody],
              ) // G2
            : polled.loaded
            ? Column(
                children: [
                  LinkBanner(text: s.fmt(K.unavailableBanner, {'time': '–'})),
                  const Spacer(),
                ],
              )
            : const Center(child: CircularProgressIndicator()),
      );
    }

    final kind = kindOfStatus(data);
    final eta = primaryEta(data, fromCode: widget.from, toCode: widget.to);
    final first = data.stops.isEmpty ? null : data.stops.first;
    final last = data.stops.isEmpty ? null : data.stops.last;
    final toCode = widget.to ?? last?.code;
    final fromCode = widget.from ?? first?.code;
    final destinationName = toCode == null
        ? ''
        : stationName(stations, toCode, lang);
    final others = [
      for (final l in Lang.values)
        if (l != lang && toCode != null) stationName(stations, toCode, l),
    ];
    final fresh = data.lastEventAt ?? data.serverTime;
    final delayText = formatMinutes(data.delayMinutes);
    final following = ref
        .watch(followsProvider)
        .any((f) => f.trainNumber == widget.number);

    return Scaffold(
      appBar: header(),
      body: Column(
        children: [
          if (link == LinkStatus.unavailable)
            LinkBanner(
              text: s.fmt(
                link == LinkStatus.offline
                    ? K.offlineBanner
                    : K.unavailableBanner,
                {'time': formatTime(data.serverTime)},
              ),
            ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(Space.md),
              children: [
                Wrap(
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: 8,
                  children: [
                    Icon(
                      link == LinkStatus.live
                          ? Icons.circle
                          : Icons.circle_outlined,
                      size: 12,
                    ),
                    Text(
                      s.fmt(
                        link == LinkStatus.live ? K.updatedAt : K.lastUpdate,
                        {'time': formatTime(fresh)},
                      ),
                      style: context.text.labelLarge,
                    ),
                  ],
                ),
                const SizedBox(height: Space.sm),
                Text(
                  '${s.fmt(K.trainN, {'n': widget.number})}${fromCode == null || toCode == null ? '' : ' · ${stationName(stations, fromCode, lang)} → $destinationName'}',
                  style: context.text.bodyMedium?.copyWith(
                    color: c.textSecondary,
                  ),
                ),
                Text(destinationName, style: context.text.headlineMedium),
                if (others.isNotEmpty)
                  Text(
                    others.join(' · '),
                    style: context.text.bodyMedium?.copyWith(
                      color: c.textSecondary,
                    ),
                  ),
                const SizedBox(height: Space.md),
                if (data.completed)
                  SectionCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        StatusChip(
                          kind: StatusKind.arrived,
                          label: s[K.arrived],
                          large: true,
                        ),
                        const SizedBox(height: Space.sm),
                        Text(
                          s[K.arrivedBody],
                          style: context.text.bodyLarge,
                        ), // G2
                      ],
                    ),
                  )
                else
                  _EtaCard(
                    s: s,
                    data: data,
                    eta: eta,
                    kind: kind,
                    link: link,
                    delayText: delayText,
                    stations: stations,
                    lang: lang,
                    fresh: fresh,
                  ),
                if (!data.completed &&
                    data.reasonCode != null &&
                    data.reasonVerifiedAt != null) ...[
                  const SizedBox(height: Space.sm),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(Icons.verified_outlined, size: 20),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          s.fmt(K.reasonVerified, {
                            'reason': _reasonText(s, data.reasonCode!),
                            'time': formatTime(data.reasonVerifiedAt!),
                          }),
                          style: context.text.bodyMedium,
                        ),
                      ),
                    ],
                  ),
                ],
                if (!data.completed &&
                    data.reasonCode == null &&
                    (data.delayMinutes > 0 || !data.delayKnown)) ...[
                  const SizedBox(height: Space.sm),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(Icons.hourglass_empty, size: 20),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          s[K.reasonPending],
                          style: context.text.bodyMedium,
                        ),
                      ),
                    ],
                  ),
                ],
                const SizedBox(height: Space.md),
                if (!data.completed)
                  PrimaryButton(
                    label: following ? s[K.stopFollowing] : s[K.follow],
                    icon: following
                        ? Icons.bookmark_remove_outlined
                        : Icons.bookmark_add_outlined,
                    onPressed: () {
                      final f = ref.read(followsProvider.notifier);
                      following
                          ? f.unfollow(widget.number)
                          : f.follow(
                              widget.number,
                              from: widget.from,
                              to: widget.to,
                            );
                    },
                  ),
                const SizedBox(height: Space.md),
                FoodMenuRow(
                  number: widget.number,
                  from: widget.from,
                  to: widget.to,
                ),
                _StopList(
                  s: s,
                  data: data,
                  eta: eta,
                  stations: stations,
                  lang: lang,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// The approved reason codes the server can publish; an unknown code is shown as it is.
String _reasonText(Strings s, String code) => switch (code) {
  'SignalIssue' => s[K.reasonSignalIssue],
  'TechnicalFault' => s[K.reasonTechnicalFault],
  'Obstruction' => s[K.reasonObstruction],
  'Weather' => s[K.reasonWeather],
  _ => code,
};

class _EtaCard extends StatelessWidget {
  const _EtaCard({
    required this.s,
    required this.data,
    required this.eta,
    required this.kind,
    required this.link,
    required this.delayText,
    required this.stations,
    required this.lang,
    required this.fresh,
  });

  final Strings s;
  final TrainStatus data;
  final PrimaryEta? eta;
  final StatusKind kind;
  final LinkStatus link;
  final String delayText;
  final List<Station>? stations;
  final Lang lang;
  final DateTime fresh;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final stopName = eta == null
        ? ''
        : stationName(stations, eta!.stop.code, lang);
    final stale = link != LinkStatus.live;
    final seen = data.lastSeen;
    return SectionCard(
      child: MergeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              stale && eta?.time != null
                  ? s[K.lastKnown]
                  : s.fmt(
                      kind == StatusKind.waiting ? K.arrivalAt : K.arrivesAt,
                      {'station': stopName},
                    ),
              style: context.text.bodyLarge?.copyWith(color: c.textSecondary),
            ),
            const SizedBox(height: Space.sm),
            if (kind == StatusKind.waiting)
              StatusChip(
                kind: kind,
                label: s[K.waiting],
                large: true,
              ) // D-39: no ETA, said plainly
            else
              Wrap(
                alignment: WrapAlignment.spaceBetween,
                crossAxisAlignment: WrapCrossAlignment.center,
                spacing: Space.sm,
                runSpacing: Space.sm,
                children: [
                  Text(
                    eta?.time == null ? '–' : formatTime(eta!.time!),
                    style: TextStyle(
                      fontSize: TypeSize.mobileTime,
                      fontWeight: FontWeight.w700,
                      height: 1,
                      color: stale ? c.textSecondary : c.textPrimary,
                    ),
                  ),
                  StatusChip(kind: kind, label: statusText(s, kind, delayText)),
                ],
              ),
            const SizedBox(height: Space.sm),
            if (kind == StatusKind.waiting) ...[
              Text(
                s.fmt(K.noNewsYet, {'time': formatTime(fresh)}),
                style: context.text.bodyLarge,
              ),
              if (seen != null)
                Text(
                  s.fmt(
                    seen.state == 'Arrived'
                        ? K.lastSeenArrived
                        : K.lastSeenDeparted,
                    {
                      'station': stationName(stations, seen.code, lang),
                      'time': formatTime(seen.at),
                    },
                  ),
                  style: context.text.bodyMedium?.copyWith(
                    color: c.textSecondary,
                  ),
                ),
            ] else if (kind == StatusKind.delayed)
              Text(
                s.fmt(K.aboutLate, {'t': delayText, 'time': formatTime(fresh)}),
                style: context.text.bodyMedium?.copyWith(
                  color: c.textSecondary,
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _StopList extends StatelessWidget {
  const _StopList({
    required this.s,
    required this.data,
    required this.eta,
    required this.stations,
    required this.lang,
  });

  final Strings s;
  final TrainStatus data;
  final PrimaryEta? eta;
  final List<Station>? stations;
  final Lang lang;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    // The first stop with nothing reported yet is "next": it gets the pin, like the design's Gampaha.
    final next = data.stops
        .where((p) => p.confirmation == 'Pending')
        .firstOrNull;
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
          for (final p in data.stops)
            _StopRow(
              s: s,
              data: data,
              stop: p,
              isNext: identical(p, next),
              stations: stations,
              lang: lang,
              c: c,
            ),
        ],
      ),
    );
  }
}

class _StopRow extends StatelessWidget {
  const _StopRow({
    required this.s,
    required this.data,
    required this.stop,
    required this.isNext,
    required this.stations,
    required this.lang,
    required this.c,
  });

  final Strings s;
  final TrainStatus data;
  final StopView stop;
  final bool isNext;
  final List<Station>? stations;
  final Lang lang;
  final SmartRailColors c;

  @override
  Widget build(BuildContext context) {
    final passed = stop.confirmation == 'Inferred'; // a stop passed with no report: "Passed", never "missing" (AC-E06-02 5, P-07)
    final departed = stop.actualDeparture != null;
    final scheduled = stop.scheduledArrival ?? stop.scheduledDeparture;
    final estimated = stop.estimatedArrival ?? stop.estimatedDeparture;

    String? sub;
    DateTime? shown;
    if (departed) {
      sub = s[K.departed];
      shown = stop.actualDeparture;
    } else if (passed) {
      sub = s[K.passedStop];
    } else if (stop.actualArrival != null) {
      shown = stop.actualArrival;
    } else {
      shown = data.delayKnown ? estimated : null;
      if (isNext && scheduled != null && shown != null && shown != scheduled) {
        sub = '${s[K.sched]} ${formatTime(scheduled)}';
      }
    }
    final icon = departed || passed || stop.actualArrival != null
        ? (Icons.check, c.textSecondary)
        : isNext
        ? (Icons.location_on, c.brandAccent)
        : (Icons.radio_button_unchecked, c.textSecondary);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Icon(icon.$1, size: 22, color: icon.$2),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  stationName(stations, stop.code, lang),
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: isNext
                        ? FontWeight.w700
                        : (departed ? FontWeight.w600 : FontWeight.w400),
                  ),
                ),
                if (sub != null) Text(sub, style: context.text.bodySmall),
              ],
            ),
          ),
          Text(
            shown == null ? '–' : formatTime(shown),
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w600,
              color: shown == null ? c.textSecondary : c.textPrimary,
            ),
          ),
        ],
      ),
    );
  }
}
