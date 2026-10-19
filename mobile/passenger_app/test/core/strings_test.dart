import 'package:flutter_test/flutter_test.dart';
import 'package:passenger_app/core/l10n/strings.dart';

void main() {
  test(
    'every text exists in English, Sinhala and Tamil (EC-78, AC-E06-05 1)',
    () {
      for (final e in Strings.all) {
        final (en, si, ta) = e.value;
        expect(en, isNotEmpty, reason: '${e.key} en');
        expect(si, isNotEmpty, reason: '${e.key} si');
        expect(ta, isNotEmpty, reason: '${e.key} ta');
      }
    },
  );

  test('every key of the enum has a text', () {
    expect(Strings.all.map((e) => e.key).toSet(), K.values.toSet());
  });

  test(
    'a text with a value in it has the same placeholders in every language',
    () {
      final marker = RegExp(r'\{(\w+)\}');
      Set<String> names(String s) =>
          marker.allMatches(s).map((m) => m.group(1)!).toSet();
      for (final e in Strings.all) {
        final (en, si, ta) = e.value;
        expect(names(si), names(en), reason: '${e.key} si');
        expect(names(ta), names(en), reason: '${e.key} ta');
      }
    },
  );

  test('values are filled in, and the chosen language is used', () {
    expect(
      const Strings(Lang.en).fmt(K.arrivesAt, {'station': 'Gampaha'}),
      'Arrives at Gampaha',
    );
    expect(const Strings(Lang.si).fmt(K.trainN, {'n': '8058'}), 'දුම්රිය 8058');
    expect(const Strings(Lang.ta)[K.onTime], 'சரியான நேரத்தில்');
  });

  test('the phrases taken from the design canvas are as drawn', () {
    const en = Strings(Lang.en);
    expect(en[K.offlineBanner], "You're offline · Showing info from {time}");
    expect(en[K.waiting], 'Waiting for update');
    expect(en[K.noNewsYet], 'No news yet. Last update {time}.');
  });
}
