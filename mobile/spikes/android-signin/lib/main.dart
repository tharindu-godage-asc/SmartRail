import 'package:flutter/material.dart';
import 'package:flutter_appauth/flutter_appauth.dart';

import 'auth_service.dart';

/// SPIKE UI (spec android-signin-spike): one throw-away screen. It shows subject, roles and expiry, never a token.
void main() => runApp(const SpikeApp());

class SpikeApp extends StatelessWidget {
  const SpikeApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'SmartRail sign-in spike',
        theme: ThemeData(colorSchemeSeed: Colors.indigo),
        home: const SpikeScreen(),
      );
}

class SpikeScreen extends StatefulWidget {
  const SpikeScreen({super.key});

  @override
  State<SpikeScreen> createState() => _SpikeScreenState();
}

class _SpikeScreenState extends State<SpikeScreen> {
  final _auth = AuthService();
  String _status = 'Signed out';
  String _claims = '';
  String _api = '';
  bool _busy = false;

  Future<void> _run(String working, Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _status = working;
    });
    try {
      await action();
    } on FlutterAppAuthUserCancelledException {
      _status = 'Sign-in cancelled';
      _claims = '';
    } catch (error) {
      // Plain error line, no stack trace, and never a token.
      _status = 'Error: ${error.runtimeType}';
    }
    if (mounted) {
      setState(() => _busy = false);
    }
  }

  void _showClaims() {
    final c = _auth.claimsSummary();
    _claims = c.isEmpty
        ? ''
        : 'subject: ${c['subject']}\nroles: ${c['roles']}\naudience: ${c['audience']}\nexpires: ${c['expires']}';
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Sign-in spike')),
        body: Padding(
          padding: const EdgeInsets.all(16),
          child: ListView(children: [
            FilledButton(
              onPressed: _busy
                  ? null
                  : () => _run('Signing in...', () async {
                        await _auth.signIn();
                        _status = 'Signed in';
                        _showClaims();
                      }),
              child: const Text('Sign in'),
            ),
            const SizedBox(height: 8),
            OutlinedButton(
              onPressed: _busy || !_auth.signedIn
                  ? null
                  : () => _run('Calling whoami...', () async {
                        _api = await _auth.callWhoAmI();
                        _status = 'whoami done';
                      }),
              child: const Text('Call whoami'),
            ),
            const SizedBox(height: 8),
            OutlinedButton(
              onPressed: _busy || !_auth.signedIn
                  ? null
                  : () => _run('Refreshing...', () async {
                        await _auth.refresh();
                        _status = 'Refreshed';
                        _showClaims();
                      }),
              child: const Text('Refresh'),
            ),
            const SizedBox(height: 8),
            TextButton(
              onPressed: _busy || !_auth.signedIn
                  ? null
                  : () => setState(() {
                        _auth.signOut();
                        _status = 'Signed out';
                        _claims = '';
                        _api = '';
                      }),
              child: const Text('Sign out (local)'),
            ),
            const Divider(height: 32),
            Text('Status: $_status', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            Text(_claims.isEmpty ? 'No claims' : _claims),
            const SizedBox(height: 12),
            Text(_api.isEmpty ? 'whoami: not called' : 'whoami: $_api'),
          ]),
        ),
      );
}
