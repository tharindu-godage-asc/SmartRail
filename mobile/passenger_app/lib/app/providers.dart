import 'dart:convert';

import 'package:clock/clock.dart';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/config.dart';
import '../core/l10n/strings.dart';
import '../core/time.dart';
import '../data/models.dart';
import '../data/passenger_api.dart';
import '../data/preferences.dart';
import '../domain/logic.dart';
import 'polling.dart';

final appConfigProvider = Provider<AppConfig>((ref) => const AppConfig());

/// Set at start-up with the real shared preferences (tests set their own).
final preferencesProvider = Provider<Preferences>(
  (ref) => throw UnimplementedError('preferencesProvider must be overridden'),
);

final apiProvider = Provider<PassengerApi>(
  (ref) => DioPassengerApi(ref.watch(appConfigProvider)),
);

/// Whether the phone has any connection at all. Offline is said at once; "unavailable" needs 30 seconds of failures (rule 7).
final hasNetworkProvider = StreamProvider<bool>((ref) async* {
  final c = Connectivity();
  bool any(List<ConnectivityResult> r) =>
      r.any((x) => x != ConnectivityResult.none);
  yield any(await c.checkConnectivity());
  yield* c.onConnectivityChanged.map(any);
});

bool hasNetwork(WidgetRef ref) => ref.watch(hasNetworkProvider).value ?? true;

// ---- language, first run ----

class LanguageController extends Notifier<Lang?> {
  @override
  Lang? build() => ref.read(preferencesProvider).language;

  Future<void> choose(Lang lang) async {
    await ref.read(preferencesProvider).setLanguage(lang);
    state = lang;
  }
}

final languageProvider = NotifierProvider<LanguageController, Lang?>(
  LanguageController.new,
);

/// The words of the chosen language (English until one is chosen).
final stringsProvider = Provider<Strings>(
  (ref) => Strings(ref.watch(languageProvider) ?? Lang.en),
);

class OnboardedController extends Notifier<bool> {
  @override
  bool build() => ref.read(preferencesProvider).onboarded;

  Future<void> finish() async {
    await ref.read(preferencesProvider).setOnboarded();
    state = true;
  }
}

final onboardedProvider = NotifierProvider<OnboardedController, bool>(
  OnboardedController.new,
);

// ---- stations (cached with their version, so the pickers work offline: rule 11) ----

class StationsUnavailable implements Exception {}

final stationsProvider = FutureProvider<List<Station>>((ref) async {
  final prefs = ref.read(preferencesProvider);
  final cache = prefs.stationCache;
  try {
    final fresh = await ref
        .read(apiProvider)
        .stations(knownVersion: cache?.version);
    if (fresh == null && cache != null) {
      return cache.stations; // 304: still current
    }
    if (fresh != null) {
      await prefs.setStationCache(fresh.version, fresh.stations);
      return fresh.stations;
    }
  } catch (_) {
    // fall through to the cache
  }
  if (cache != null) return cache.stations;
  throw StationsUnavailable(); // first launch, no network, nothing cached (G4)
});

// ---- search selection ----

class Selection {
  const Selection({this.from, this.to});

  final String? from;
  final String? to;

  bool get ready => from != null && to != null && from != to;
}

class SelectionController extends Notifier<Selection> {
  @override
  Selection build() => Selection(from: ref.read(preferencesProvider).fromCode);

  Future<void> setFrom(String code) async {
    await ref.read(preferencesProvider).setFromCode(code);
    await ref.read(preferencesProvider).addRecent(code);
    state = Selection(from: code, to: state.to == code ? null : state.to);
  }

  Future<void> setTo(String code) async {
    await ref.read(preferencesProvider).addRecent(code);
    state = Selection(from: state.from, to: code);
  }
}

final selectionProvider = NotifierProvider<SelectionController, Selection>(
  SelectionController.new,
);

final recentProvider = Provider<List<String>>((ref) {
  ref.watch(selectionProvider); // changes whenever a station is picked
  return ref.read(preferencesProvider).recent;
});

// ---- search results (refreshed every 30 s while open) ----

class ResultsData {
  const ResultsData(this.today, this.firstTomorrow);

  final TrainsAnswer today;

  /// Filled only when nothing is left today (rule: "No more trains today" with the first train tomorrow).
  final TrainResult? firstTomorrow;
}

class ResultsPoller extends PollingNotifier<ResultsData> {
  ResultsPoller(this.route);

  final (String, String) route;

  @override
  Duration get interval => AppConfig.listInterval;

  @override
  DateTime? serverTimeOf(ResultsData value) => value.today.serverTime;

  @override
  Future<ResultsData> fetch() async {
    final api = ref.read(apiProvider);
    final today = await api.trains(from: route.$1, to: route.$2);
    if (today.trains.isNotEmpty) return ResultsData(today, null);
    final tomorrow = inColombo(clock.now()).add(const Duration(days: 1));
    final date =
        '${tomorrow.year.toString().padLeft(4, '0')}-${tomorrow.month.toString().padLeft(2, '0')}-${tomorrow.day.toString().padLeft(2, '0')}';
    try {
      final next = await api.trains(from: route.$1, to: route.$2, date: date);
      return ResultsData(today, next.trains.isEmpty ? null : next.trains.first);
    } catch (_) {
      return ResultsData(today, null);
    }
  }
}

final resultsProvider = NotifierProvider.autoDispose
    .family<ResultsPoller, Polled<ResultsData>, (String, String)>(
      ResultsPoller.new,
    );

// ---- one train's live status (every 10 s while open, last snapshot kept offline) ----

class TrainStatusPoller extends PollingNotifier<TrainStatus?> {
  TrainStatusPoller(this.number);

  final String number;

  @override
  Duration get interval => AppConfig.liveInterval;

  @override
  Polled<TrainStatus?> initial() {
    final raw = ref.read(preferencesProvider).snapshot(number);
    if (raw == null) return const Polled();
    try {
      return Polled(
        data: TrainStatus.fromJson(
          (jsonDecode(raw) as Map).cast<String, dynamic>(),
        ),
      );
    } catch (_) {
      return const Polled();
    }
  }

  @override
  DateTime? serverTimeOf(TrainStatus? value) => value?.serverTime;

  @override
  Future<TrainStatus?> fetch() => ref.read(apiProvider).trainStatus(number);

  @override
  void onSuccess(TrainStatus? value) {
    if (value != null) {
      ref.read(preferencesProvider).setSnapshot(number, jsonEncode(value.raw));
    }
  }
}

final trainStatusProvider = NotifierProvider.autoDispose
    .family<TrainStatusPoller, Polled<TrainStatus?>, String>(
      TrainStatusPoller.new,
    );

// ---- the line banner on Home ----

class LineStatusPoller extends PollingNotifier<LineStatus> {
  @override
  Duration get interval => AppConfig.listInterval;

  @override
  DateTime? serverTimeOf(LineStatus value) => value.serverTime;

  @override
  Future<LineStatus> fetch() => ref.read(apiProvider).lineStatus();
}

final lineStatusProvider =
    NotifierProvider.autoDispose<LineStatusPoller, Polled<LineStatus>>(
      LineStatusPoller.new,
    );

// ---- followed trains (a bookmark on this phone) ----

class FollowsController extends Notifier<List<Follow>> {
  @override
  List<Follow> build() {
    final prefs = ref.read(preferencesProvider);
    final kept = prefs.follows
        .where((f) => !followExpired(f, now: clock.now()))
        .toList();
    if (kept.length != prefs.follows.length) prefs.setFollows(kept);
    return kept;
  }

  bool isFollowing(String number) => state.any((f) => f.trainNumber == number);

  Future<void> follow(String number, {String? from, String? to}) async {
    if (isFollowing(number)) return;
    state = [Follow(number, from, to, clock.now().toUtc()), ...state];
    await ref.read(preferencesProvider).setFollows(state);
  }

  Future<void> unfollow(String number) async {
    state = state.where((f) => f.trainNumber != number).toList();
    await ref.read(preferencesProvider).setFollows(state);
  }
}

final followsProvider = NotifierProvider<FollowsController, List<Follow>>(
  FollowsController.new,
);
