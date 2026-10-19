import 'package:flutter/material.dart';

import '../l10n/strings.dart';
import 'tokens.g.dart';

/// The one theme of the passenger app: light, with the teal accent (the passenger app's own, never used for journey status). Values come
/// from `smartrail-tokens.json` through the generated file. The fonts are bundled, with the script line heights of the tokens.
ThemeData buildAppTheme(Lang lang) {
  const colors = SmartRailColors.light;
  final height = switch (lang) {
    Lang.en => LineHeight.mobileLatin,
    Lang.si => LineHeight.mobileSinhala,
    Lang.ta => LineHeight.mobileTamil,
  };
  TextStyle style(double size, FontWeight weight, [Color? color, double? h]) =>
      TextStyle(
        fontFamily: 'NotoSans',
        fontFamilyFallback: const ['NotoSansSinhala', 'NotoSansTamil'],
        fontSize: size,
        fontWeight: weight,
        height: h ?? height,
        color: color ?? colors.textPrimary,
      );

  return ThemeData(
    useMaterial3: true,
    fontFamily: 'NotoSans',
    fontFamilyFallback: const ['NotoSansSinhala', 'NotoSansTamil'],
    colorScheme: ColorScheme.light(
      primary: colors.brandAccent,
      onPrimary: colors.brandOnAccent,
      surface: colors.bgPage,
      onSurface: colors.textPrimary,
      outline: colors.borderDefault,
      error: colors.statusDisruptedFg,
    ),
    scaffoldBackgroundColor: colors.bgSurface,
    extensions: const [colors],
    textTheme: TextTheme(
      headlineMedium: style(
        TypeSize.mobileHeading,
        FontWeight.w700,
        null,
        1.25,
      ),
      bodyLarge: style(TypeSize.mobileBody, FontWeight.w400),
      bodyMedium: style(TypeSize.mobileLabel, FontWeight.w400),
      labelLarge: style(TypeSize.mobileLabel, FontWeight.w600),
      bodySmall: style(
        TypeSize.mobileCaption,
        FontWeight.w400,
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
