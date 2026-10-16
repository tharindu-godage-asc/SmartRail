import 'package:flutter/material.dart';

import '../../domain/logic.dart';
import '../l10n/strings.dart';
import '../theme/app_theme.dart';
import '../theme/tokens.g.dart';

/// The words of a status. Delays read in hours and minutes once an hour or more (4 h, never 240 min).
String statusText(Strings s, StatusKind kind, String delay) => switch (kind) {
  StatusKind.onTime => s[K.onTime],
  StatusKind.delayed => s.fmt(K.delayedFor, {'t': delay}),
  StatusKind.scheduled => s[K.scheduled],
  StatusKind.waiting => s[K.waiting],
  StatusKind.arrived => s[K.arrived],
};

/// A status as icon plus words, never colour alone (NFR-30). Outline weights follow the tokens: thick for warning, dashed for unknown.
class StatusChip extends StatelessWidget {
  const StatusChip({
    required this.kind,
    required this.label,
    this.large = false,
    super.key,
  });

  final StatusKind kind;
  final String label;
  final bool large;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final (fg, bg, icon, width, dashed) = switch (kind) {
      StatusKind.delayed => (
        c.statusWarningFg,
        c.statusWarningChipBg,
        Icons.warning_amber_rounded,
        ChipOutline.warning,
        false,
      ),
      StatusKind.waiting => (
        c.statusUnknownFg,
        c.statusUnknownChipBg,
        Icons.help_outline,
        ChipOutline.unknown,
        true,
      ),
      StatusKind.arrived => (
        c.statusNormalFg,
        c.statusNormalChipBg,
        Icons.done,
        ChipOutline.normal,
        false,
      ),
      _ => (
        c.statusNormalFg,
        c.statusNormalChipBg,
        Icons.schedule,
        ChipOutline.normal,
        false,
      ),
    };
    final size = large ? 22.0 : 15.0;
    final chip = Container(
      padding: EdgeInsets.symmetric(
        horizontal: large ? 14 : 10,
        vertical: large ? 8 : 4,
      ),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(Radii.pill),
        border: dashed ? null : Border.all(color: fg, width: width),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: size + 4, color: fg),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              label,
              style: TextStyle(
                fontSize: size,
                fontWeight: FontWeight.w700,
                color: fg,
                height: 1.2,
              ),
            ),
          ),
        ],
      ),
    );
    return dashed ? _Dashed(color: fg, child: chip) : chip;
  }
}

class _Dashed extends StatelessWidget {
  const _Dashed({required this.color, required this.child});

  final Color color;
  final Widget child;

  @override
  Widget build(BuildContext context) =>
      CustomPaint(foregroundPainter: _DashPainter(color), child: child);
}

class _DashPainter extends CustomPainter {
  _DashPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5;
    final path = Path()
      ..addRRect(
        RRect.fromRectAndRadius(
          Offset.zero & size,
          Radius.circular(size.height / 2),
        ),
      );
    for (final m in path.computeMetrics()) {
      for (var d = 0.0; d < m.length; d += 7) {
        canvas.drawPath(m.extractPath(d, d + 4), paint);
      }
    }
  }

  @override
  bool shouldRepaint(_DashPainter old) => old.color != color;
}

/// A white card with the design's border and radius.
class SectionCard extends StatelessWidget {
  const SectionCard({
    required this.child,
    this.padding = const EdgeInsets.all(Space.md),
    super.key,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    // A Material (not a decorated box), so list tiles inside a card show their ink and the tests' ListTile check stays quiet.
    return SizedBox(
      width: double.infinity,
      child: Material(
        color: c.bgPage,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(
          side: BorderSide(color: c.borderDefault),
          borderRadius: BorderRadius.circular(Radii.lg),
        ),
        child: Padding(padding: padding, child: child),
      ),
    );
  }
}

/// The teal primary button (56 px in the design; never smaller than the 44 px touch target).
class PrimaryButton extends StatelessWidget {
  const PrimaryButton({
    required this.label,
    required this.onPressed,
    this.icon,
    super.key,
  });

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return SizedBox(
      width: double.infinity,
      height: 52,
      child: FilledButton.icon(
        onPressed: onPressed,
        icon: icon == null ? const SizedBox.shrink() : Icon(icon, size: 20),
        label: Text(
          label,
          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
        ),
        style: FilledButton.styleFrom(
          backgroundColor: c.brandAccent,
          foregroundColor: c.brandOnAccent,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.md),
          ),
        ),
      ),
    );
  }
}

/// The strip that says the information is old: offline, or the server is not answering (spec 10 rule 7). It always says from when.
class LinkBanner extends StatelessWidget {
  const LinkBanner({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Semantics(
      liveRegion: true,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(
          horizontal: Space.md,
          vertical: Space.sm + 2,
        ),
        color: c.textPrimary,
        child: Row(
          children: [
            Icon(Icons.cloud_off, size: 20, color: c.bgPage),
            const SizedBox(width: Space.sm),
            Expanded(
              child: Text(
                text,
                style: TextStyle(
                  color: c.bgPage,
                  fontWeight: FontWeight.w600,
                  fontSize: 14,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A heading-less message in the middle of a screen (empty states, errors), with an optional action.
class Notice extends StatelessWidget {
  const Notice({
    required this.title,
    this.body,
    this.icon = Icons.info_outline,
    this.action,
    super.key,
  });

  final String title;
  final String? body;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(Space.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 40, color: c.textSecondary),
            const SizedBox(height: Space.md),
            Text(
              title,
              textAlign: TextAlign.center,
              style: context.text.labelLarge?.copyWith(fontSize: 18),
            ),
            if (body != null) ...[
              const SizedBox(height: Space.sm),
              Text(
                body!,
                textAlign: TextAlign.center,
                style: context.text.bodyMedium?.copyWith(
                  color: c.textSecondary,
                ),
              ),
            ],
            if (action != null) ...[const SizedBox(height: Space.md), action!],
          ],
        ),
      ),
    );
  }
}

/// The small header of an inner screen: back, a title and an optional trailing widget.
class InnerHeader extends StatelessWidget implements PreferredSizeWidget {
  const InnerHeader({required this.title, this.trailing, super.key});

  final Widget title;
  final Widget? trailing;

  /// The height of the status bar the header must start below. Set once per build by the app's top banner wrapper (it is zero while the
  /// offline banner covers the status bar), because a Scaffold gives its app bar no inset of its own.
  static double statusInset = 0;

  @override
  Size get preferredSize => Size.fromHeight(56 + statusInset);

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Material(
      color: c.bgPage,
      child: Container(
        // The bar starts below the status bar (and any camera cut-out).
        padding: EdgeInsets.only(top: statusInset),
        height: 56 + statusInset,
        decoration: BoxDecoration(
          border: Border(bottom: BorderSide(color: c.borderDefault)),
        ),
        child: Row(
          children: [
            IconButton(
              tooltip: MaterialLocalizations.of(context).backButtonTooltip,
              icon: const Icon(Icons.arrow_back),
              onPressed: () => Navigator.of(context).maybePop(),
            ),
            Expanded(child: Center(child: title)),
            SizedBox(width: 48, child: trailing),
          ],
        ),
      ),
    );
  }
}
