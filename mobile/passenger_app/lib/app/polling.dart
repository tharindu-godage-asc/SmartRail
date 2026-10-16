import 'dart:async';

import 'package:clock/clock.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/logic.dart';

/// When any screen last got a good answer from the server: the time the offline banner shows on every screen (spec 10 rule 7).
final lastSyncProvider = NotifierProvider<LastSync, DateTime?>(LastSync.new);

class LastSync extends Notifier<DateTime?> {
  @override
  DateTime? build() => null;

  void set(DateTime value) => state = value;
}

/// A screen's data that refreshes on its own while it is open (spec 10 rule 8): one request at a time, only while the app is in the
/// foreground, one extra refresh when the app comes back, and the last good answer kept through any failure. Not faster than [interval].
abstract class PollingNotifier<T> extends Notifier<Polled<T>>
    with WidgetsBindingObserver {
  Duration get interval;

  Future<T> fetch();

  /// A cached answer to show before the first fetch finishes, if there is one.
  Polled<T> initial() => Polled<T>();

  /// The server's own time on an answer, which the offline banner shows ("info from ..."); null means the phone's clock.
  DateTime? serverTimeOf(T value) => null;

  /// Called after each good answer (the screens use it to save a snapshot).
  void onSuccess(T value) {}

  Timer? _timer;
  bool _busy = false;
  bool _disposed = false;

  @override
  Polled<T> build() {
    _timer = Timer.periodic(interval, (_) => refresh());
    WidgetsBinding.instance.addObserver(this);
    ref.onDispose(() {
      _disposed = true;
      _timer?.cancel();
      WidgetsBinding.instance.removeObserver(this);
    });
    scheduleMicrotask(refresh);
    return initial();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) refresh();
  }

  bool get _foreground {
    final s = WidgetsBinding.instance.lifecycleState;
    // Only a paused or hidden app is in the background; the state is null or detached before the first frame and in tests.
    return s != AppLifecycleState.paused && s != AppLifecycleState.hidden;
  }

  Future<void> refresh() async {
    if (_busy || _disposed || !_foreground) return;
    _busy = true;
    try {
      final value = await fetch();
      if (_disposed) return;
      state = state.succeeded(value, clock.now());
      ref
          .read(lastSyncProvider.notifier)
          .set(serverTimeOf(value) ?? clock.now());
      onSuccess(value);
    } catch (_) {
      if (!_disposed) state = state.failed(clock.now());
    } finally {
      _busy = false;
    }
  }
}
