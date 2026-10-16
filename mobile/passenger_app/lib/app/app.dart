import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/l10n/strings.dart';
import '../core/theme/app_theme.dart';
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
    );
  }
}
