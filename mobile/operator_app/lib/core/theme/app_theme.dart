import 'package:flutter/material.dart';

import 'tokens.g.dart';

/// The one theme of the operator app. Light only: the designs have no dark theme for the operator app, although the
/// tokens carry dark values for later. Values come from `smartrail-tokens.json` through the generated file.
ThemeData buildAppTheme() {
  const colors = SmartRailColors.light;
  final scheme = ColorScheme.light(
    primary: colors.textPrimary,
    onPrimary: colors.bgPage,
    surface: colors.bgPage,
    onSurface: colors.textPrimary,
    outline: colors.borderDefault,
    error: colors.statusDisruptedFg,
  );

  TextStyle style(
    double size,
    FontWeight weight,
    double height, [
    Color? color,
  ]) => TextStyle(
    fontSize: size,
    fontWeight: weight,
    height: height,
    color: color ?? colors.textPrimary,
  );

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: colors.bgSurface,
    // Noto Sans is the design font. The font file is not bundled yet, so the platform font is used until it is.
    fontFamily: 'Noto Sans',
    extensions: const [colors],
    textTheme: TextTheme(
      headlineMedium: style(TypeSize.mobileHeading, FontWeight.w700, 1.25),
      bodyLarge: style(
        TypeSize.mobileBody,
        FontWeight.w400,
        LineHeight.mobileLatin,
      ),
      bodyMedium: style(
        TypeSize.mobileLabel,
        FontWeight.w400,
        LineHeight.mobileLatin,
      ),
      labelLarge: style(
        TypeSize.mobileLabel,
        FontWeight.w600,
        LineHeight.mobileLatin,
      ),
      bodySmall: style(
        TypeSize.mobileCaption,
        FontWeight.w400,
        LineHeight.mobileLatin,
        colors.textSecondary,
      ),
    ),
    dividerColor: colors.borderDefault,
  );
}

extension SmartRailThemeContext on BuildContext {
  SmartRailColors get colors => Theme.of(this).extension<SmartRailColors>()!;
  TextTheme get text => Theme.of(this).textTheme;
}
