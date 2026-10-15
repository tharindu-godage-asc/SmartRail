import 'package:flutter/material.dart';

import '../strings.dart';
import '../theme/app_theme.dart';
import '../theme/tokens.g.dart';

/// Top bar: logo, "Operator" and the signed-in name (every screen after sign-in).
class AppHeader extends StatelessWidget {
  const AppHeader({this.name, super.key});

  final String? name;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Container(
      constraints: const BoxConstraints(minHeight: 56),
      padding: const EdgeInsets.symmetric(
        horizontal: Space.md,
        vertical: Space.sm,
      ),
      decoration: BoxDecoration(
        color: colors.bgPage,
        border: Border(bottom: BorderSide(color: colors.borderDefault)),
      ),
      child: Row(
        children: [
          Text(
            Strings.appName,
            style: TextStyle(
              fontSize: 17,
              fontWeight: FontWeight.w700,
              color: colors.textPrimary,
            ),
          ),
          const SizedBox(width: 10),
          Container(
            padding: const EdgeInsets.only(left: 10),
            decoration: BoxDecoration(
              border: Border(left: BorderSide(color: colors.borderDefault)),
            ),
            child: Text(
              Strings.roleOperator,
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: colors.textSecondary,
              ),
            ),
          ),
          const SizedBox(width: 10),
          // The name gives way first when space runs out (long names, large text).
          Expanded(
            child: Text(
              name ?? '',
              textAlign: TextAlign.end,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: context.text.labelLarge,
            ),
          ),
        ],
      ),
    );
  }
}

/// A white card with a small upper-case title, as used for "Last report" and "Journey".
class SectionCard extends StatelessWidget {
  const SectionCard({required this.children, this.title, super.key});

  final String? title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(Space.md),
      decoration: BoxDecoration(
        color: colors.bgPage,
        border: Border.all(color: colors.borderDefault),
        borderRadius: BorderRadius.circular(Radii.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        spacing: 10,
        children: [
          if (title != null)
            Semantics(
              header: true,
              child: Text(
                title!.toUpperCase(),
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                  letterSpacing: .6,
                  color: colors.textSecondary,
                ),
              ),
            ),
          ...children,
        ],
      ),
    );
  }
}

/// The big filled button (56 px) for the one main action on a screen.
class PrimaryButton extends StatelessWidget {
  const PrimaryButton({
    required this.label,
    required this.onPressed,
    this.icon,
    super.key,
  });

  final String label;
  final IconData? icon;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return SizedBox(
      width: double.infinity,
      height: 56,
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          backgroundColor: colors.textPrimary,
          foregroundColor: colors.bgPage,
          disabledBackgroundColor: colors.borderDefault,
          disabledForegroundColor: colors.textSecondary,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.md),
          ),
          textStyle: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
        ),
        child: _ButtonLabel(label: label, icon: icon),
      ),
    );
  }
}

/// The outlined secondary button (48 px).
class SecondaryButton extends StatelessWidget {
  const SecondaryButton({
    required this.label,
    required this.onPressed,
    this.icon,
    super.key,
  });

  final String label;
  final IconData? icon;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return SizedBox(
      width: double.infinity,
      height: 48,
      child: OutlinedButton(
        onPressed: onPressed,
        style: OutlinedButton.styleFrom(
          foregroundColor: colors.textPrimary,
          backgroundColor: colors.bgPage,
          side: BorderSide(
            color: onPressed == null
                ? colors.borderDefault
                : colors.textPrimary,
            width: 1.5,
          ),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.md),
          ),
          textStyle: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
        ),
        child: _ButtonLabel(label: label, icon: icon),
      ),
    );
  }
}

class _ButtonLabel extends StatelessWidget {
  const _ButtonLabel({required this.label, this.icon});

  final String label;
  final IconData? icon;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisAlignment: MainAxisAlignment.center,
    spacing: 8,
    children: [
      if (icon != null) Icon(icon, size: 22),
      Flexible(child: Text(label, textAlign: TextAlign.center)),
    ],
  );
}

/// A line of small help text with an info icon.
class InfoLine extends StatelessWidget {
  const InfoLine(this.text, {this.icon = Icons.info, super.key});

  final String text;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final color = context.colors.textSecondary;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      spacing: 8,
      children: [
        Icon(icon, size: 20, color: color),
        Expanded(
          child: Text(
            text,
            style: TextStyle(fontSize: 14, height: 1.45, color: color),
          ),
        ),
      ],
    );
  }
}

/// Draws a dashed outline around a pill, used for "Not sent yet" so it differs from "Delivered" without colour.
class DashedPill extends StatelessWidget {
  const DashedPill({
    required this.color,
    required this.child,
    this.dashed = false,
    this.width = 1.5,
    super.key,
  });

  final Color color;
  final bool dashed;
  final double width;
  final Widget child;

  @override
  Widget build(BuildContext context) => CustomPaint(
    painter: _PillBorderPainter(
      color: color,
      dashed: dashed,
      width: width,
      fill: context.colors.bgPage,
    ),
    child: Padding(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      child: child,
    ),
  );
}

class _PillBorderPainter extends CustomPainter {
  const _PillBorderPainter({
    required this.color,
    required this.dashed,
    required this.width,
    required this.fill,
  });

  final Color color;
  final bool dashed;
  final double width;
  final Color fill;

  @override
  void paint(Canvas canvas, Size size) {
    final rect = RRect.fromRectAndRadius(
      Offset(width / 2, width / 2) &
          Size(size.width - width, size.height - width),
      Radius.circular(size.height),
    );
    canvas.drawRRect(rect, Paint()..color = fill);
    final stroke = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = width;
    if (!dashed) {
      canvas.drawRRect(rect, stroke);
      return;
    }
    final path = Path()..addRRect(rect);
    for (final metric in path.computeMetrics()) {
      var distance = 0.0;
      while (distance < metric.length) {
        canvas.drawPath(metric.extractPath(distance, distance + 5), stroke);
        distance += 9;
      }
    }
  }

  @override
  bool shouldRepaint(_PillBorderPainter old) =>
      old.color != color ||
      old.dashed != dashed ||
      old.width != width ||
      old.fill != fill;
}

/// Bottom bar with a bar above the selected tab, as in the design.
class AppBottomBar extends StatelessWidget {
  const AppBottomBar({
    required this.index,
    required this.onSelected,
    super.key,
  });

  final int index;
  final ValueChanged<int> onSelected;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Container(
      // At least 72 px; taller when the text is enlarged, so labels are never cut.
      constraints: const BoxConstraints(minHeight: 72),
      decoration: BoxDecoration(
        color: colors.bgPage,
        border: Border(top: BorderSide(color: colors.borderDefault)),
      ),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _Tab(
              label: Strings.tabJourney,
              icon: Icons.train,
              selected: index == 0,
              onTap: () => onSelected(0),
            ),
            _Tab(
              label: Strings.tabHelp,
              icon: Icons.help,
              selected: index == 1,
              onTap: () => onSelected(1),
            ),
          ],
        ),
      ),
    );
  }
}

class _Tab extends StatelessWidget {
  const _Tab({
    required this.label,
    required this.icon,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return Expanded(
      child: Semantics(
        button: true,
        selected: selected,
        label: label,
        child: InkWell(
          onTap: onTap,
          child: Stack(
            alignment: Alignment.topCenter,
            children: [
              if (selected)
                Positioned(
                  left: 0,
                  right: 0,
                  top: 0,
                  child: Container(height: 4, color: colors.textPrimary),
                ),
              Padding(
                padding: const EdgeInsets.only(top: 10),
                child: Column(
                  spacing: 4,
                  children: [
                    Icon(icon, size: 28, color: colors.textPrimary),
                    Text(
                      label,
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: selected
                            ? FontWeight.w700
                            : FontWeight.w400,
                        color: colors.textPrimary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
