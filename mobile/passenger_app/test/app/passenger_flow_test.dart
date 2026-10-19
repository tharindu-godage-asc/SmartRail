import 'package:clock/clock.dart';
import 'package:flutter_test/flutter_test.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

/// A clock the test moves together with the widget timers, so "30 seconds of failures" can be tested.
class TestClock {
  DateTime now = DateTime.utc(2026, 10, 16, 3, 12);

  Future<void> advance(WidgetTester tester, Duration d) async {
    now = now.add(d);
    await tester.pump(d);
    await tester.pumpAndSettle(const Duration(milliseconds: 50));
  }
}

Future<void> tapText(WidgetTester tester, String text) async {
  await tester.tap(find.text(text).first);
  await tester.pumpAndSettle();
}

void main() {
  group('first run (AC-E06-04 5, EC-70)', () {
    testWidgets(
      'asks for a language before anything else, saves it, and shows the intro in it',
      (tester) async {
        final h = await Harness.create();
        await h.pump(tester);

        expect(find.text('Choose your language'), findsOneWidget);
        expect(find.text('English'), findsOneWidget);
        expect(find.text('සිංහල'), findsOneWidget);
        expect(find.text('தமிழ்'), findsOneWidget);

        await tapText(tester, 'සිංහල');

        expect(h.prefs.language?.code, 'si');
        expect(
          find.text('ඔබේ දුම්රිය පැමිණෙන වේලාව දැනගන්න'),
          findsOneWidget,
        ); // the first intro page, in Sinhala
      },
    );

    testWidgets('three intro pages, then Home; the intro is not shown again', (
      tester,
    ) async {
      final h = await Harness.create(stored: {'language': 'en'});
      await h.pump(tester);

      expect(find.text('Know when your train is coming'), findsOneWidget);
      await tapText(tester, 'Next');
      expect(find.text('Honest when we are unsure'), findsOneWidget);
      expect(
        find.text('On time'),
        findsOneWidget,
      ); // the status legend: words, with icons
      await tapText(tester, 'Next');
      expect(find.text('Follow your train'), findsOneWidget);
      expect(
        find.textContaining('notif'),
        findsNothing,
      ); // never promises what this version cannot do
      await tapText(tester, 'Get started');

      expect(find.text('Find a train'), findsOneWidget);
      expect(h.prefs.onboarded, isTrue);
    });

    testWidgets('a phone that has been used opens on Home', (tester) async {
      final h = await Harness.create(stored: Harness.used);
      await h.pump(tester);

      expect(find.text('Find a train'), findsOneWidget);
      expect(find.text('Main Line: running normally'), findsOneWidget);
    });
  });

  group('find trains (AC-E06-01)', () {
    Future<void> search(WidgetTester tester, Harness h) async {
      await h.pump(tester);
      await tapText(tester, 'Trains');
      await tapText(tester, 'Choose where you start');
      await tapText(tester, 'Veyangoda');
      await tapText(tester, 'Choose where you are going');
      await tapText(tester, 'Colombo Fort');
      await tapText(tester, 'Show trains');
    }

    testWidgets(
      'lists 8058 with the estimate beside the schedule, the status and the platform; the origin is remembered',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.trainsToday = [resultJson(delay: 8, key: 'Delayed')];

        await search(tester, h);

        expect(find.text('08:38'), findsOneWidget); // AC-E06-01 2: 08:38 ...
        expect(
          find.text('Sched. 08:30'),
          findsOneWidget,
        ); // ... beside the scheduled 08:30
        expect(find.text('Colombo Fort'), findsWidgets);
        expect(find.text('Delayed 8 min'), findsOneWidget);
        expect(find.textContaining('Platform 1'), findsOneWidget);
        expect(h.prefs.fromCode, 'VYG'); // AC-E06-01 4, no location permission
      },
    );

    testWidgets('the origin is preselected the next time the app opens', (
      tester,
    ) async {
      final h = await Harness.create(stored: {...Harness.used, 'from': 'VYG'});
      await h.pump(tester);
      await tapText(tester, 'Trains');

      expect(find.text('Veyangoda'), findsOneWidget);
    });

    testWidgets(
      'a train with no platform shows a dash, and one from the timetable says "Scheduled"',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.trainsToday = [
          resultJson(live: false, key: 'Scheduled', platform: null),
        ];

        await search(tester, h);

        expect(find.text('Scheduled'), findsOneWidget);
        expect(find.textContaining('Train 8058 · –'), findsOneWidget);
      },
    );

    testWidgets('names the stations in the chosen language', (tester) async {
      final h = await Harness.create(
        stored: {'language': 'si', 'onboarded': true},
      );
      await h.pump(tester);
      await tapText(tester, 'දුම්රිය'); // the Trains tab, in Sinhala
      await tapText(tester, 'ඔබ ආරම්භ කරන තැන තෝරන්න');

      expect(find.text('වේයන්ගොඩ'), findsOneWidget);
      expect(find.text('කොළඹ කොටුව'), findsOneWidget);
    });

    testWidgets('nothing left today says so', (tester) async {
      final h = await Harness.create(stored: Harness.used);
      h.api.trainsToday = [];

      await search(tester, h);

      expect(find.text('No more trains today'), findsOneWidget);
    });

    testWidgets(
      'the station list comes from the cache when the server cannot be reached (rule 11)',
      (tester) async {
        final api = FakeApi();
        final h = await Harness.create(stored: Harness.used, api: api);
        await h.pump(tester);
        await tapText(
          tester,
          'Trains',
        ); // opening the pickers' screen loads and caches the stations
        await tester.pumpAndSettle();
        expect(h.prefs.stationCache, isNotNull);
        api.failing = true;
        await tapText(tester, 'Choose where you start');

        expect(find.text('Gampaha'), findsOneWidget);
      },
    );

    testWidgets(
      'first launch with no network and nothing cached asks to connect once (G4)',
      (tester) async {
        final api = FakeApi()..failing = true;
        final h = await Harness.create(stored: Harness.used, api: api);
        await h.pump(tester);

        await tapText(tester, 'Trains');

        expect(find.text('Connect once to load the stations'), findsOneWidget);
        expect(find.text('Retry'), findsOneWidget);
      },
    );
  });

  group('live status (AC-E06-02, AC-E06-03)', () {
    Future<void> open(WidgetTester tester, Harness h) async {
      await h.pump(tester);
      await tapText(tester, 'Trains');
      await tapText(tester, 'Choose where you start');
      await tapText(tester, 'Gampaha');
      await tapText(tester, 'Choose where you are going');
      await tapText(tester, 'Colombo Fort');
      await tapText(tester, 'Show trains');
      await tester.tap(find.textContaining('Train 8058').first);
      await tester.pumpAndSettle();
    }

    testWidgets(
      'shows "Arrives at Gampaha 08:38" with the delay, no reason, and the stops with progress and ETA together',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.status = statusJson(delay: 8, reported: 1, departed: true);

        await open(tester, h);

        expect(find.text('Arrives at Gampaha'), findsOneWidget);
        expect(
          find.text('08:38'),
          findsWidgets,
        ); // the ETA card and the stop list
        expect(find.text('Delayed 8 min'), findsOneWidget);
        expect(find.textContaining('About 8 min late'), findsOneWidget);
        expect(
          find.textContaining('Reason'),
          findsNothing,
        ); // D-10: nothing verifies a reason yet
        expect(
          find.text('Departed'),
          findsOneWidget,
        ); // Veyangoda, in the same list
        expect(find.text('Stops'.toUpperCase()), findsOneWidget);
        expect(find.text('Follow this train'), findsOneWidget);
        expect(
          find.text('Notify me'),
          findsNothing,
        ); // no dead-end buttons (rule 14)
      },
    );

    testWidgets(
      'after the train leaves Gampaha the ETA moves to Colombo Fort (AC-E06-02 2)',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.status = statusJson(delay: 8, reported: 1, departed: true);
        await open(tester, h);
        expect(find.text('Arrives at Gampaha'), findsOneWidget);

        h.api.status = statusJson(delay: 8, reported: 2, departed: true);
        await Harness.pass(tester, const Duration(seconds: 10));

        expect(find.text('Arrives at Colombo Fort'), findsOneWidget);
        expect(find.text('09:03'), findsWidgets);
      },
    );

    testWidgets(
      'an unknown delay shows "Waiting for update", no ETA and the last seen text (AC-E06-03 1, D-39)',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.status = statusJson(
          delay: 8,
          reported: 1,
          departed: true,
          known: false,
        );

        await open(tester, h);

        expect(find.text('Waiting for update'), findsOneWidget);
        expect(find.textContaining('No news yet'), findsOneWidget);
        expect(
          find.textContaining('Last seen: departed Veyangoda'),
          findsOneWidget,
        );
        expect(find.text('08:38'), findsNothing);
        expect(find.text('–'), findsWidgets); // the stop ETAs are dashes
      },
    );

    testWidgets(
      'a stop passed with no report shows "Passed" and no "missing" wording (AC-E06-02 5)',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        final json = statusJson(delay: 5, reported: 3, departed: true);
        (json['stops'] as List)[1]['confirmation'] = 'Inferred';
        (json['stops'] as List)[1]['actualDeparture'] = null;
        (json['stops'] as List)[1]['actualArrival'] = null;
        h.api.status = json;

        await open(tester, h);

        expect(find.text('Passed'), findsOneWidget);
        expect(
          find.textContaining('missing', findRichText: true),
          findsNothing,
        );
      },
    );

    testWidgets('a finished journey says Arrived and offers no follow', (
      tester,
    ) async {
      final h = await Harness.create(stored: Harness.used);
      h.api.status = statusJson(reported: 5, completed: true);

      await open(tester, h);

      expect(find.text('Arrived'), findsOneWidget);
      expect(find.text('This train has finished its journey.'), findsOneWidget);
      expect(find.text('Follow this train'), findsNothing);
    });

    testWidgets('no journey today says so (G2)', (tester) async {
      final h = await Harness.create(stored: Harness.used);
      h.api.status = null;

      await open(tester, h);

      expect(
        find.text('No journey found for this train today'),
        findsOneWidget,
      );
    });

    testWidgets(
      'offline: the banner says from when, the last ETA stays marked "Last known", and going online restores it',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.status = statusJson(delay: 8, reported: 1, departed: true);
        await open(tester, h);

        h.goOffline();
        h.api.failing = true;
        await tester.pumpAndSettle();

        expect(
          find.text("You're offline · Showing info from 08:42"),
          findsOneWidget,
        ); // from the snapshot's server time
        expect(find.text('Last known'), findsOneWidget);
        expect(find.text('08:38'), findsWidgets); // the data is kept
        expect(
          find.text('Arrives at Gampaha'),
          findsNothing,
        ); // replaced by "Last known" while old

        h.goOnline();
        h.api.failing = false;
        await Harness.pass(tester, const Duration(seconds: 10));

        expect(find.textContaining("You're offline"), findsNothing);
        expect(find.text('Arrives at Gampaha'), findsOneWidget);
      },
    );

    testWidgets(
      'online but the server does not answer: after 30 seconds "Live updates unavailable", same data, then it recovers',
      (tester) async {
        final t = TestClock();
        await withClock(Clock(() => t.now), () async {
          final h = await Harness.create(stored: Harness.used);
          h.api.status = statusJson(delay: 8, reported: 1, departed: true);
          await open(tester, h);

          h.api.failing = true;
          await t.advance(tester, const Duration(seconds: 10));
          expect(
            find.textContaining('Live updates unavailable'),
            findsNothing,
          ); // 10 s of failures: still live
          await t.advance(tester, const Duration(seconds: 10));
          await t.advance(tester, const Duration(seconds: 10));
          await t.advance(tester, const Duration(seconds: 10));

          expect(
            find.text('Live updates unavailable · Showing info from 08:42'),
            findsOneWidget,
          );
          expect(find.text('08:38'), findsWidgets);

          h.api.failing = false;
          await t.advance(tester, const Duration(seconds: 10));
          expect(find.textContaining('Live updates unavailable'), findsNothing);
        });
      },
    );

    testWidgets('large text does not break the layout (AC-E06-05 3)', (
      tester,
    ) async {
      final h = await Harness.create(stored: Harness.used);
      h.api.status = statusJson(delay: 8, reported: 1, departed: true);
      await h.pump(tester, textScale: 2.0);
      await tapText(tester, 'Trains');
      await tapText(tester, 'Choose where you start');
      await tapText(tester, 'Gampaha');
      await tapText(tester, 'Choose where you are going');
      await tapText(tester, 'Colombo Fort');
      await tapText(tester, 'Show trains');
      await tester.tap(find.textContaining('Train 8058').first);
      await tester.pumpAndSettle();

      expect(find.text('08:38'), findsWidgets);
      expect(tester.takeException(), isNull); // no overflow
    });
  });

  group('follow, Home and Settings (AC-E06-04)', () {
    testWidgets(
      'a followed train is the first card on Home with ETA, status and freshness, and can be removed in Settings',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.status = statusJson(delay: 8, reported: 1, departed: true);
        await h.pump(tester);
        expect(
          find.text('Your train'),
          findsNothing,
        ); // none followed: search first, no empty card

        await tapText(tester, 'Trains');
        await tapText(tester, 'Choose where you start');
        await tapText(tester, 'Gampaha');
        await tapText(tester, 'Choose where you are going');
        await tapText(tester, 'Colombo Fort');
        await tapText(tester, 'Show trains');
        await tester.tap(find.textContaining('Train 8058').first);
        await tester.pumpAndSettle();
        await tapText(tester, 'Follow this train');
        expect(find.text('Stop following'), findsOneWidget);
        await tester.pageBack();
        await tester.pumpAndSettle();
        await tester.pageBack();
        await tester.pumpAndSettle();
        await tapText(tester, 'Home');

        expect(find.text('Your train'), findsOneWidget);
        expect(find.text('Arrives at Gampaha'), findsOneWidget);
        expect(find.text('08:38'), findsOneWidget);
        expect(find.text('Delayed 8 min'), findsOneWidget);
        expect(find.text('Updated 08:42'), findsOneWidget);

        await tapText(tester, 'Account');
        expect(find.text('Train 8058'), findsOneWidget);
        await tapText(tester, 'Remove');
        expect(find.text('You are not following any train.'), findsOneWidget);
        expect(h.prefs.follows, isEmpty);
      },
    );

    testWidgets(
      'changing the language in Settings rebuilds the screen in place',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        await h.pump(tester);
        await tapText(tester, 'Account');
        await tapText(tester, 'Language');

        await tapText(tester, 'தமிழ்');

        expect(h.prefs.language?.code, 'ta');
        expect(find.text('அமைப்புகள்'), findsOneWidget); // Settings, in Tamil
      },
    );

    testWidgets(
      'the Alerts tab says there are no service alerts, and My tickets says tickets are not available',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        await h.pump(tester);

        await tapText(tester, 'Alerts');
        expect(find.text('No service alerts'), findsOneWidget);
        await tapText(tester, 'My tickets');
        expect(
          find.text('Tickets are not available in this version.'),
          findsOneWidget,
        );
      },
    );

    testWidgets('Settings says what is kept on the phone', (tester) async {
      final h = await Harness.create(stored: Harness.used);
      await h.pump(tester);

      await tapText(tester, 'Account');

      expect(find.textContaining('There is no account'), findsOneWidget);
      expect(find.textContaining('No location is used'), findsOneWidget);
    });

    testWidgets('Home says so when the line status cannot be loaded (G6)', (
      tester,
    ) async {
      final api = FakeApi()..failing = true;
      final h = await Harness.create(stored: Harness.used, api: api);
      await h.pump(tester);

      expect(find.text('Line status unavailable'), findsOneWidget);
    });
  });
}
