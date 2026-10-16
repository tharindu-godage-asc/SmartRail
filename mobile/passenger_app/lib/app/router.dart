import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/l10n/strings.dart';
import '../features/home/home_screens.dart';
import '../features/live/live_screen.dart';
import '../features/onboarding/onboarding_screens.dart';
import '../features/search/search_screens.dart';
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
      body: SafeArea(bottom: false, child: child),
      bottomNavigationBar: NavigationBar(
        selectedIndex: index,
        onDestinationSelected: (i) => context.go(_tabs[i]),
        destinations: [
          NavigationDestination(
            icon: const Icon(Icons.train_outlined),
            selectedIcon: const Icon(Icons.train),
            label: s[K.tabTrains],
          ),
          NavigationDestination(
            icon: const Icon(Icons.confirmation_number_outlined),
            selectedIcon: const Icon(Icons.confirmation_number),
            label: s[K.tabTickets],
          ),
          NavigationDestination(
            icon: const Icon(Icons.home_outlined),
            selectedIcon: const Icon(Icons.home),
            label: s[K.tabHome],
          ),
          NavigationDestination(
            icon: const Icon(Icons.notifications_none),
            selectedIcon: const Icon(Icons.notifications),
            label: s[K.tabAlerts],
          ),
          NavigationDestination(
            icon: const Icon(Icons.person_outline),
            selectedIcon: const Icon(Icons.person),
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
      ShellRoute(
        builder: (_, state, child) =>
            AppShell(location: state.uri.path, child: child),
        routes: [
          GoRoute(path: '/home', builder: (_, _) => const HomeTab()),
          GoRoute(path: '/trains', builder: (_, _) => const TrainsTab()),
          GoRoute(path: '/tickets', builder: (_, _) => const TicketsTab()),
          GoRoute(path: '/alerts', builder: (_, _) => const AlertsTab()),
          GoRoute(path: '/account', builder: (_, _) => const AccountTab()),
        ],
      ),
      GoRoute(
        path: '/pick/:which',
        builder: (_, state) =>
            StationPicker(which: state.pathParameters['which']!),
      ),
      GoRoute(path: '/results', builder: (_, _) => const ResultsScreen()),
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
