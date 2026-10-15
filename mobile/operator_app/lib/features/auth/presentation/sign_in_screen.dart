import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/providers.dart';
import '../../../core/strings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/tokens.g.dart';
import '../../../core/time.dart';
import '../../../core/widgets/widgets.dart';
import '../../journey/presentation/help_screen.dart';
import '../../reports/domain/report.dart';
import '../../reports/presentation/delivery_chip.dart';
import '../domain/auth.dart';
import 'auth_controller.dart';

/// Shown whenever the operator is not signed in: first start (`Nimal-Login`), an interrupted or failed sign-in, and an
/// expired session with reports still waiting (`Nimal-SessionExpired`).
class SignInScreen extends ConsumerStatefulWidget {
  const SignInScreen({super.key});

  @override
  ConsumerState<SignInScreen> createState() => _SignInScreenState();
}

class _SignInScreenState extends ConsumerState<SignInScreen> {
  bool _busy = false;

  Future<void> _signIn() async {
    if (_busy) {
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(authControllerProvider.notifier).signIn();
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authControllerProvider).value;
    final status = auth?.status ?? AuthStatus.signedOut;

    return Scaffold(
      body: SafeArea(
        child: status == AuthStatus.sessionExpired
            ? _SessionExpired(
                name: auth?.displayName,
                busy: _busy,
                onSignIn: _signIn,
              )
            : _Welcome(status: status, busy: _busy, onSignIn: _signIn),
      ),
    );
  }
}

class _Welcome extends StatelessWidget {
  const _Welcome({
    required this.status,
    required this.busy,
    required this.onSignIn,
  });

  final AuthStatus status;
  final bool busy;
  final VoidCallback onSignIn;

  @override
  Widget build(BuildContext context) {
    final colors = context.colors;
    // A notice above the welcome text when the last attempt did not work.
    final notice = switch (status) {
      AuthStatus.signInInterrupted => (
        Strings.signInInterruptedTitle,
        Strings.signInInterruptedBody,
      ),
      AuthStatus.signInCancelled => (
        Strings.signInCancelledTitle,
        Strings.signInCancelledBody,
      ),
      AuthStatus.signInFailed => (
        Strings.signInFailedTitle,
        Strings.signInFailedBody,
      ),
      _ => null,
    };

    return Column(
      children: [
        Container(
          width: double.infinity,
          padding: const EdgeInsets.fromLTRB(Space.md, 18, Space.md, 18),
          decoration: BoxDecoration(
            color: colors.bgPage,
            border: Border(bottom: BorderSide(color: colors.borderDefault)),
          ),
          child: Text(
            'Operator app',
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: colors.textSecondary,
            ),
          ),
        ),
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(Space.md),
            children: [
              const SizedBox(height: 56),
              Text(
                Strings.appName,
                style: TextStyle(
                  fontSize: 34,
                  fontWeight: FontWeight.w700,
                  color: colors.textPrimary,
                ),
              ),
              const SizedBox(height: 20),
              if (notice != null) ...[
                SectionCard(
                  children: [
                    Semantics(
                      liveRegion: true,
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        spacing: 10,
                        children: [
                          Icon(Icons.info, size: 22, color: colors.textPrimary),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              spacing: 4,
                              children: [
                                Text(
                                  notice.$1,
                                  style: const TextStyle(
                                    fontSize: 16,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                                Text(
                                  notice.$2,
                                  style: TextStyle(
                                    fontSize: 14,
                                    height: 1.45,
                                    color: colors.textSecondary,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
              ],
              Semantics(
                header: true,
                child: const Text(
                  Strings.signInTitle,
                  style: TextStyle(
                    fontSize: 28,
                    fontWeight: FontWeight.w700,
                    height: 1.25,
                  ),
                ),
              ),
              const SizedBox(height: 8),
              Text(
                Strings.signInBody,
                style: TextStyle(
                  fontSize: 17,
                  height: 1.5,
                  color: colors.textSecondary,
                ),
              ),
              const SizedBox(height: 28),
              PrimaryButton(
                label: Strings.signIn,
                icon: Icons.lock,
                onPressed: busy ? null : onSignIn,
              ),
              const SizedBox(height: 12),
              const InfoLine(Strings.signInNeedsConnection),
              const SizedBox(height: 12),
              const InfoLine(Strings.signInHelp, icon: Icons.call),
            ],
          ),
        ),
      ],
    );
  }
}

/// The session ended while reports were waiting: show them, say they are safe, offer sign-in and the desk phone.
class _SessionExpired extends ConsumerWidget {
  const _SessionExpired({
    required this.name,
    required this.busy,
    required this.onSignIn,
  });

  final String? name;
  final bool busy;
  final VoidCallback onSignIn;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = context.colors;
    final pending =
        ref.watch(_waitingReportsProvider).value ?? const <QueuedReport>[];

    return Column(
      children: [
        AppHeader(name: name),
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(Space.md),
            children: [
              Row(
                spacing: 8,
                children: [
                  Icon(Icons.lock, size: 26, color: colors.textPrimary),
                  Expanded(
                    child: Semantics(
                      header: true,
                      child: Text(
                        Strings.sessionExpiredTitle,
                        style: context.text.headlineMedium,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              if (pending.isNotEmpty) ...[
                SectionCard(
                  title: Strings.waitingOnPhone(pending.length),
                  children: [
                    for (final report in pending)
                      Container(
                        padding: const EdgeInsets.symmetric(vertical: 10),
                        decoration: BoxDecoration(
                          border: Border(
                            top: BorderSide(color: colors.borderDefault),
                          ),
                        ),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                spacing: 4,
                                children: [
                                  Text(
                                    Strings.departedLabel(report.stationName),
                                    style: const TextStyle(
                                      fontSize: 16,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  const DeliveryChip(ReportState.pending),
                                ],
                              ),
                            ),
                            Text(
                              formatTime(report.occurredAt),
                              style: const TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
                const SizedBox(height: 12),
              ],
              const InfoLine(Strings.sessionExpiredNothingLost),
              const SizedBox(height: Space.lg),
              PrimaryButton(
                label: Strings.signInToSend,
                icon: Icons.lock,
                onPressed: busy ? null : onSignIn,
              ),
              const SizedBox(height: 10),
              const PhoneDeskButton(),
            ],
          ),
        ),
      ],
    );
  }
}

/// Reports waiting to send, whatever journey they belong to (the session ended, so no journey is on screen).
final _waitingReportsProvider = FutureProvider.autoDispose<List<QueuedReport>>(
  (ref) => ref.read(reportQueueProvider).pending(),
);
