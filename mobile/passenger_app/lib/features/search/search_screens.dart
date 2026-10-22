import 'package:clock/clock.dart';
import 'package:flutter/material.dart';
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

/// The name of a station in the chosen language, from the cached list (the code if it is not there).
String stationName(List<Station>? stations, String code, Lang lang) {
  for (final s in stations ?? const <Station>[]) {
    if (s.code == code) return s.name(lang);
  }
  return code;
}

/// The "Trains" tab: choose From and To, then see the trains (AC-E06-01).
class TrainsTab extends ConsumerWidget {
  const TrainsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final sel = ref.watch(selectionProvider);
    final stations = ref.watch(stationsProvider);
    final list = stations.value;
    final c = context.colors;

    if (stations.hasError && list == null) {
      // First launch, offline, nothing cached (G4): everything else still works, so this only explains the pickers.
      return Notice(
        icon: Icons.cloud_off,
        title: s[K.connectOnce],
        action: OutlinedButton(
          onPressed: () => ref.invalidate(stationsProvider),
          child: Text(s[K.retry]),
        ),
      );
    }

    Widget tile(K label, String? code, String which) => Semantics(
      button: true,
      label:
          '${s[label]}: ${code == null ? '' : stationName(list, code, lang)}',
      child: InkWell(
        borderRadius: BorderRadius.circular(Radii.lg),
        onTap: () => context.push('/pick/$which'),
        child: Container(
          constraints: const BoxConstraints(minHeight: 64),
          padding: const EdgeInsets.symmetric(
            horizontal: Space.md,
            vertical: Space.sm,
          ),
          decoration: BoxDecoration(
            color: c.bgPage,
            border: Border.all(color: c.borderDefault),
            borderRadius: BorderRadius.circular(Radii.lg),
          ),
          child: Row(
            children: [
              Expanded(
                child: ExcludeSemantics(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(s[label], style: context.text.bodySmall),
                      Text(
                        code == null
                            ? (which == 'from'
                                  ? s[K.chooseFrom]
                                  : s[K.chooseTo])
                            : stationName(list, code, lang),
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w600,
                          color: code == null ? c.textSecondary : c.textPrimary,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              Icon(Icons.chevron_right, color: c.textSecondary),
            ],
          ),
        ),
      ),
    );

    return ListView(
      padding: const EdgeInsets.all(Space.md),
      children: [
        Text(s[K.findTrain], style: context.text.headlineMedium),
        const SizedBox(height: Space.md),
        tile(K.from, sel.from, 'from'),
        const SizedBox(height: Space.sm),
        tile(K.to, sel.to, 'to'),
        const SizedBox(height: Space.lg),
        PrimaryButton(
          label: s[K.showTrains],
          icon: Icons.search,
          onPressed: sel.ready ? () => context.push('/results') : null,
        ),
      ],
    );
  }
}

/// Pick a station from the cached list, typing to narrow it. Works with no network once the list has been loaded once (rule 11).
class StationPicker extends ConsumerStatefulWidget {
  const StationPicker({required this.which, super.key});

  /// `from` or `to`.
  final String which;

  @override
  ConsumerState<StationPicker> createState() => _StationPickerState();
}

class _StationPickerState extends ConsumerState<StationPicker> {
  final _search = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final stations = ref.watch(stationsProvider);
    final recent = ref.watch(recentProvider);
    final sel = ref.watch(selectionProvider);
    final isFrom = widget.which == 'from';
    final c = context.colors;

    Future<void> pick(String code) async {
      final controller = ref.read(selectionProvider.notifier);
      await (isFrom ? controller.setFrom(code) : controller.setTo(code));
      if (!context.mounted) return;
      context.pop();
    }

    return Scaffold(
      appBar: InnerHeader(
        title: Semantics(
          header: true,
          child: Text(
            isFrom ? s[K.from] : s[K.to],
            style: context.text.labelLarge?.copyWith(fontSize: 18),
          ),
        ),
      ),
      body: stations.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, _) => Notice(
          icon: Icons.cloud_off,
          title: s[K.connectOnce],
          action: OutlinedButton(
            onPressed: () => ref.invalidate(stationsProvider),
            child: Text(s[K.retry]),
          ),
        ),
        data: (all) {
          final q = _query.trim().toLowerCase();
          bool match(Station st) =>
              q.isEmpty ||
              [
                st.en,
                st.si,
                st.ta,
                st.code,
              ].any((t) => t.toLowerCase().contains(q));
          // From is the origin and To the destination, so the other end is left out; the rest keep the line order the API sent.
          final other = isFrom ? sel.to : sel.from;
          final shown = all
              .where((st) => st.code != other && match(st))
              .toList();
          // Recent stations stay listed while typing, below the matches (as in the design).
          final recents = [
            for (final code in recent)
              ...all.where((st) => st.code == code && st.code != other),
          ];
          // The API lists the line starting at Colombo Fort, the end the trains run to.
          final lastStop = all.isEmpty ? null : all.first.code;
          return Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(
                  Space.md,
                  Space.md,
                  Space.md,
                  Space.sm,
                ),
                child: TextField(
                  controller: _search,
                  textInputAction: TextInputAction.search,
                  decoration: InputDecoration(
                    filled: true,
                    fillColor: c.bgPage,
                    prefixIcon: Icon(Icons.search, color: c.brandAccent),
                    suffixIcon: _query.isEmpty
                        ? null
                        : IconButton(
                            tooltip: s[K.clearSearch],
                            icon: const Icon(Icons.close),
                            onPressed: () {
                              _search.clear();
                              setState(() => _query = '');
                            },
                          ),
                    hintText: s[K.searchStations],
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(Radii.md + 4),
                      borderSide: BorderSide(color: c.borderDefault),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(Radii.md + 4),
                      borderSide: BorderSide(color: c.borderDefault),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(Radii.md + 4),
                      borderSide: BorderSide(color: c.brandAccent, width: 2),
                    ),
                  ),
                  onChanged: (v) => setState(() => _query = v),
                ),
              ),
              Expanded(
                child: ListView(
                  children: [
                    _SectionLabel(s[K.matchingStations]),
                    if (shown.isEmpty)
                      Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: Space.md,
                          vertical: Space.sm,
                        ),
                        child: Text(s[K.noStationMatch]),
                      ),
                    for (final st in shown) ...[
                      _StationRow(
                        name: st.name(lang),
                        subtitle: st.code == lastStop
                            ? '${s[K.mainLine]} · ${s[K.lastStop]}'
                            : s[K.mainLine],
                        icon: Icons.train,
                        onTap: () => pick(st.code),
                      ),
                      const _RowDivider(),
                    ],
                    if (recents.isNotEmpty) ...[
                      const SizedBox(height: Space.sm),
                      _SectionLabel(s[K.recent]),
                      for (final st in recents) ...[
                        _StationRow(
                          name: st.name(lang),
                          subtitle: st.code == sel.from
                              ? s[K.usualStart]
                              : null,
                          icon: Icons.history,
                          onTap: () => pick(st.code),
                        ),
                        const _RowDivider(),
                      ],
                    ],
                    Padding(
                      padding: const EdgeInsets.all(Space.md),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(Icons.info, size: 20, color: c.textSecondary),
                          const SizedBox(width: Space.sm),
                          Expanded(
                            child: Text(
                              s[K.stationsHint],
                              style: context.text.bodySmall?.copyWith(
                                color: c.textSecondary,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

/// A small uppercase heading over a group of stations ("MATCHING STATIONS", "RECENT").
class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(Space.md, Space.md, Space.md, Space.xs),
    child: Semantics(
      header: true,
      child: Text(
        text.toUpperCase(),
        style: context.text.bodySmall?.copyWith(
          fontWeight: FontWeight.w700,
          letterSpacing: 0.6,
          color: context.colors.textSecondary,
        ),
      ),
    ),
  );
}

class _RowDivider extends StatelessWidget {
  const _RowDivider();

  @override
  Widget build(BuildContext context) =>
      Divider(height: 1, indent: 72, color: context.colors.borderDefault);
}

class _StationRow extends StatelessWidget {
  const _StationRow({
    required this.name,
    required this.icon,
    required this.onTap,
    this.subtitle,
  });

  final String name;
  final String? subtitle;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => ListTile(
    minTileHeight: 56,
    leading: Icon(icon, color: context.colors.brandAccent),
    title: Text(
      name,
      style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700),
    ),
    subtitle: subtitle == null
        ? null
        : Text(
            subtitle!,
            style: TextStyle(fontSize: 13, color: context.colors.textSecondary),
          ),
    onTap: onTap,
  );
}

/// The trains between two stations: time at the passenger's station with the schedule beside it, a status chip and the platform
/// (AC-E06-01 1, 2). Refreshes every 30 s while open; offline it keeps the last list and says from when.
class ResultsScreen extends ConsumerWidget {
  const ResultsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final sel = ref.watch(selectionProvider);
    final list = ref.watch(stationsProvider).value;
    if (!sel.ready) {
      return Scaffold(
        appBar: InnerHeader(title: Text(s[K.trains])),
        body: Notice(title: s[K.noTrainsFound]),
      );
    }
    final polled = ref.watch(resultsProvider((sel.from!, sel.to!)));
    final network = hasNetwork(ref);
    final link = polled.link(hasNetwork: network, now: clock.now().toUtc());
    final data = polled.data;
    final heading =
        '${stationName(list, sel.from!, lang)} → ${stationName(list, sel.to!, lang)}';

    Widget body;
    if (data == null) {
      body = polled.loaded
          ? Notice(icon: Icons.cloud_off, title: s[K.noTrainsFound])
          : const Center(child: CircularProgressIndicator());
    } else if (data.today.trains.isEmpty) {
      body = Notice(
        icon: Icons.train,
        title: s[K.noMoreToday],
        body: data.firstTomorrow == null
            ? s[K.noTrainsFound]
            : '${s[K.firstTomorrow]}: ${formatTime(data.firstTomorrow!.timeAtFrom?.shown ?? clock.now())} · ${data.firstTomorrow!.destination.name(lang)}',
      );
    } else {
      body = ListView.separated(
        padding: const EdgeInsets.all(Space.md),
        itemCount: data.today.trains.length,
        separatorBuilder: (_, _) => const SizedBox(height: Space.sm),
        itemBuilder: (_, i) => _ResultCard(
          train: data.today.trains[i],
          lang: lang,
          s: s,
          from: sel.from!,
          to: sel.to!,
        ),
      );
    }

    return Scaffold(
      appBar: InnerHeader(
        title: Text(
          heading,
          style: context.text.labelLarge,
          overflow: TextOverflow.ellipsis,
        ),
      ),
      body: Column(
        children: [
          if (link == LinkStatus.unavailable && data != null)
            LinkBanner(
              text: s.fmt(
                link == LinkStatus.offline
                    ? K.offlineBanner
                    : K.unavailableBanner,
                {'time': formatTime(data.today.serverTime)},
              ),
            ),
          Expanded(child: body),
        ],
      ),
    );
  }
}

class _ResultCard extends StatelessWidget {
  const _ResultCard({
    required this.train,
    required this.lang,
    required this.s,
    required this.from,
    required this.to,
  });

  final TrainResult train;
  final Lang lang;
  final Strings s;
  final String from;
  final String to;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final time = train.timeAtFrom;
    final kind = kindOfResult(train);
    final delayed =
        time != null &&
        time.estimated != null &&
        time.estimated != time.scheduled;
    return InkWell(
      borderRadius: BorderRadius.circular(Radii.lg),
      onTap: () => context.push('/train/${train.number}?from=$from&to=$to'),
      child: SectionCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              crossAxisAlignment: WrapCrossAlignment.end,
              spacing: Space.sm,
              children: [
                Text(
                  time == null ? '–' : formatTime(time.shown),
                  style: const TextStyle(
                    fontSize: 28,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                if (delayed)
                  Text(
                    '${s[K.sched]} ${formatTime(time.scheduled)}',
                    style: context.text.bodySmall,
                  ),
              ],
            ),
            const SizedBox(height: 2),
            Text(train.destination.name(lang), style: context.text.labelLarge),
            Text(
              '${s.fmt(K.trainN, {'n': train.number})} · ${train.platform == null ? '–' : s.fmt(K.platform, {'n': train.platform!})}',
              style: context.text.bodySmall?.copyWith(color: c.textSecondary),
            ),
            const SizedBox(height: Space.sm),
            StatusChip(
              kind: kind,
              label: statusText(s, kind, formatMinutes(train.delayMinutes)),
            ),
          ],
        ),
      ),
    );
  }
}
