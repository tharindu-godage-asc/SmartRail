// GENERATED FILE. Do not edit by hand.
// Source: smartrail-tokens.json. Regenerate with: dart run tool/generate_tokens.dart
// ignore_for_file: public_member_api_docs

import 'package:flutter/material.dart';

/// Colours for one theme. The app reads them with `Theme.of(context).extension<SmartRailColors>()!`.
@immutable
class SmartRailColors extends ThemeExtension<SmartRailColors> {
  const SmartRailColors({
    required this.bgPage,
    required this.bgSurface,
    required this.textPrimary,
    required this.textSecondary,
    required this.borderDefault,
    required this.statusNormalFg,
    required this.statusNormalChipBg,
    required this.statusWarningFg,
    required this.statusWarningChipBg,
    required this.statusUnknownFg,
    required this.statusUnknownChipBg,
    required this.statusDisruptedFg,
    required this.statusDisruptedChipBg,
    required this.statusDisruptedOnFilled,
    required this.brandAccent,
    required this.brandOnAccent,
    required this.brandAccentSurface,
  });

  final Color bgPage;
  final Color bgSurface;
  final Color textPrimary;
  final Color textSecondary;
  final Color borderDefault;
  final Color statusNormalFg;
  final Color statusNormalChipBg;
  final Color statusWarningFg;
  final Color statusWarningChipBg;
  final Color statusUnknownFg;
  final Color statusUnknownChipBg;
  final Color statusDisruptedFg;
  final Color statusDisruptedChipBg;
  final Color statusDisruptedOnFilled;
  final Color brandAccent;
  final Color brandOnAccent;
  final Color brandAccentSurface;

  static const light = SmartRailColors(
    bgPage: Color(0xFFFFFFFF),
    bgSurface: Color(0xFFF6F7F8),
    textPrimary: Color(0xFF1B1F24),
    textSecondary: Color(0xFF4A5058),
    borderDefault: Color(0xFFD5D9DE),
    statusNormalFg: Color(0xFF1F5FBF),
    statusNormalChipBg: Color(0xFFE4ECF7),
    statusWarningFg: Color(0xFF9E4A00),
    statusWarningChipBg: Color(0xFFF3E9E0),
    statusUnknownFg: Color(0xFF666A6E),
    statusUnknownChipBg: Color(0xFFF6F6F6),
    statusDisruptedFg: Color(0xFFB0003A),
    statusDisruptedChipBg: Color(0xFFF6E0E7),
    statusDisruptedOnFilled: Color(0xFFFFFFFF),
    brandAccent: Color(0xFF00696F),
    brandOnAccent: Color(0xFFFFFFFF),
    brandAccentSurface: Color(0xFFE3F1F2),
  );

  static const dark = SmartRailColors(
    bgPage: Color(0xFF1B1F24),
    bgSurface: Color(0xFF262B32),
    textPrimary: Color(0xFFF2F4F6),
    textSecondary: Color(0xFFC4C8CC),
    borderDefault: Color(0xFF3A4048),
    statusNormalFg: Color(0xFF6EA8FF),
    statusNormalChipBg: Color(0xFF252F3E),
    statusWarningFg: Color(0xFFFFB74D),
    statusWarningChipBg: Color(0xFF363129),
    statusUnknownFg: Color(0xFFC4C8CC),
    statusUnknownChipBg: Color(0xFF25292E),
    statusDisruptedFg: Color(0xFFFF6B8B),
    statusDisruptedChipBg: Color(0xFF362830),
    statusDisruptedOnFilled: Color(0xFF1B1F24),
    brandAccent: Color(0xFF4FC3CC),
    brandOnAccent: Color(0xFF1B1F24),
    brandAccentSurface: Color(0xFF12363A),
  );

  @override
  SmartRailColors copyWith({
    Color? bgPage,
    Color? bgSurface,
    Color? textPrimary,
    Color? textSecondary,
    Color? borderDefault,
    Color? statusNormalFg,
    Color? statusNormalChipBg,
    Color? statusWarningFg,
    Color? statusWarningChipBg,
    Color? statusUnknownFg,
    Color? statusUnknownChipBg,
    Color? statusDisruptedFg,
    Color? statusDisruptedChipBg,
    Color? statusDisruptedOnFilled,
    Color? brandAccent,
    Color? brandOnAccent,
    Color? brandAccentSurface,
  }) => SmartRailColors(
    bgPage: bgPage ?? this.bgPage,
    bgSurface: bgSurface ?? this.bgSurface,
    textPrimary: textPrimary ?? this.textPrimary,
    textSecondary: textSecondary ?? this.textSecondary,
    borderDefault: borderDefault ?? this.borderDefault,
    statusNormalFg: statusNormalFg ?? this.statusNormalFg,
    statusNormalChipBg: statusNormalChipBg ?? this.statusNormalChipBg,
    statusWarningFg: statusWarningFg ?? this.statusWarningFg,
    statusWarningChipBg: statusWarningChipBg ?? this.statusWarningChipBg,
    statusUnknownFg: statusUnknownFg ?? this.statusUnknownFg,
    statusUnknownChipBg: statusUnknownChipBg ?? this.statusUnknownChipBg,
    statusDisruptedFg: statusDisruptedFg ?? this.statusDisruptedFg,
    statusDisruptedChipBg: statusDisruptedChipBg ?? this.statusDisruptedChipBg,
    statusDisruptedOnFilled:
        statusDisruptedOnFilled ?? this.statusDisruptedOnFilled,
    brandAccent: brandAccent ?? this.brandAccent,
    brandOnAccent: brandOnAccent ?? this.brandOnAccent,
    brandAccentSurface: brandAccentSurface ?? this.brandAccentSurface,
  );

  @override
  SmartRailColors lerp(ThemeExtension<SmartRailColors>? other, double t) {
    if (other is! SmartRailColors) {
      return this;
    }
    return SmartRailColors(
      bgPage: Color.lerp(bgPage, other.bgPage, t)!,
      bgSurface: Color.lerp(bgSurface, other.bgSurface, t)!,
      textPrimary: Color.lerp(textPrimary, other.textPrimary, t)!,
      textSecondary: Color.lerp(textSecondary, other.textSecondary, t)!,
      borderDefault: Color.lerp(borderDefault, other.borderDefault, t)!,
      statusNormalFg: Color.lerp(statusNormalFg, other.statusNormalFg, t)!,
      statusNormalChipBg: Color.lerp(
        statusNormalChipBg,
        other.statusNormalChipBg,
        t,
      )!,
      statusWarningFg: Color.lerp(statusWarningFg, other.statusWarningFg, t)!,
      statusWarningChipBg: Color.lerp(
        statusWarningChipBg,
        other.statusWarningChipBg,
        t,
      )!,
      statusUnknownFg: Color.lerp(statusUnknownFg, other.statusUnknownFg, t)!,
      statusUnknownChipBg: Color.lerp(
        statusUnknownChipBg,
        other.statusUnknownChipBg,
        t,
      )!,
      statusDisruptedFg: Color.lerp(
        statusDisruptedFg,
        other.statusDisruptedFg,
        t,
      )!,
      statusDisruptedChipBg: Color.lerp(
        statusDisruptedChipBg,
        other.statusDisruptedChipBg,
        t,
      )!,
      statusDisruptedOnFilled: Color.lerp(
        statusDisruptedOnFilled,
        other.statusDisruptedOnFilled,
        t,
      )!,
      brandAccent: Color.lerp(brandAccent, other.brandAccent, t)!,
      brandOnAccent: Color.lerp(brandOnAccent, other.brandOnAccent, t)!,
      brandAccentSurface: Color.lerp(
        brandAccentSurface,
        other.brandAccentSurface,
        t,
      )!,
    );
  }
}

abstract final class Space {
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 16;
  static const double lg = 24;
  static const double xl = 32;
}

abstract final class Radii {
  static const double sm = 4;
  static const double md = 8;
  static const double lg = 12;
  static const double pill = 999;
}

abstract final class TouchTarget {
  static const double min = 44;
}

abstract final class TypeSize {
  static const double mobileHeading = 24;
  static const double mobileBody = 16;
  static const double mobileLabel = 14;
  static const double mobileCaption = 12;
  static const double mobileTime = 56;
  static const double displayLevel1Time = 160;
  static const double displayLevel1Status = 96;
  static const double displayLevel2 = 64;
  static const double displayLevel3 = 48;
  static const double displayLevel4 = 28;
  static const double scriptMinStatusSize = 14;
  static const double scriptCaptionSize = 13;
}

abstract final class LineHeight {
  static const double mobileLatin = 1.4;
  static const double mobileSinhala = 1.6;
  static const double mobileTamil = 1.5;
  static const double displayLatin = 1.15;
  static const double displaySinhala = 1.4;
  static const double displayTamil = 1.3;
}

abstract final class ChipOutline {
  static const double normal = 1.5;
  static const double warning = 3;
  static const double unknown = 1.5;
  static const double disrupted = 0;
}

/// Icon names from the tokens, as Material icons. Every status shows an icon and text, never colour alone.
abstract final class TokenIcons {
  static const IconData statusOnTime = Icons.schedule;
  static const IconData statusDelayed = Icons.warning;
  static const IconData statusWaiting = Icons.help;
  static const IconData statusSuspended = Icons.pause_circle;
  static const IconData statusCancelled = Icons.cancel;
  static const IconData statusTerminated = Icons.stop_circle;
  static const IconData groupNormal = Icons.schedule;
  static const IconData groupWarning = Icons.warning;
  static const IconData groupUnknown = Icons.help;
  static const IconData groupDisrupted = Icons.cancel;
  static const IconData verificationVerified = Icons.verified_user;
  static const IconData stopCurrent = Icons.location_on;
  static const IconData stopPassed = Icons.done;
  static const IconData stopUpcoming = Icons.radio_button_unchecked;
  static const IconData stopInferred = Icons.more_horiz;
  static const IconData connectionConnected = Icons.cloud_done;
  static const IconData connectionReconnecting = Icons.sync;
  static const IconData connectionOffline = Icons.cloud_off;
  static const IconData deliveryDelivered = Icons.check_circle;
  static const IconData deliveryNotSent = Icons.schedule_send;
  static const IconData deliveryRejected = Icons.error;
}
