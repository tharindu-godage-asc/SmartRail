/// Turns `smartrail-tokens.json` (DTCG format, ADR-16) into the Dart constants the app uses.
/// Pure string work with no Flutter imports, so `dart run tool/generate_tokens.dart` and the test can both call it.
library;

const _header = '''// GENERATED FILE. Do not edit by hand.
// Source: smartrail-tokens.json. Regenerate with: dart run tool/generate_tokens.dart
// ignore_for_file: public_member_api_docs

import 'package:flutter/material.dart';
''';

String generateTokens(Map<String, dynamic> tokens) {
  final colors = _leaves(tokens['color'] as Map<String, dynamic>);
  final out = StringBuffer(_header)
    ..writeln()
    ..write(_colorClass(colors))
    ..writeln()
    ..write(_constants('Space', 'space', tokens, _dimension))
    ..writeln()
    ..write(_constants('Radii', 'radius', tokens, _dimension))
    ..writeln()
    ..write(_constants('TouchTarget', 'size', tokens, _dimension))
    ..writeln()
    ..write(_constants('TypeSize', 'type', tokens, _dimension))
    ..writeln()
    ..write(_constants('LineHeight', 'lineHeight', tokens, (v) => '$v'))
    ..writeln()
    ..write(
      _constants(
        'ChipOutline',
        'statusChip',
        tokens,
        _chipValue,
        only: 'outlineWidth',
      ),
    )
    ..writeln()
    ..write(_icons(tokens['icon'] as Map<String, dynamic>));
  return out.toString();
}

/// Every token with a `$value`, keyed by its path, for example `status.normal.fg`.
Map<String, Map<String, dynamic>> _leaves(
  Map<String, dynamic> node, [
  String prefix = '',
]) {
  final result = <String, Map<String, dynamic>>{};
  node.forEach((key, value) {
    if (key.startsWith(r'$') || value is! Map<String, dynamic>) {
      return;
    }
    final path = prefix.isEmpty ? key : '$prefix.$key';
    if (value.containsKey(r'$value')) {
      result[path] = value;
    } else {
      result.addAll(_leaves(value, path));
    }
  });
  return result;
}

String _camel(String path) {
  final parts = path.split(RegExp(r'[.\-_]'));
  return parts.first +
      parts
          .skip(1)
          .map((p) => p.isEmpty ? p : p[0].toUpperCase() + p.substring(1))
          .join();
}

String _hex(String value) {
  final h = value.replaceFirst('#', '').toUpperCase();
  return 'Color(0xFF$h)';
}

String _colorClass(Map<String, Map<String, dynamic>> colors) {
  final names = {for (final path in colors.keys) path: _camel(path)};
  final b = StringBuffer()
    ..writeln(
      '/// Colours for one theme. The app reads them with `Theme.of(context).extension<SmartRailColors>()!`.',
    )
    ..writeln('@immutable')
    ..writeln('class SmartRailColors extends ThemeExtension<SmartRailColors> {')
    ..writeln('  const SmartRailColors({');
  for (final name in names.values) {
    b.writeln('    required this.$name,');
  }
  b
    ..writeln('  });')
    ..writeln();
  for (final name in names.values) {
    b.writeln('  final Color $name;');
  }

  b
    ..writeln()
    ..writeln('  static const light = SmartRailColors(');
  colors.forEach(
    (path, token) =>
        b.writeln('    ${names[path]}: ${_hex(token[r'$value'] as String)},'),
  );
  b
    ..writeln('  );')
    ..writeln()
    ..writeln('  static const dark = SmartRailColors(');
  colors.forEach((path, token) {
    final dark =
        (token[r'$extensions'] as Map<String, dynamic>?)?['smartrail']?['dark']
            as String?;
    b.writeln(
      '    ${names[path]}: ${_hex(dark ?? token[r'$value'] as String)},',
    );
  });
  b
    ..writeln('  );')
    ..writeln()
    ..writeln('  @override')
    ..writeln('  SmartRailColors copyWith({');
  for (final name in names.values) {
    b.writeln('    Color? $name,');
  }
  b
    ..writeln('  }) =>')
    ..writeln('      SmartRailColors(');
  for (final name in names.values) {
    b.writeln('        $name: $name ?? this.$name,');
  }
  b
    ..writeln('      );')
    ..writeln()
    ..writeln('  @override')
    ..writeln(
      '  SmartRailColors lerp(ThemeExtension<SmartRailColors>? other, double t) {',
    )
    ..writeln('    if (other is! SmartRailColors) {')
    ..writeln('      return this;')
    ..writeln('    }')
    ..writeln('    return SmartRailColors(');
  for (final name in names.values) {
    b.writeln('      $name: Color.lerp($name, other.$name, t)!,');
  }
  b
    ..writeln('    );')
    ..writeln('  }')
    ..writeln('}');
  return b.toString();
}

String _dimension(Object? value) => (value as String).replaceAll('px', '');

String _chipValue(Object? value) => _dimension(value);

String _constants(
  String className,
  String root,
  Map<String, dynamic> tokens,
  String Function(Object? value) format, {
  String? only,
}) {
  final leaves = _leaves(tokens[root] as Map<String, dynamic>);
  final b = StringBuffer()..writeln('abstract final class $className {');
  leaves.forEach((path, token) {
    if (only != null && !path.endsWith(only)) {
      return;
    }
    // statusChip.normal.outlineWidth becomes normal; type.mobile.heading.size becomes mobileHeading.
    final name = _camel(
      only == null
          ? path
                .replaceAll(RegExp(r'\.size$'), '')
                .replaceFirst('touchTarget.', '')
          : path.replaceAll('.$only', ''),
    );
    b.writeln('  static const double $name = ${format(token[r'$value'])};');
  });
  b.writeln('}');
  return b.toString();
}

String _icons(Map<String, dynamic> icons) {
  final leaves = _leaves(icons);
  final b = StringBuffer()
    ..writeln(
      '/// Icon names from the tokens, as Material icons. Every status shows an icon and text, never colour alone.',
    )
    ..writeln('abstract final class TokenIcons {');
  leaves.forEach((path, token) {
    b.writeln(
      '  static const IconData ${_camel(path)} = Icons.${token[r'$value']};',
    );
  });
  b.writeln('}');
  return b.toString();
}
