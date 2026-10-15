import 'dart:math';

/// How long to wait after a failed attempt: 5 s, doubling to a 5 minute cap, with random jitter (AC-E03-05 5).
class Backoff {
  const Backoff({
    this.first = const Duration(seconds: 5),
    this.cap = const Duration(minutes: 5),
    this.jitter = 0.2,
  });

  final Duration first;
  final Duration cap;

  /// 0.2 means the wait varies by up to 20 % either way, so phones that went offline together do not all retry together.
  final double jitter;

  /// [attempts] is how many tries have failed so far (1 after the first failure).
  /// [random] is a number from 0 to 1; pass a fixed one in tests.
  Duration after(int attempts, {double? random}) {
    final steps = max(attempts, 1) - 1;
    final unjittered = min(
      first.inMilliseconds * pow(2, min(steps, 20)),
      cap.inMilliseconds.toDouble(),
    );
    final factor = 1 + jitter * (2 * (random ?? Random().nextDouble()) - 1);
    return Duration(
      milliseconds: min((unjittered * factor).round(), cap.inMilliseconds),
    );
  }
}
