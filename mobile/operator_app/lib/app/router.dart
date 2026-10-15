import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/widgets/widgets.dart';
import '../features/auth/presentation/auth_controller.dart';
import '../features/auth/presentation/sign_in_screen.dart';
import '../features/journey/presentation/delay_screens.dart';
import '../features/journey/presentation/help_screen.dart';
import '../features/journey/presentation/journey_screen.dart';

/// Tells the router to look again when the sign-in state changes.
class _AuthRefresh extends ChangeNotifier {
  _AuthRefresh(Ref ref) {
    ref.listen(authControllerProvider, (_, _) => notifyListeners());
  }
}

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = _AuthRefresh(ref);
  ref.onDispose(refresh.dispose);

  return GoRouter(
    refreshListenable: refresh,
    initialLocation: '/',
    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      // Still reading the saved session: stay where we are, the splash is shown by the app.
      if (auth.isLoading && !auth.hasValue) {
        return null;
      }
      final signedIn = auth.value?.isSignedIn ?? false;
      final onSignIn = state.matchedLocation == '/sign-in';
      if (!signedIn) {
        return onSignIn ? null : '/sign-in';
      }
      return onSignIn ? '/' : null;
    },
    routes: [
      GoRoute(
        path: '/sign-in',
        builder: (context, state) => const SignInScreen(),
      ),
      GoRoute(path: '/', builder: (context, state) => const _Shell()),
      GoRoute(
        path: '/delay/reason',
        builder: (context, state) => const DelayReasonScreen(),
      ),
      GoRoute(
        path: '/delay/estimate',
        builder: (context, state) => const DelayEstimateScreen(),
      ),
    ],
  );
});

/// The signed-in frame: header, the current tab, and the bottom bar.
class _Shell extends ConsumerStatefulWidget {
  const _Shell();

  @override
  ConsumerState<_Shell> createState() => _ShellState();
}

class _ShellState extends ConsumerState<_Shell> {
  int _tab = 0;

  @override
  Widget build(BuildContext context) {
    final name = ref.watch(authControllerProvider).value?.displayName;
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            AppHeader(name: name),
            Expanded(
              child: IndexedStack(
                index: _tab,
                children: const [JourneyScreen(), HelpScreen()],
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        top: false,
        child: AppBottomBar(
          index: _tab,
          onSelected: (i) => setState(() => _tab = i),
        ),
      ),
    );
  }
}
