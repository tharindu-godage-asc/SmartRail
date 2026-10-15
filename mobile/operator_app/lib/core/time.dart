import 'package:clock/clock.dart';
import 'package:intl/intl.dart';

/// Sri Lanka has no daylight saving, so a fixed offset is the zone (assumption A-TZ). All times on screen use it,
/// not the phone's zone (spec rule 7).
const colomboOffset = Duration(hours: 5, minutes: 30);

DateTime inColombo(DateTime instant) => instant.toUtc().add(colomboOffset);

String formatTime(DateTime instant) =>
    DateFormat('HH:mm').format(inColombo(instant));

String formatDay(DateTime instant) =>
    DateFormat('EEE d MMM').format(inColombo(instant));

/// The difference between the server's clock and the phone's, measured at the last successful read (spec rule 1).
/// It is kept on the device so a report taken offline is still stamped with corrected time.
class ClockOffset {
  const ClockOffset(this.value);

  static const none = ClockOffset(Duration.zero);

  final Duration value;

  /// [serverTime] is what the server said it was, [receivedAt] what the phone's clock said at that moment.
  factory ClockOffset.measure({
    required DateTime serverTime,
    required DateTime receivedAt,
  }) => ClockOffset(serverTime.difference(receivedAt));

  /// The corrected "now". With no measurement it is the raw phone time.
  DateTime now() => clock.now().toUtc().add(value);
}
