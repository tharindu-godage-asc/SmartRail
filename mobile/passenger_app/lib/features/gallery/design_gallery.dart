import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/providers.dart';
import '../../core/l10n/strings.dart';
import '../../core/theme/app_theme.dart';
import '../../core/theme/tokens.g.dart';
import '../../core/widgets/widgets.dart';
import '../live/override_screens.dart';

/// Debug builds only (spec 10): the screens whose backend does not exist yet, drawn from fixture data so they can be seen and tested.
/// It is not in release builds: the route and the entry in Settings both check `kDebugMode`.
class DesignGallery extends ConsumerWidget {
  const DesignGallery({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(stringsProvider);
    return Scaffold(
      appBar: InnerHeader(
        title: Text(s[K.galleryTitle], style: context.text.labelLarge),
      ),
      body: ListView(
        padding: const EdgeInsets.all(Space.md),
        children: [
          Text(s[K.galleryHelp], style: context.text.bodyMedium),
          const SizedBox(height: Space.md),
          SectionCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                _Entry(
                  title: s[K.serviceSuspended],
                  kind: OverrideKind.suspended,
                ),
                Divider(height: 1, color: context.colors.borderDefault),
                _Entry(
                  title: s[K.trainCancelled],
                  kind: OverrideKind.cancelled,
                ),
                Divider(height: 1, color: context.colors.borderDefault),
                _Entry(
                  title: s.fmt(K.terminatesAtN, {'station': 'Ragama'}),
                  kind: OverrideKind.terminated,
                ),
                Divider(height: 1, color: context.colors.borderDefault),
                ListTile(
                  minTileHeight: 56,
                  leading: const Icon(Icons.error),
                  title: const Text('Sign in: wrong email or password'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => context.push('/gallery/signin-error'),
                ),
                Divider(height: 1, color: context.colors.borderDefault),
                ListTile(
                  minTileHeight: 56,
                  leading: const Icon(Icons.error),
                  title: const Text('Create account: errors'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => context.push('/gallery/register-errors'),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Entry extends StatelessWidget {
  const _Entry({required this.title, required this.kind});

  final String title;
  final OverrideKind kind;

  @override
  Widget build(BuildContext context) => ListTile(
    minTileHeight: 56,
    leading: const Icon(Icons.cancel),
    title: Text(title),
    trailing: const Icon(Icons.chevron_right),
    onTap: () => context.push('/gallery/${kind.name}'),
  );
}
