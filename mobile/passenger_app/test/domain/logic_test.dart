import 'package:flutter_test/flutter_test.dart';
import 'package:passenger_app/core/time.dart';
import 'package:passenger_app/data/models.dart';
import 'package:passenger_app/data/preferences.dart';
import 'package:passenger_app/domain/logic.dart';

import '../support/fixtures.dart';

void main() {
  group('primary ETA (D-38, FR-66, AC-E06-02 1, 2)', () {
    test('before the train leaves the boarding station it is the arrival there, with the delay', () {
      final eta = primaryEta(
        status(delay: 8, reported: 1, departed: true),
        fromCode: 'GPH',
        toCode: 'CF',
      )!;

      expect(eta.stop.code, 'GPH');
      expect(eta.atDestination, isFalse);
      expect(formatTime(eta.time!), '08:38'); // 08:30 + 8
    });

    test('once the train departs the boarding station it switches to the destination', () {
      final eta = primaryEta(
        status(delay: 8, reported: 2, departed: true),
        fromCode: 'GPH',
        toCode: 'CF',
      )!;

      expect(eta.stop.code, 'CF');
      expect(eta.atDestination, isTrue);
      expect(formatTime(eta.time!), '09:03'); // 08:55 + 8
    });

    test('a later stop being reported means the boarding station was passed, even with no report for it', () {
      final json = statusJson(delay: 5, reported: 3, departed: true);
      (json['stops'] as List)[1]['confirmation'] = 'Inferred';
      (json['stops'] as List)[1]['actualDeparture'] = null;

      final eta = primaryEta(
        TrainStatus.fromJson(json),
        fromCode: 'GPH',
        toCode: 'CF',
      )!;

      expect(eta.stop.code, 'CF');
    });

    test('with no station chosen it is the first stop to the last', () {
      final eta = primaryEta(status())!;

      expect(eta.stop.code, 'VYG');
      expect(
        formatTime(eta.time!),
        '08:20',
      ); // the first stop has no arrival: its departure is the time
    });

    test('with the delay length unknown there is no ETA at all', () {
      final eta = primaryEta(
        status(delay: 8, reported: 1, departed: true, known: false),
        fromCode: 'GPH',
        toCode: 'CF',
      )!;

      expect(eta.time, isNull);
    });

    test('a finished journey has none', () {
      expect(primaryEta(status(reported: 5, completed: true)), isNull);
    });
  });

  group('statuses are one of a fixed list (NFR-30)', () {
    test('on time, delayed, scheduled, waiting for update and arrived', () {
      expect(
        kindOfStatus(status(reported: 1, key: 'OnTime')),
        StatusKind.onTime,
      );
      expect(kindOfStatus(status(delay: 8, reported: 1)), StatusKind.delayed);
      expect(kindOfStatus(status(key: 'Scheduled')), StatusKind.scheduled);
      expect(
        kindOfStatus(status(delay: 8, reported: 1, known: false)),
        StatusKind.waiting,
      );
      expect(
        kindOfStatus(status(reported: 5, completed: true)),
        StatusKind.arrived,
      );
    });

    test('a train from the timetable only is "scheduled", never on time', () {
      final timetable = TrainResult.fromJson(
        resultJson(live: false, key: 'Scheduled'),
      );

      expect(kindOfResult(timetable), StatusKind.scheduled);
    });
  });

  group('two kinds of silence (rule 7, NFR-03)', () {
    final t0 = DateTime.utc(2026, 10, 16, 3);

    test('a healthy poll is live with a network and offline without one', () {
      final p = const Polled<int>().succeeded(1, t0);

      expect(p.link(hasNetwork: true, now: t0), LinkStatus.live);
      expect(
        p.link(hasNetwork: false, now: t0),
        LinkStatus.offline,
      ); // said at once
    });

    test('failures under 30 seconds are still live; at 30 seconds the server is unavailable', () {
      final p = const Polled<int>().succeeded(1, t0).failed(t0);

      expect(
        p.link(hasNetwork: true, now: t0.add(const Duration(seconds: 29))),
        LinkStatus.live,
      );
      expect(
        p.link(hasNetwork: true, now: t0.add(const Duration(seconds: 30))),
        LinkStatus.unavailable,
      );
    });

    test('the last data is kept through failures and the first good answer restores live', () {
      var p = const Polled<int>()
          .succeeded(7, t0)
          .failed(t0)
          .failed(t0.add(const Duration(seconds: 10)));
      expect(p.data, 7);
      expect(p.failingSince, t0); // the first failure, not the latest

      p = p.succeeded(8, t0.add(const Duration(seconds: 40)));
      expect(
        p.link(hasNetwork: true, now: t0.add(const Duration(seconds: 40))),
        LinkStatus.live,
      );
    });
  });

  test('a follow expires after 24 hours or when the journey is completed (rule 10)', () {
    final f = Follow('8058', 'GPH', 'CF', DateTime.utc(2026, 10, 16, 3));

    expect(
      followExpired(
        f,
        now: f.followedAt.add(const Duration(hours: 23, minutes: 59)),
      ),
      isFalse,
    );
    expect(
      followExpired(f, now: f.followedAt.add(const Duration(hours: 24))),
      isTrue,
    );
    expect(followExpired(f, now: f.followedAt, completed: true), isTrue);
  });

  group('what a screen reader hears (NFR-32, AC-E06-05 5)', () {
    Announcement? say(
      StatusKind? before,
      StatusKind after, [
      LinkStatus? beforeLink,
      LinkStatus afterLink = LinkStatus.live,
    ]) => announcementFor(
      beforeKind: before,
      afterKind: after,
      beforeLink: beforeLink,
      afterLink: afterLink,
    );

    test('a change of meaning is announced', () {
      expect(say(StatusKind.onTime, StatusKind.delayed), Announcement.delayed);
      expect(say(StatusKind.delayed, StatusKind.waiting), Announcement.waiting);
      expect(say(StatusKind.delayed, StatusKind.arrived), Announcement.arrived);
      expect(
        say(
          StatusKind.delayed,
          StatusKind.delayed,
          LinkStatus.live,
          LinkStatus.offline,
        ),
        Announcement.offline,
      );
      expect(
        say(
          StatusKind.delayed,
          StatusKind.delayed,
          LinkStatus.offline,
          LinkStatus.live,
        ),
        Announcement.back,
      );
    });

    test(
      'an ETA moving from 08:40 to 08:41 changes no status, so nothing is said',
      () {
        expect(
          say(
            StatusKind.delayed,
            StatusKind.delayed,
            LinkStatus.live,
            LinkStatus.live,
          ),
          isNull,
        );
      },
    );

    test('the first answer says nothing', () {
      expect(say(null, StatusKind.delayed), isNull);
    });
  });

  test('delays read in hours and minutes, never 240 min', () {
    expect(formatMinutes(8), '8 min');
    expect(formatMinutes(60), '1 h');
    expect(formatMinutes(65), '1 h 05 min');
    expect(formatMinutes(240), '4 h');
  });

  test('times show in Sri Lanka time whatever the phone zone is', () {
    expect(formatTime(DateTime.utc(2026, 10, 16, 3)), '08:30');
    expect(
      formatTime(DateTime.utc(2026, 10, 16, 20)),
      '01:30',
    ); // after midnight local
  });
}
