import 'package:flutter/foundation.dart' show kDebugMode;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/l10n/strings.dart';
import '../features/home/home_screens.dart';
import '../features/food/food_menu_screen.dart';
import '../features/gallery/design_gallery.dart';
import '../features/live/live_screen.dart';
import '../features/live/override_screens.dart';
import '../features/onboarding/onboarding_screens.dart';
import '../features/search/search_screens.dart';
import 'nav_bar.dart';
import 'providers.dart';

/// The five tabs of the design, Home in the middle: Trains, My tickets, Home, Alerts, Account.
const _tabs = ['/trains', '/tickets', '/home', '/alerts', '/account'];

class AppShell extends ConsumerWidget {
  const AppShell({required this.location, required this.child, super.key});

  final String location;
  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final index = _tabs
        .indexWhere(location.startsWith)
        .clamp(0, _tabs.length - 1);
    return Scaffold(
      // The station picker brings its own header, which already starts below the status bar.
      body: SafeArea(
        top: !location.startsWith('/pick'),
        bottom: false,
        child: child,
      ),
      bottomNavigationBar: DesignNavBar(
        selectedIndex: index,
        onSelected: (i) => context.go(_tabs[i]),
        tabs: [
          NavTab(
            icon: Icons.train_outlined,
            selectedIcon: Icons.train,
            label: s[K.tabTrains],
          ),
          NavTab(
            icon: Icons.confirmation_number_outlined,
            selectedIcon: Icons.confirmation_number,
            label: s[K.tabTickets],
          ),
          NavTab(
            icon: Icons.home_outlined,
            selectedIcon: Icons.home,
            label: s[K.tabHome],
          ),
          NavTab(
            icon: Icons.notifications_none,
            selectedIcon: Icons.notifications,
            label: s[K.tabAlerts],
          ),
          NavTab(
            icon: Icons.person_outline,
            selectedIcon: Icons.person,
            label: s[K.tabAccount],
          ),
        ],
      ),
    );
  }
}

/// The routes. It does not watch the language, so changing it rebuilds the screens in place, never the navigation.
final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (_, _) => const SplashScreen()),
      GoRoute(
        path: '/language',
        builder: (_, state) => LanguageScreen(
          fromSettings: state.uri.queryParameters['settings'] == '1',
        ),
      ),
      GoRoute(path: '/onboard', builder: (_, _) => const OnboardingScreen()),
      // The fixture screens for states with no backend yet: debug builds only.
      if (kDebugMode) ...[
        GoRoute(path: '/gallery', builder: (_, _) => const DesignGallery()),
        GoRoute(
          path: '/gallery/:kind',
          builder: (_, state) => OverrideScreen(
            fixture: OverrideFixture.of(
              OverrideKind.values.firstWhere(
                (k) => k.name == state.pathParameters['kind'],
                orElse: () => OverrideKind.suspended,
              ),
            ),
          ),
        ),
      ],
      ShellRoute(
        builder: (_, state, child) =>
            AppShell(location: state.uri.path, child: child),
        routes: [
          GoRoute(path: '/home', builder: (_, _) => const HomeTab()),
          GoRoute(path: '/trains', builder: (_, _) => const TrainsTab()),
          GoRoute(path: '/tickets', builder: (_, _) => const TicketsTab()),
          GoRoute(path: '/alerts', builder: (_, _) => const AlertsTab()),
          GoRoute(path: '/account', builder: (_, _) => const AccountTab()),
          // Inside the shell, so the bottom bar stays (Trains selected), as in the design.
          GoRoute(
            path: '/pick/:which',
            builder: (_, state) =>
                StationPicker(which: state.pathParameters['which']!),
          ),
        ],
      ),
      GoRoute(path: '/results', builder: (_, _) => const ResultsScreen()),
      GoRoute(
        path: '/menu/:number',
        builder: (_, state) => FoodMenuScreen(
          number: state.pathParameters['number']!,
          from: _nonEmpty(state.uri.queryParameters['from']),
          to: _nonEmpty(state.uri.queryParameters['to']),
        ),
      ),
      GoRoute(
        path: '/train/:number',
        builder: (_, state) => LiveScreen(
          number: state.pathParameters['number']!,
          from: _nonEmpty(state.uri.queryParameters['from']),
          to: _nonEmpty(state.uri.queryParameters['to']),
        ),
      ),
    ],
  );
});

String? _nonEmpty(String? v) => (v == null || v.isEmpty) ? null : v;
