import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/l10n/strings.dart';
import '../core/theme/app_theme.dart';
import '../core/time.dart';
import '../core/widgets/widgets.dart';
import 'polling.dart';
import 'providers.dart';
import 'router.dart';

class PassengerApp extends ConsumerWidget {
  const PassengerApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final lang = ref.watch(languageProvider) ?? Lang.en;
    return MaterialApp.router(
      title: 'SmartRail',
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(lang),
      routerConfig: ref.watch(routerProvider),
      builder: (context, child) => _OfflineFrame(child: child!),
    );
  }
}

/// The offline strip on every screen (not only the train details): shown as soon as the phone has no connection, with the time of the
/// last good answer. It covers the status bar, so the screens below start without it.
class _OfflineFrame extends ConsumerWidget {
  const _OfflineFrame({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final lastSync = ref.watch(lastSyncProvider);
    final offline = !hasNetwork(ref) && lastSync != null;
    final top = MediaQuery.paddingOf(context).top;
    InnerHeader.statusInset = offline ? 0 : top;
    return Column(
      children: [
        if (offline)
          Padding(
            padding: EdgeInsets.zero,
            child: Container(
              color: context.colors.textPrimary,
              padding: EdgeInsets.only(top: top),
              child: LinkBanner(
                text: s.fmt(K.offlineBanner, {'time': formatTime(lastSync)}),
              ),
            ),
          ),
        Expanded(
          child: MediaQuery.removePadding(
            context: context,
            removeTop: offline,
            child: child,
          ),
        ),
      ],
    );
  }
}
