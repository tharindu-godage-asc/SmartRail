import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/providers.dart';
import '../../core/l10n/strings.dart';
import '../../core/theme/app_theme.dart';
import '../../core/theme/tokens.g.dart';
import '../../core/widgets/widgets.dart';

/// Sign in and Create account, as drawn (boards 27 to 31 of the passenger canvas). They are screens only: there is no account service behind
/// them (spec 10 Q5), so the forms check what a person typed and a valid form says that accounts are not available in this version.
/// Nothing typed is stored or sent anywhere.
///
/// [showError] and [emailExists] draw the server-error states of the design from the debug-only gallery; no real flow reaches them.
class SignInScreen extends ConsumerStatefulWidget {
  const SignInScreen({this.showError = false, super.key});

  final bool showError;

  @override
  ConsumerState<SignInScreen> createState() => _SignInScreenState();
}

class _SignInScreenState extends ConsumerState<SignInScreen> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _hidden = true;
  bool _submitted = false;

  @override
  void initState() {
    super.initState();
    if (widget.showError) {
      _email.text = 'kasuni@example.com';
    }
  }

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  bool get _emailOk => _validEmail(_email.text);
  bool get _passwordOk => _password.text.isNotEmpty;

  void _submit(Strings s) {
    setState(() => _submitted = true);
    if (_emailOk && _passwordOk) {
      _notAvailable(context, s);
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = ref.watch(stringsProvider);
    final c = context.colors;
    final bannerError = widget.showError;
    final emailError = _submitted && !_emailOk || bannerError;
    final passwordError = _submitted && !_passwordOk || bannerError;
    return _AccountPage(
      children: [
        Text(s[K.signInTitle], style: context.text.headlineMedium),
        const SizedBox(height: 2),
        Text(
          s[K.signInSubtitle],
          style: context.text.bodyMedium?.copyWith(color: c.textSecondary),
        ),
        const SizedBox(height: Space.md),
        if (bannerError) ...[
          _ErrorBanner(title: s[K.signInWrong], body: s[K.signInWrongHelp]),
          const SizedBox(height: Space.md),
        ],
        _LabeledField(
          label: s[K.emailLabel],
          controller: _email,
          icon: Icons.mail_outline,
          keyboard: TextInputType.emailAddress,
          error: emailError,
          errorText: _submitted && !_emailOk ? s[K.emailInvalid] : null,
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: Space.md),
        _LabeledField(
          label: s[K.passwordLabel],
          controller: _password,
          icon: Icons.lock_outline,
          obscure: _hidden,
          error: passwordError,
          errorText: _submitted && !_passwordOk ? s[K.passwordMissing] : null,
          onChanged: (_) => setState(() {}),
          suffix: IconButton(
            tooltip: _hidden ? s[K.showPassword] : s[K.hidePassword],
            icon: Icon(
              _hidden
                  ? Icons.visibility_outlined
                  : Icons.visibility_off_outlined,
            ),
            onPressed: () => setState(() => _hidden = !_hidden),
          ),
        ),
        Align(
          alignment: Alignment.centerRight,
          child: TextButton(
            onPressed: () => _notAvailable(context, s),
            child: Text(
              s[K.forgotPassword],
              style: TextStyle(
                color: c.brandAccent,
                fontWeight: FontWeight.w700,
                decoration: TextDecoration.underline,
              ),
            ),
          ),
        ),
        PrimaryButton(label: s[K.signInButton], onPressed: () => _submit(s)),
        const SizedBox(height: Space.md),
        _OrDivider(text: s[K.newToSmartRail]),
        const SizedBox(height: Space.md),
        _OutlineAction(
          label: s[K.createAccountButton],
          onPressed: () => context.pushReplacement('/register'),
        ),
        const SizedBox(height: Space.md),
        _Hint(icon: Icons.info, text: s[K.signInHint]),
      ],
    );
  }
}

class CreateAccountScreen extends ConsumerStatefulWidget {
  const CreateAccountScreen({this.showErrors = false, super.key});

  /// The design's error state: an email that already exists and a password that is too short.
  final bool showErrors;

  @override
  ConsumerState<CreateAccountScreen> createState() =>
      _CreateAccountScreenState();
}

class _CreateAccountScreenState extends ConsumerState<CreateAccountScreen> {
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _hidden = true;
  bool _submitted = false;

  @override
  void initState() {
    super.initState();
    if (widget.showErrors) {
      _name.text = 'Kasuni Perera';
      _email.text = 'kasuni@example.com';
      _password.text = '••••';
      _submitted = true;
    }
  }

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  bool get _nameOk => _name.text.trim().isNotEmpty;
  bool get _emailOk => _validEmail(_email.text);
  bool get _passwordOk => _password.text.length >= 8;

  @override
  Widget build(BuildContext context) {
    final s = ref.watch(stringsProvider);
    final c = context.colors;
    final exists = widget.showErrors;
    final nameError = _submitted && !_nameOk;
    final emailInvalid = _submitted && !_emailOk && !exists;
    final passwordError = _submitted && !_passwordOk;
    return _AccountPage(
      children: [
        Text(s[K.createTitle], style: context.text.headlineMedium),
        const SizedBox(height: 2),
        Text(
          s[K.createSubtitle],
          style: context.text.bodyMedium?.copyWith(color: c.textSecondary),
        ),
        const SizedBox(height: Space.md),
        _LabeledField(
          label: s[K.nameLabel],
          controller: _name,
          icon: Icons.person_outline,
          error: nameError,
          errorText: nameError ? s[K.nameMissing] : null,
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: Space.md),
        _LabeledField(
          label: s[K.emailLabel],
          controller: _email,
          icon: Icons.mail_outline,
          keyboard: TextInputType.emailAddress,
          error: exists || emailInvalid,
          errorText: exists
              ? s[K.emailExists]
              : (emailInvalid ? s[K.emailInvalid] : null),
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: Space.md),
        _LabeledField(
          label: s[K.passwordLabel],
          controller: _password,
          icon: Icons.lock_outline,
          obscure: _hidden,
          error: passwordError,
          errorText: passwordError ? s[K.passwordShort] : null,
          helper: passwordError ? null : s[K.passwordHint],
          onChanged: (_) => setState(() {}),
          suffix: IconButton(
            tooltip: _hidden ? s[K.showPassword] : s[K.hidePassword],
            icon: Icon(
              _hidden
                  ? Icons.visibility_outlined
                  : Icons.visibility_off_outlined,
            ),
            onPressed: () => setState(() => _hidden = !_hidden),
          ),
        ),
        const SizedBox(height: Space.md),
        if (!widget.showErrors)
          Container(
            padding: const EdgeInsets.all(Space.sm + 4),
            decoration: BoxDecoration(
              color: c.bgPage,
              border: Border.all(color: c.borderDefault),
              borderRadius: BorderRadius.circular(Radii.md),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.verified_user, size: 22, color: c.brandAccent),
                const SizedBox(width: Space.sm),
                Expanded(
                  child: Text(
                    s[K.createPrivacy],
                    style: context.text.bodySmall,
                  ),
                ),
              ],
            ),
          ),
        if (!widget.showErrors) const SizedBox(height: Space.md),
        PrimaryButton(
          label: s[K.createAccountButton],
          onPressed: () {
            setState(() => _submitted = true);
            if (_nameOk && _emailOk && _passwordOk) {
              _notAvailable(context, s);
            }
          },
        ),
        const SizedBox(height: Space.md),
        Center(
          child: Wrap(
            alignment: WrapAlignment.center,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(s[K.haveAccount], style: context.text.bodyMedium),
              TextButton(
                onPressed: () => context.pushReplacement('/signin'),
                child: Text(
                  s[K.signInLink],
                  style: TextStyle(
                    color: c.brandAccent,
                    fontWeight: FontWeight.w700,
                    decoration: TextDecoration.underline,
                  ),
                ),
              ),
            ],
          ),
        ),
        if (!widget.showErrors)
          Padding(
            padding: const EdgeInsets.only(top: Space.sm),
            child: Text(
              s[K.createTerms],
              textAlign: TextAlign.center,
              style: context.text.bodySmall?.copyWith(color: c.textSecondary),
            ),
          ),
      ],
    );
  }
}

bool _validEmail(String v) {
  final t = v.trim();
  final at = t.indexOf('@');
  return at > 0 && t.indexOf('.', at) > at + 1 && !t.endsWith('.');
}

void _notAvailable(BuildContext context, Strings s) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(s[K.accountsUnavailable])));
}

/// The page frame of both screens: a close button, the three language links, then the form.
class _AccountPage extends ConsumerWidget {
  const _AccountPage({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final lang = ref.watch(languageProvider) ?? Lang.en;
    final c = context.colors;
    // The order of the design: Sinhala, Tamil, English.
    const order = [Lang.si, Lang.ta, Lang.en];
    return Scaffold(
      appBar: const InnerHeader(
        title: SizedBox.shrink(),
        leadingIcon: Icons.close,
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          Space.md,
          Space.sm,
          Space.md,
          Space.lg,
        ),
        children: [
          Align(
            alignment: Alignment.centerRight,
            child: Wrap(
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                for (final l in order) ...[
                  if (l != order.first)
                    Text(' · ', style: TextStyle(color: c.textSecondary)),
                  InkWell(
                    onTap: () => ref.read(languageProvider.notifier).choose(l),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        vertical: 12,
                        horizontal: 2,
                      ),
                      child: Semantics(
                        button: true,
                        selected: l == lang,
                        label: l.label,
                        child: Text(
                          l.label,
                          style: TextStyle(
                            fontSize: 13,
                            fontWeight: l == lang
                                ? FontWeight.w700
                                : FontWeight.w400,
                            color: l == lang ? c.textPrimary : c.textSecondary,
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
          ...children,
        ],
      ),
    );
  }
}

class _LabeledField extends StatelessWidget {
  const _LabeledField({
    required this.label,
    required this.controller,
    required this.icon,
    required this.onChanged,
    this.obscure = false,
    this.keyboard,
    this.error = false,
    this.errorText,
    this.helper,
    this.suffix,
  });

  final String label;
  final TextEditingController controller;
  final IconData icon;
  final ValueChanged<String> onChanged;
  final bool obscure;
  final TextInputType? keyboard;
  final bool error;
  final String? errorText;
  final String? helper;
  final Widget? suffix;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final red = c.statusDisruptedFg;
    OutlineInputBorder border(Color color, [double width = 1.5]) =>
        OutlineInputBorder(
          borderRadius: BorderRadius.circular(Radii.md),
          borderSide: BorderSide(color: color, width: width),
        );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: context.text.bodySmall?.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          obscureText: obscure,
          keyboardType: keyboard,
          autocorrect: false,
          enableSuggestions: false,
          onChanged: onChanged,
          decoration: InputDecoration(
            filled: true,
            fillColor: c.bgPage,
            prefixIcon: Icon(icon, color: error ? red : c.textPrimary),
            suffixIcon: suffix,
            border: border(error ? red : c.borderDefault),
            enabledBorder: border(error ? red : c.borderDefault),
            focusedBorder: border(error ? red : c.brandAccent, 2),
          ),
        ),
        if (errorText != null)
          Padding(
            padding: const EdgeInsets.only(top: 6),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.error, size: 16, color: red),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    errorText!,
                    style: context.text.bodySmall?.copyWith(
                      color: red,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
          )
        else if (helper != null)
          Padding(
            padding: const EdgeInsets.only(top: 6),
            child: Text(
              helper!,
              style: context.text.bodySmall?.copyWith(color: c.textSecondary),
            ),
          ),
      ],
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.all(Space.sm + 4),
        decoration: BoxDecoration(
          color: c.bgPage,
          border: Border.all(color: c.statusDisruptedFg, width: 1.5),
          borderRadius: BorderRadius.circular(Radii.md),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(Icons.error, size: 22, color: c.statusDisruptedFg),
            const SizedBox(width: Space.sm),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: context.text.bodyMedium?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  Text(body, style: context.text.bodySmall),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _OrDivider extends StatelessWidget {
  const _OrDivider({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Row(
      children: [
        Expanded(child: Divider(color: c.borderDefault)),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: Space.sm + 4),
          child: Text(
            text,
            style: context.text.bodySmall?.copyWith(color: c.textSecondary),
          ),
        ),
        Expanded(child: Divider(color: c.borderDefault)),
      ],
    );
  }
}

class _OutlineAction extends StatelessWidget {
  const _OutlineAction({required this.label, required this.onPressed});

  final String label;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return SizedBox(
      width: double.infinity,
      height: 52,
      child: OutlinedButton(
        onPressed: onPressed,
        style: OutlinedButton.styleFrom(
          foregroundColor: c.brandAccent,
          side: BorderSide(color: c.brandAccent, width: 1.5),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.md),
          ),
        ),
        child: Text(
          label,
          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
        ),
      ),
    );
  }
}

class _Hint extends StatelessWidget {
  const _Hint({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 18, color: c.textSecondary),
        const SizedBox(width: Space.sm),
        Expanded(
          child: Text(
            text,
            style: context.text.bodySmall?.copyWith(color: c.textSecondary),
          ),
        ),
      ],
    );
  }
}
