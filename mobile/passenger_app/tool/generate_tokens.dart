import 'dart:convert';
import 'dart:io';

import 'tokens_generator.dart';

/// Run from `mobile/passenger_app`: `dart run tool/generate_tokens.dart`.
/// One token file serves both apps (ADR-16): it is read from the operator app's folder.
void main() {
  final json = jsonDecode(
    File('../operator_app/smartrail-tokens.json').readAsStringSync(),
  ) as Map<String, dynamic>;
  File('lib/core/theme/tokens.g.dart')
    ..createSync(recursive: true)
    ..writeAsStringSync(generateTokens(json));
  stdout.writeln('Wrote lib/core/theme/tokens.g.dart');
}
