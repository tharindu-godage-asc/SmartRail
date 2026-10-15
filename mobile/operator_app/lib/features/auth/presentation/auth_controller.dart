import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/providers.dart';
import '../domain/auth.dart';

/// Where sign-in stands. The router and the sign-in screens follow this and nothing else.
class AuthController extends AsyncNotifier<AuthState> {
  @override
  Future<AuthState> build() => ref.read(authRepositoryProvider).restore();

  Future<void> signIn() async {
    final repository = ref.read(authRepositoryProvider);
    state = AsyncData(await repository.signIn());
  }

  /// A request or the report queue found the session over (refresh token refused). Waiting reports are not touched.
  void markSessionExpired() {
    final current = state.value;
    if (current == null ||
        current.status == AuthStatus.sessionExpired ||
        !current.isSignedIn) {
      return;
    }
    state = AsyncData(
      AuthState(AuthStatus.sessionExpired, displayName: current.displayName),
    );
  }

  Future<void> signOut() async {
    await ref.read(authRepositoryProvider).signOut();
    // A shared phone must not show the last operator's journey to the next one. Waiting reports stay on purpose.
    await ref.read(localCacheProvider).clearJourney();
    state = const AsyncData(AuthState.signedOut());
  }
}

final authControllerProvider = AsyncNotifierProvider<AuthController, AuthState>(
  AuthController.new,
);
