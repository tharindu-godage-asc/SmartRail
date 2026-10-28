import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:passenger_app/app/app.dart';
import 'package:passenger_app/app/providers.dart';
import 'package:passenger_app/data/preferences.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Integration test on a phone or emulator, against the real SmartRail API (Compose stack), with no fakes.
/// Needs: `docker compose up` (or ./start.sh), `adb reverse tcp:8080 tcp:8080`, then
///   flutter test integration_test --device-id emulator-5554
/// It finds Veyangoda to Colombo Fort in the real station list and lists the trains the real API answers with. It reads only, so it can
/// be repeated on any database. The search needs a train still to come today (the seed has a night train 8075 at 22:10 every day).
/// Waits for a real network answer: `pumpAndSettle` returns as soon as no frame is pending, which is before the server has replied.
Future<void> waitFor(
  WidgetTester tester,
  Finder finder, {
  Duration timeout = const Duration(seconds: 20),
}) async {
  final end = DateTime.now().add(timeout);
  while (finder.evaluate().isEmpty && DateTime.now().isBefore(end)) {
    await tester.pump(const Duration(milliseconds: 250));
  }
}

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets(
    'a passenger finds trains from Veyangoda to Colombo Fort from the real API',
    (tester) async {
      // A phone that has been used: language chosen, intro done, nothing remembered.
      SharedPreferences.setMockInitialValues({
        'language': 'en',
        'onboarded': true,
      });
      final prefs = await SharedPreferences.getInstance();

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            preferencesProvider.overrideWithValue(Preferences(prefs)),
          ],
          child: const PassengerApp(),
        ),
      );
      await tester.pumpAndSettle(const Duration(seconds: 1));
      expect(find.text('Where to today?'), findsOneWidget);

      await tester.tap(find.text('Trains').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Choose where you start').first);
      await waitFor(tester, find.text('Veyangoda')); // the real station list
      expect(
        find.text('Veyangoda'),
        findsWidgets,
        reason: 'the station list came from the real API',
      );
      await tester.tap(find.text('Veyangoda').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Choose where you are going').first);
      await waitFor(tester, find.text('Colombo Fort'));
      await tester.tap(find.text('Colombo Fort').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Show trains').first);
      await waitFor(
        tester,
        find.textContaining('Train '),
      ); // the real search answer

      // Either trains are listed (each card says "Train <number>"), or it is late and the real API has none left today.
      final trains = find.textContaining('Train ');
      expect(
        trains,
        findsWidgets,
        reason: 'the real API listed at least one train from Veyangoda to Colombo Fort',
      );
    },
  );
}
