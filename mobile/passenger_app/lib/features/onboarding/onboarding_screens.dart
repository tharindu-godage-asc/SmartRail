import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/providers.dart';
import '../../core/l10n/strings.dart';
import '../../core/theme/app_theme.dart';
import '../../core/theme/tokens.g.dart';
import '../../core/widgets/widgets.dart';
import '../../domain/logic.dart';

/// First thing on launch: the name, then the language choice on the first run (AC-E06-04 5), the intro once, then Home.
class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _timer = Timer(const Duration(milliseconds: 900), () {
      if (!mounted) return;
      final lang = ref.read(languageProvider);
      final onboarded = ref.read(onboardedProvider);
      context.go(
        lang == null ? '/language' : (onboarded ? '/home' : '/onboard'),
      );
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final s = ref.watch(stringsProvider);
    return Scaffold(
      backgroundColor: c.brandAccent,
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(Space.xl),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                s[K.appName],
                style: TextStyle(
                  fontSize: 36,
                  fontWeight: FontWeight.w700,
                  color: c.brandOnAccent,
                ),
              ),
              const SizedBox(height: Space.md),
              Text(
                s[K.tagline],
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 18, color: c.brandOnAccent),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Three large choices. Saved on the phone; shown first on the first run and again from Settings (`?settings=1`).
class LanguageScreen extends ConsumerWidget {
  const LanguageScreen({this.fromSettings = false, super.key});

  final bool fromSettings;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    final current = ref.watch(languageProvider);
    final c = context.colors;
    return Scaffold(
      appBar: fromSettings
          ? InnerHeader(
              title: Text(s[K.language], style: context.text.labelLarge),
            )
          : null,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(Space.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (!fromSettings) const SizedBox(height: Space.xl),
              Text(s[K.chooseLanguage], style: context.text.headlineMedium),
              const SizedBox(height: Space.sm),
              Text(
                s[K.languageHint],
                style: context.text.bodyMedium?.copyWith(
                  color: c.textSecondary,
                ),
              ),
              const SizedBox(height: Space.lg),
              for (final lang in Lang.values) ...[
                _LanguageTile(
                  lang: lang,
                  selected: current == lang,
                  onTap: () async {
                    await ref.read(languageProvider.notifier).choose(lang);
                    if (!context.mounted) return;
                    if (fromSettings) {
                      context.pop();
                    } else {
                      context.go(
                        ref.read(onboardedProvider) ? '/home' : '/onboard',
                      );
                    }
                  },
                ),
                const SizedBox(height: Space.md),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _LanguageTile extends StatelessWidget {
  const _LanguageTile({
    required this.lang,
    required this.selected,
    required this.onTap,
  });

  final Lang lang;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Semantics(
      button: true,
      selected: selected,
      label: lang.label,
      child: InkWell(
        borderRadius: BorderRadius.circular(Radii.lg),
        onTap: onTap,
        child: Container(
          height: 64,
          padding: const EdgeInsets.symmetric(horizontal: Space.md),
          decoration: BoxDecoration(
            color: c.bgPage,
            borderRadius: BorderRadius.circular(Radii.lg),
            border: Border.all(
              color: selected ? c.brandAccent : c.borderDefault,
              width: selected ? 3 : 1,
            ),
          ),
          child: Row(
            children: [
              Expanded(
                child: ExcludeSemantics(
                  child: Text(
                    lang.label,
                    style: const TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ),
              if (selected) Icon(Icons.check_circle, color: c.brandAccent),
            ],
          ),
        ),
      ),
    );
  }
}

/// Three short pages. The text never promises notifications, which this version does not have (spec 10 Q2).
class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key});

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  final _controller = PageController();
  int _page = 0;

  Future<void> _finish() async {
    await ref.read(onboardedProvider.notifier).finish();
    if (mounted) context.go('/home');
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final s = ref.watch(stringsProvider);
    final c = context.colors;
    final pages = [
      (K.onboard1Title, K.onboard1Body, Icons.schedule),
      (K.onboard2Title, K.onboard2Body, Icons.help_outline),
      (K.onboard3Title, K.onboard3Body, Icons.bookmark_border),
    ];
    final last = _page == pages.length - 1;
    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(onPressed: _finish, child: Text(s[K.skip])),
            ),
            Expanded(
              child: PageView(
                controller: _controller,
                onPageChanged: (i) => setState(() => _page = i),
                children: [
                  for (final (title, body, icon) in pages)
                    Padding(
                      padding: const EdgeInsets.all(Space.xl),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(icon, size: 72, color: c.brandAccent),
                          const SizedBox(height: Space.lg),
                          Text(
                            s[title],
                            textAlign: TextAlign.center,
                            style: context.text.headlineMedium,
                          ),
                          const SizedBox(height: Space.md),
                          Text(
                            s[body],
                            textAlign: TextAlign.center,
                            style: context.text.bodyLarge,
                          ),
                          if (title == K.onboard2Title) ...[
                            const SizedBox(height: Space.lg),
                            Wrap(
                              spacing: Space.sm,
                              runSpacing: Space.sm,
                              alignment: WrapAlignment.center,
                              children: [
                                StatusChip(
                                  kind: StatusKind.onTime,
                                  label: s[K.legendOnTime],
                                ),
                                StatusChip(
                                  kind: StatusKind.delayed,
                                  label: s[K.legendDelayed],
                                ),
                                StatusChip(
                                  kind: StatusKind.waiting,
                                  label: s[K.legendWaiting],
                                ),
                              ],
                            ),
                          ],
                        ],
                      ),
                    ),
                ],
              ),
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                for (var i = 0; i < pages.length; i++)
                  Container(
                    margin: const EdgeInsets.all(4),
                    width: 10,
                    height: 10,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: i == _page ? c.brandAccent : c.borderDefault,
                    ),
                  ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.all(Space.lg),
              child: PrimaryButton(
                label: last ? s[K.getStarted] : s[K.next],
                onPressed: last
                    ? _finish
                    : () => _controller.nextPage(
                        duration: const Duration(milliseconds: 250),
                        curve: Curves.easeOut,
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
