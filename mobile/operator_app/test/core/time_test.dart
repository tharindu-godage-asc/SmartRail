import 'package:clock/clock.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/core/time.dart';

void main() {
  test('times are shown in Colombo time, not the phone zone (spec rule 7)', () {
    final instant = DateTime.utc(2026, 10, 6, 2, 58); // 08:28 in Colombo
    expect(formatTime(instant), '08:28');
    expect(formatDay(instant), 'Tue 6 Oct');
  });

  test('a time after midnight in Colombo is on the next day', () {
    expect(formatTime(DateTime.utc(2026, 10, 6, 18, 45)), '00:15');
    expect(formatDay(DateTime.utc(2026, 10, 6, 18, 45)), 'Wed 7 Oct');
  });

  group('ClockOffset (FR-21, spec rule 1)', () {
    test('a phone clock 3 minutes fast is corrected to server time', () {
      final server = DateTime.utc(2026, 10, 6, 3);
      final phone = server.add(const Duration(minutes: 3));
      final offset = ClockOffset.measure(serverTime: server, receivedAt: phone);

      expect(offset.value, const Duration(minutes: -3));
      withClock(Clock.fixed(phone.add(const Duration(minutes: 10))), () {
        expect(offset.now(), server.add(const Duration(minutes: 10)));
      });
    });

    test('with no measurement the raw phone time is used', () {
      final phone = DateTime.utc(2026, 10, 6, 3);
      withClock(Clock.fixed(phone), () {
        expect(ClockOffset.none.now(), phone);
      });
    });
  });
}
