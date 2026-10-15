import 'dart:convert';
import 'dart:io';

import 'package:flutter/painting.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/core/theme/tokens.g.dart';

import '../../tool/tokens_generator.dart';

void main() {
  final tokens = jsonDecode(
    File('smartrail-tokens.json').readAsStringSync(),
  ) as Map<String, dynamic>;

  test('the committed tokens.g.dart is what the generator makes from smartrail-tokens.json', () {
    final committed = File('lib/core/theme/tokens.g.dart').readAsStringSync();
    // The committed file is formatted with dart format; compare without whitespace so formatting never fails this test.
    String squash(String s) => s
        .replaceAll(RegExp(r'\s+'), '')
        .replaceAll(',)', ')')
        .replaceAll(',]', ']')
        .replaceAll(',}', '}');
    expect(
      squash(committed),
      squash(generateTokens(tokens)),
      reason: 'run: dart run tool/generate_tokens.dart',
    );
  });

  test('status colours, spacing and the touch target match the design system (discovery 20)', () {
    expect(SmartRailColors.light.statusNormalFg, const Color(0xFF1F5FBF));
    expect(SmartRailColors.light.statusWarningFg, const Color(0xFF9E4A00));
    expect(SmartRailColors.light.statusDisruptedFg, const Color(0xFFB0003A));
    expect(SmartRailColors.dark.bgPage, const Color(0xFF1B1F24));
    expect(
      [Space.xs, Space.sm, Space.md, Space.lg, Space.xl],
      [4, 8, 16, 24, 32],
    );
    expect(TouchTarget.min, 44);
  });

  test('the four status groups differ by outline as well as colour (accessibility audit)', () {
    expect(ChipOutline.warning, greaterThan(ChipOutline.normal));
    expect(ChipOutline.disrupted, 0);
  });
}
