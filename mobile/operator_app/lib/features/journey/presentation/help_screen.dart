import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../app/providers.dart';
import '../../../core/strings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/tokens.g.dart';
import '../../../core/widgets/widgets.dart';
import '../../auth/presentation/auth_controller.dart';
import '../../reports/domain/report.dart';
import '../../reports/presentation/delivery_chip.dart';

/// "Phone the desk": opens the dialer. Used where the operator cannot report on their own.
class PhoneDeskButton extends ConsumerWidget {
  const PhoneDeskButton({this.label = Strings.phoneDesk, super.key});

  final String label;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final number = ref.watch(appConfigProvider).deskPhone;
    return SecondaryButton(
      label: label,
      icon: Icons.call,
      onPressed: () =>
          launchUrl(Uri(scheme: 'tel', path: number.replaceAll(' ', ''))),
    );
  }
}

/// The Help tab: who to phone when reporting fails, and sign out (`Nimal-PhoneFallback`).
class HelpScreen extends ConsumerWidget {
  const HelpScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = context.colors;
    final name = ref.watch(authControllerProvider).value?.displayName ?? '';
    final number = ref.watch(appConfigProvider).deskPhone;

    return ListView(
      padding: const EdgeInsets.all(Space.md),
      children: [
        Semantics(
          header: true,
          child: Text(Strings.helpTitle, style: context.text.headlineMedium),
        ),
        const SizedBox(height: 8),
        Text(Strings.helpBody, style: context.text.bodyLarge),
        const SizedBox(height: 14),
        SectionCard(
          children: [
            Row(
              spacing: 12,
              children: [
                Icon(Icons.call, size: 28, color: colors.textPrimary),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        Strings.deskName,
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      Text(
                        number,
                        style: const TextStyle(
                          fontSize: 20,
                          fontWeight: FontWeight.w700,
                          fontFeatures: [FontFeature.tabularFigures()],
                        ),
                      ),
                      Text(
                        Strings.deskHours,
                        style: TextStyle(
                          fontSize: 14,
                          color: colors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const PhoneDeskButton(label: Strings.callDesk),
          ],
        ),
        const SizedBox(height: 12),
        const InfoLine(Strings.helpNoDoubleCount),
        const SizedBox(height: 16),
        SectionCard(
          title: Strings.account,
          children: [
            Row(
              spacing: 12,
              children: [
                Icon(Icons.account_circle, size: 28, color: colors.textPrimary),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        name,
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      Text(
                        Strings.roleOperator,
                        style: TextStyle(
                          fontSize: 14,
                          color: colors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            SecondaryButton(
              label: Strings.signOut,
              icon: Icons.logout,
              onPressed: () => _confirmSignOut(context, ref),
            ),
          ],
        ),
      ],
    );
  }

  /// Says how many reports are still waiting before anything is ended (`Nimal-SignOut-Confirm`).
  Future<void> _confirmSignOut(BuildContext context, WidgetRef ref) async {
    final waiting = await ref.read(reportQueueProvider).pendingCount();
    if (!context.mounted) {
      return;
    }
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => _SignOutDialog(waiting: waiting),
    );
    if (confirmed ?? false) {
      await ref.read(authControllerProvider.notifier).signOut();
    }
  }
}

class _SignOutDialog extends StatelessWidget {
  const _SignOutDialog({required this.waiting});

  final int waiting;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    return AlertDialog(
      backgroundColor: colors.bgPage,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(Radii.lg),
      ),
      title: Text(Strings.signOutTitle, style: context.text.headlineMedium),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        spacing: 10,
        children: [
          Text(
            waiting == 0
                ? Strings.signOutNothingWaiting
                : Strings.signOutWaiting(waiting),
            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
          ),
          if (waiting > 0) ...[
            const DeliveryChip(ReportState.pending),
            Text(
              Strings.signOutKept,
              style: TextStyle(
                fontSize: 14,
                height: 1.45,
                color: colors.textSecondary,
              ),
            ),
          ],
        ],
      ),
      actionsOverflowDirection: VerticalDirection.down,
      actions: [
        PrimaryButton(
          label: Strings.signOut,
          onPressed: () => Navigator.of(context).pop(true),
        ),
        const SizedBox(height: 10),
        SecondaryButton(
          label: Strings.stayIn,
          onPressed: () => Navigator.of(context).pop(false),
        ),
      ],
    );
  }
}
