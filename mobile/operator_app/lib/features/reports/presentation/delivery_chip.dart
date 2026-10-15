import 'package:flutter/material.dart';

import '../../../core/strings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/tokens.g.dart';
import '../../../core/widgets/widgets.dart';
import '../domain/report.dart';

/// Delivered, Not sent yet or Rejected. Each has an icon, words and its own outline, so colour is never the only signal.
class DeliveryChip extends StatelessWidget {
  const DeliveryChip(this.state, {super.key});

  final ReportState state;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    final (icon, label, color, dashed, width) = switch (state) {
      ReportState.delivered => (
        TokenIcons.deliveryDelivered,
        Strings.delivered,
        colors.textSecondary,
        false,
        1.5,
      ),
      ReportState.pending => (
        TokenIcons.deliveryNotSent,
        Strings.notSentYet,
        colors.textSecondary,
        true,
        1.5,
      ),
      // Needs a person's attention, so it takes the warning style: thick solid outline.
      ReportState.rejected => (
        TokenIcons.deliveryRejected,
        Strings.rejected,
        colors.statusWarningFg,
        false,
        ChipOutline.warning,
      ),
    };

    return DashedPill(
      color: color,
      dashed: dashed,
      width: width,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        spacing: 6,
        children: [
          Icon(icon, size: 17, color: color),
          Flexible(
            child: Text(
              label,
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: color,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
