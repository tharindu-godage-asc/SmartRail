import 'package:intl/intl.dart';

/// Sri Lanka has no daylight saving, so a fixed offset is the zone (assumption A-TZ). All times on screen use it,
/// not the phone's zone (spec rule 7).
const colomboOffset = Duration(hours: 5, minutes: 30);

DateTime inColombo(DateTime instant) => instant.toUtc().add(colomboOffset);

String formatTime(DateTime instant) =>
    DateFormat('HH:mm').format(inColombo(instant));

String formatDay(DateTime instant) =>
    DateFormat('EEE d MMM').format(inColombo(instant));

/// A delay as people say it: "45 min", "4 h", "1 h 05 min" (never "240 min").
String formatMinutes(int total) {
  final minutes = total < 0 ? 0 : total;
  if (minutes < 60) return '$minutes min';
  final h = minutes ~/ 60;
  final r = minutes % 60;
  return r == 0 ? '$h h' : '$h h ${r.toString().padLeft(2, '0')} min';
}
