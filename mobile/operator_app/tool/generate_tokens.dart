import 'dart:convert';
import 'dart:io';

import 'tokens_generator.dart';

/// Run from `mobile/operator_app`: `dart run tool/generate_tokens.dart`
void main() {
  final json = jsonDecode(
    File('smartrail-tokens.json').readAsStringSync(),
  ) as Map<String, dynamic>;
  File('lib/core/theme/tokens.g.dart')
    ..createSync(recursive: true)
    ..writeAsStringSync(generateTokens(json));
  stdout.writeln('Wrote lib/core/theme/tokens.g.dart');
}
