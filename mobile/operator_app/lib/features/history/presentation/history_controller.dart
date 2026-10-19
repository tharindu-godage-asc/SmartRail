import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/providers.dart';
import '../../journey/domain/journey_api.dart';
import '../domain/history.dart';

/// What the Previous journeys list shows (spec 12 rules 9 and 10).
class HistoryState {
  const HistoryState({
    this.journeys = const [],
    this.serverTime,
    this.next,
    this.loading = false,
    this.failed = false,
    this.fromCache = false,
  });

  final List<PastJourney> journeys;

  /// The server's time of the first page ("Last updated"); null before anything was loaded.
  final DateTime? serverTime;
  final DateTime? next;
  final bool loading;

  /// The last refresh did not work. With a copy on the phone it is shown with "Last updated"; without one the tab says it needs a
  /// connection.
  final bool failed;
  final bool fromCache;

  bool get hasData => serverTime != null;

  HistoryState copyWith({
    List<PastJourney>? journeys,
    DateTime? serverTime,
    DateTime? next,
    bool clearNext = false,
    bool? loading,
    bool? failed,
    bool? fromCache,
  }) => HistoryState(
    journeys: journeys ?? this.journeys,
    serverTime: serverTime ?? this.serverTime,
    next: clearNext ? null : next ?? this.next,
    loading: loading ?? this.loading,
    failed: failed ?? this.failed,
    fromCache: fromCache ?? this.fromCache,
  );
}

/// Loads the previous journeys when the segment is first opened, on pull to refresh and for "Show earlier". Never on a timer, one
/// request at a time (rule 10). The first page is kept on the phone, so the list still shows with no connection.
class HistoryController extends Notifier<HistoryState> {
  bool _busy = false;

  @override
  HistoryState build() {
    Future.microtask(_start);
    return const HistoryState(loading: true);
  }

  Future<void> _start() async {
    final cached = await ref.read(localCacheProvider).readHistoryJson();
    if (cached != null && !state.hasData) {
      try {
        final page = HistoryPage.fromJson(
          jsonDecode(cached) as Map<String, dynamic>,
        );
        state = _fromPage(page, fromCache: true).copyWith(loading: true);
      } on Object {
        // A copy from an older version of the app is not worth a crash.
      }
    }
    await refresh();
  }

  HistoryState _fromPage(HistoryPage page, {bool fromCache = false}) =>
      HistoryState(
        journeys: page.journeys,
        serverTime: page.serverTime,
        next: page.next,
        fromCache: fromCache,
      );

  Future<void> refresh() async {
    if (_busy) {
      return;
    }
    _busy = true;
    state = state.copyWith(loading: true, failed: false);
    try {
      final page = await ref.read(historyApiProvider).fetchHistory();
      await ref.read(localCacheProvider).writeHistoryJson(jsonEncode(page.raw));
      state = _fromPage(page);
    } on SessionExpiredException {
      state = state.copyWith(loading: false, failed: true);
    } on NetworkException {
      state = state.copyWith(loading: false, failed: true, fromCache: true);
    } on ServerException {
      state = state.copyWith(loading: false, failed: true, fromCache: true);
    } finally {
      _busy = false;
    }
  }

  /// "Show earlier": the next page, added below. It is not saved on the phone.
  Future<void> showEarlier() async {
    final before = state.next;
    if (_busy || before == null) {
      return;
    }
    _busy = true;
    state = state.copyWith(loading: true, failed: false);
    try {
      final page = await ref
          .read(historyApiProvider)
          .fetchHistory(before: before);
      state = state.copyWith(
        journeys: [...state.journeys, ...page.journeys],
        next: page.next,
        clearNext: page.next == null,
        loading: false,
      );
    } on Object {
      state = state.copyWith(loading: false, failed: true);
    } finally {
      _busy = false;
    }
  }
}

final historyControllerProvider =
    NotifierProvider<HistoryController, HistoryState>(HistoryController.new);

/// The record of one journey, fetched when it is opened and not kept (rule 9). Null means "not found"; an error means "no connection".
final journeyRecordProvider = FutureProvider.autoDispose
    .family<JourneyRecord?, String>(
      (ref, journeyId) => ref.read(historyApiProvider).fetchRecord(journeyId),
      // A failure is shown at once ("Connect to open this journey") with a Try again button, not retried in silence.
      retry: (_, _) => null,
    );
