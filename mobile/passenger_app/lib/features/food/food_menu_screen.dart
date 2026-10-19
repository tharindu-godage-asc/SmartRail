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
import '../../data/menu.dart';
import '../../domain/logic.dart';

/// The row on the live screen that opens the food menu. It is shown only when the trip has a menu (AC-E08-01 7), so a train with no food
/// shows nothing and a failed or empty read never gets in the way of the live status (NFR-07).
class FoodMenuRow extends ConsumerWidget {
  const FoodMenuRow({required this.number, this.from, this.to, super.key});

  final String number;
  final String? from;
  final String? to;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final menu = ref.watch(menuProvider((number, from, to))).data;
    if (menu == null || menu.isEmpty) return const SizedBox.shrink();
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final c = context.colors;
    final first = menu.stations.first;
    return Padding(
      padding: const EdgeInsets.only(bottom: Space.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(Radii.lg),
        onTap: () =>
            context.push('/menu/$number?from=${from ?? ''}&to=${to ?? ''}'),
        child: SectionCard(
          child: Row(
            children: [
              Icon(Icons.restaurant, color: c.textPrimary),
              const SizedBox(width: Space.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(s[K.foodMenu], style: context.text.labelLarge),
                    Text(
                      s.fmt(K.foodMenuAt, {
                        'station': localName(first.names, lang.code),
                      }),
                      style: context.text.bodyMedium?.copyWith(
                        color: c.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }
}

/// The read-only menu of the stations of a trip (FR-90, FR-91): price, Available or Sold out, when it is from, and one button to the
/// caterer's own website. There is no basket, no quantity and no order here (D-71).
class FoodMenuScreen extends ConsumerWidget {
  const FoodMenuScreen({required this.number, this.from, this.to, super.key});

  final String number;
  final String? from;
  final String? to;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final polled = ref.watch(menuProvider((number, from, to)));
    final menu = polled.data;
    final online = hasNetwork(ref);
    final c = context.colors;
    return Scaffold(
      appBar: InnerHeader(
        title: Text(s[K.foodMenu], style: context.text.labelLarge),
      ),
      body: menu == null || menu.isEmpty
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(Space.md),
              children: [
                for (final station in menu.stations) ...[
                  _StationSection(
                    station: station,
                    lang: lang,
                    s: s,
                    // Old when the caterer has been silent for too long, or this phone is offline (EC-180, rule 7).
                    old:
                        station.stale ||
                        polled.link(
                              hasNetwork: online,
                              now: clock.now().toUtc(),
                            ) !=
                            LinkStatus.live,
                  ),
                  const SizedBox(height: Space.md),
                ],
                Text(
                  s[K.orderNote],
                  style: context.text.bodyMedium?.copyWith(
                    color: c.textSecondary,
                  ),
                ),
                const SizedBox(height: Space.md),
                if (menu.orderUrl != null)
                  Semantics(
                    link: true,
                    hint: s[K.orderOnWebsite],
                    child: PrimaryButton(
                      label: s[K.orderOnWebsite],
                      icon: Icons.open_in_new,
                      onPressed: () => _open(context, ref, s, online, menu),
                    ),
                  ),
              ],
            ),
    );
  }

  Future<void> _open(
    BuildContext context,
    WidgetRef ref,
    Strings s,
    bool online,
    TrainMenu menu,
  ) async {
    final messenger = ScaffoldMessenger.of(context);
    if (!online) {
      // EC-181: nothing opens, and the list stays.
      messenger.showSnackBar(SnackBar(content: Text(s[K.orderOffline])));
      return;
    }
    final opened = await ref.read(urlOpenerProvider)(Uri.parse(menu.orderUrl!));
    if (!opened) {
      messenger.showSnackBar(SnackBar(content: Text(s[K.orderFailed])));
    }
  }
}

class _StationSection extends StatelessWidget {
  const _StationSection({
    required this.station,
    required this.lang,
    required this.s,
    required this.old,
  });

  final MenuStation station;
  final Lang lang;
  final Strings s;
  final bool old;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            s.fmt(K.foodMenuAt, {
              'station': localName(station.names, lang.code),
            }),
            style: context.text.titleMedium,
          ),
          if (station.isLongStop)
            Text(
              s[K.longStop],
              style: context.text.bodyMedium?.copyWith(color: c.textSecondary),
            ),
          const SizedBox(height: Space.xs),
          Text(
            // "Menu from 07:12. It may have changed." (EC-180)
            old
                ? '${s.fmt(K.menuFrom, {'time': formatTime(station.updatedAt)})}. ${s[K.menuMayHaveChanged]}'
                : s.fmt(K.menuFrom, {'time': formatTime(station.updatedAt)}),
            style: context.text.bodyMedium?.copyWith(color: c.textSecondary),
          ),
          const SizedBox(height: Space.sm),
          for (final item in station.items)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: Space.xs),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          localName(item.names, lang.code),
                          style: context.text.labelLarge,
                        ),
                        Text(
                          s.fmt(K.price, {
                            'price': item.priceLkr.toStringAsFixed(2),
                          }),
                          style: context.text.bodyMedium?.copyWith(
                            color: c.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  // Text and an icon, never colour alone (NFR-30).
                  Icon(
                    item.available
                        ? Icons.check_circle_outline
                        : Icons.remove_circle_outline,
                    size: 20,
                  ),
                  const SizedBox(width: Space.xs),
                  Text(
                    item.available ? s[K.available] : s[K.soldOut],
                    style: context.text.labelLarge,
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
