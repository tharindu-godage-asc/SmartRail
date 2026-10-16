import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:passenger_app/app/providers.dart';
import 'package:passenger_app/data/menu.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

void main() {
  Future<void> tapText(WidgetTester tester, String text) async {
    await tester.tap(find.text(text).first);
    await tester.pumpAndSettle();
  }

  Future<void> openLive(
    WidgetTester tester,
    Harness h, {
    List<Override> overrides = const [],
  }) async {
    await h.pump(tester, overrides: overrides);
    await tapText(tester, 'Trains');
    await tapText(tester, 'Choose where you start');
    await tapText(tester, 'Gampaha');
    await tapText(tester, 'Choose where you are going');
    await tapText(tester, 'Colombo Fort');
    await tapText(tester, 'Show trains');
    await tester.tap(find.textContaining('Train 8058').first);
    await tester.pumpAndSettle();
  }

  group('food menu (spec 11, AC-E08-01, AC-E08-07)', () {
    testWidgets(
      'a trip with a menu shows the row, the items with price and Available or Sold out, and no order controls',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.menuAnswer = menuJson(fruitSold: true);
        await openLive(tester, h);

        await tapText(tester, 'Food menu');

        expect(find.text('Egg sandwich'), findsOneWidget);
        expect(find.text('Rs 250.00'), findsOneWidget);
        expect(find.text('Available'), findsOneWidget);
        expect(
          find.text('Sold out'),
          findsOneWidget,
        ); // text beside an icon, not colour alone
        expect(find.text('Menu from 08:30'), findsOneWidget);
        expect(
          find.textContaining('SmartRail does not see your order'),
          findsOneWidget,
        );
        expect(find.textContaining('Add'), findsNothing);
        expect(find.textContaining('Basket'), findsNothing);
      },
    );

    testWidgets('a trip with no food shows no Food menu row', (tester) async {
      final h = await Harness.create(stored: Harness.used);
      await openLive(tester, h);

      expect(find.text('Food menu'), findsNothing);
    });

    test('an item with no Sinhala name falls back to English (EC-184)', () {
      final items = TrainMenu.fromJson(menuJson()).stations.first.items;

      expect(localName(items.last.names, 'si'), 'Fruit cup');
      expect(localName(items.first.names, 'si'), 'බිත්තර සැන්ඩ්විච්');
    });

    testWidgets(
      'a stale menu says it may have changed; a current one only says when it is from',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.menuAnswer = menuJson(stale: true);
        await openLive(tester, h);
        await tapText(tester, 'Food menu');

        expect(
          find.text('Menu from 08:30. It may have changed.'),
          findsOneWidget,
        );
      },
    );

    testWidgets('the button opens the address from the server', (tester) async {
      final h = await Harness.create(stored: Harness.used);
      h.api.menuAnswer = menuJson();
      final opened = <Uri>[];
      await openLive(
        tester,
        h,
        overrides: [
          urlOpenerProvider.overrideWithValue((uri) async {
            opened.add(uri);
            return true;
          }),
        ],
      );
      await tapText(tester, 'Food menu');

      await tester.ensureVisible(find.text("Order on the caterer's website"));
      await tapText(tester, "Order on the caterer's website");

      expect(
        opened.single.toString(),
        'http://localhost:8090/?station=CF&train=8058',
      );
    });

    testWidgets(
      'offline, the button explains and nothing opens; the list stays',
      (tester) async {
        final h = await Harness.create(stored: Harness.used);
        h.api.menuAnswer = menuJson();
        final opened = <Uri>[];
        await openLive(
          tester,
          h,
          overrides: [
            urlOpenerProvider.overrideWithValue((uri) async {
              opened.add(uri);
              return true;
            }),
          ],
        );
        await tapText(tester, 'Food menu');
        h.goOffline();
        await tester.pumpAndSettle();

        await tester.ensureVisible(find.text("Order on the caterer's website"));
        await tapText(tester, "Order on the caterer's website");

        expect(opened, isEmpty);
        expect(find.textContaining("You're offline. Connect"), findsOneWidget);
        expect(find.text('Egg sandwich'), findsOneWidget);
      },
    );

    testWidgets('no address from the server means no button', (tester) async {
      final h = await Harness.create(stored: Harness.used);
      h.api.menuAnswer = menuJson(orderUrl: null);
      await openLive(tester, h);
      await tapText(tester, 'Food menu');

      expect(find.text("Order on the caterer's website"), findsNothing);
    });
  });
}
