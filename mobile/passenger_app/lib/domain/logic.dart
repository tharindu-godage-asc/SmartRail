import '../core/config.dart';
import '../data/models.dart';
import '../data/preferences.dart';

/// Which of the fixed statuses a train shows (spec 10 rule 4). Always icon plus words, never colour alone (NFR-30).
enum StatusKind { onTime, delayed, scheduled, waiting, arrived }

StatusKind kindOfStatus(TrainStatus s) {
  if (s.completed) return StatusKind.arrived;
  if (!s.delayKnown) {
    return StatusKind.waiting; // D-39: no honest ETA, so the status says so
  }
  return switch (s.status) {
    'Delayed' => StatusKind.delayed,
    'OnTime' => StatusKind.onTime,
    _ => StatusKind.scheduled,
  };
}

/// A search result has no "delay unknown" (spec 04 D4): it is on time, delayed, or the timetable only.
StatusKind kindOfResult(TrainResult r) {
  if (!r.live) return StatusKind.scheduled;
  return switch (r.statusKey) {
    'Delayed' => StatusKind.delayed,
    'OnTime' => StatusKind.onTime,
    _ => StatusKind.scheduled,
  };
}

/// The one ETA a passenger is shown (D-38, FR-66, spec 10 rule 3). The app picks between times the server worked out; it calculates none.
class PrimaryEta {
  const PrimaryEta({
    required this.stop,
    required this.time,
    required this.atDestination,
  });

  final StopView stop;

  /// Null when there is no honest ETA (the delay length is unknown).
  final DateTime? time;

  /// False while the train has not yet left the boarding station; true after, when the destination is what matters.
  final bool atDestination;
}

PrimaryEta? primaryEta(TrainStatus s, {String? fromCode, String? toCode}) {
  if (s.stops.isEmpty || s.completed) return null;
  final first = s.stops.first;
  final from = s.stops.firstWhere(
    (p) => p.code == fromCode,
    orElse: () => first,
  );
  final to = s.stops.firstWhere(
    (p) => p.code == toCode,
    orElse: () => s.stops.last,
  );

  // The train has left the boarding station when it says so, or when a later stop has been reported or passed (a skipped report).
  final left =
      from.actualDeparture != null ||
      from.confirmation == 'Inferred' ||
      s.stops.any(
        (p) => p.sequence > from.sequence && p.confirmation != 'Pending',
      );
  final stop = left ? to : from;
  final time = s.delayKnown
      ? (stop.estimatedArrival ?? stop.estimatedDeparture)
      : null;
  return PrimaryEta(stop: stop, time: time, atDestination: left);
}

/// What a polled screen knows about its connection (spec 10 rule 7).
enum LinkStatus { live, offline, unavailable }

/// The result of polling: the last good answer and when failures began. The data is never cleared on an error.
class Polled<T> {
  const Polled({
    this.data,
    this.lastSuccess,
    this.failingSince,
    this.loaded = false,
  });

  final T? data;
  final DateTime? lastSuccess;
  final DateTime? failingSince;

  /// True once the first try finished, either way.
  final bool loaded;

  Polled<T> succeeded(T value, DateTime now) =>
      Polled(data: value, lastSuccess: now, loaded: true);

  Polled<T> failed(DateTime now) => Polled(
    data: data,
    lastSuccess: lastSuccess,
    failingSince: failingSince ?? now,
    loaded: true,
  );

  /// Offline: the phone has no connection (said at once) or polls have failed for 30 seconds with nothing else wrong. "Unavailable": the phone
  /// has a connection but the server does not answer for 30 seconds. Both keep the last data (NFR-03).
  LinkStatus link({required bool hasNetwork, required DateTime now}) {
    final failing = failingSince;
    if (failing == null) {
      return hasNetwork ? LinkStatus.live : LinkStatus.offline;
    }
    if (!hasNetwork) return LinkStatus.offline;
    return now.difference(failing) >= AppConfig.staleAfter
        ? LinkStatus.unavailable
        : LinkStatus.live;
  }
}

/// A follow is dropped when its journey is Completed or it is 24 hours old (spec 10 rule 10).
bool followExpired(Follow f, {required DateTime now, bool completed = false}) =>
    completed || now.difference(f.followedAt) >= AppConfig.followLifetime;

/// What a screen reader should hear after an update (NFR-32): only a change of meaning. An ETA moving by a minute says nothing.
enum Announcement { delayed, waiting, onTime, arrived, offline, back }

Announcement? announcementFor({
  required StatusKind? beforeKind,
  required StatusKind afterKind,
  required LinkStatus? beforeLink,
  required LinkStatus afterLink,
}) {
  if (beforeLink != null && beforeLink != afterLink) {
    return afterLink == LinkStatus.live
        ? Announcement.back
        : Announcement.offline;
  }
  if (beforeKind == null || beforeKind == afterKind) return null;
  return switch (afterKind) {
    StatusKind.delayed => Announcement.delayed,
    StatusKind.waiting => Announcement.waiting,
    StatusKind.onTime => Announcement.onTime,
    StatusKind.arrived => Announcement.arrived,
    StatusKind.scheduled => null,
  };
}
