import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/strings.dart';
import '../core/theme/app_theme.dart';
import '../features/auth/presentation/auth_controller.dart';
import '../features/journey/presentation/journey_controller.dart';
import '../features/reports/presentation/report_dispatcher.dart';
import 'router.dart';

class OperatorApp extends ConsumerStatefulWidget {
  const OperatorApp({super.key});

  @override
  ConsumerState<OperatorApp> createState() => _OperatorAppState();
}

class _OperatorAppState extends ConsumerState<OperatorApp>
    with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  /// Coming back to the app is the moment to refresh and to send anything waiting.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      ref.read(journeyControllerProvider.notifier).refresh();
      ref.read(reportDispatcherProvider.notifier).kick();
    }
  }

  @override
  Widget build(BuildContext context) {
    // Kept alive for the whole run: it sends reports whenever there is something to send.
    ref.watch(reportDispatcherProvider);
    final auth = ref.watch(authControllerProvider);

    return MaterialApp.router(
      title: '${Strings.appName} ${Strings.roleOperator}',
      theme: buildAppTheme(),
      routerConfig: ref.watch(routerProvider),
      debugShowCheckedModeBanner: false,
      builder: (context, child) {
        // Reading the saved session takes a moment; do not flash the sign-in screen at a signed-in operator.
        if (auth.isLoading && !auth.hasValue) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }
        return child ?? const SizedBox.shrink();
      },
    );
  }
}
