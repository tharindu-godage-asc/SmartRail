import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import '../core/l10n/strings.dart';
import 'models.dart';

/// A train the passenger follows: a bookmark on this phone only (spec 10 rule 10).
class Follow {
  const Follow(this.trainNumber, this.fromCode, this.toCode, this.followedAt);

  factory Follow.fromJson(Map<String, dynamic> j) => Follow(
    j['n'] as String,
    j['f'] as String?,
    j['t'] as String?,
    DateTime.parse(j['at'] as String).toUtc(),
  );

  final String trainNumber;
  final String? fromCode;
  final String? toCode;
  final DateTime followedAt;

  Map<String, dynamic> toJson() => {
    'n': trainNumber,
    'f': fromCode,
    't': toCode,
    'at': followedAt.toIso8601String(),
  };
}

/// Everything the app keeps on the phone. Plain JSON in shared preferences: nothing sensitive, no account, no phone identifier
/// (spec 10 section 5). Wrapped so the rest of the app never touches the plugin.
class Preferences {
  Preferences(this._p);

  final SharedPreferences _p;

  static const _language = 'language';
  static const _onboarded = 'onboarded';
  static const _from = 'from';
  static const _recent = 'recent';
  static const _follows = 'follows';
  static const _stations = 'stations';
  static const _stationsVersion = 'stations_version';
  static const _snapshotPrefix = 'snapshot_';

  Lang? get language => Lang.fromCode(_p.getString(_language));
  Future<void> setLanguage(Lang l) => _p.setString(_language, l.code);

  bool get onboarded => _p.getBool(_onboarded) ?? false;
  Future<void> setOnboarded() => _p.setBool(_onboarded, true);

  /// The remembered origin (AC-E06-01 4): saved here, no location permission.
  String? get fromCode => _p.getString(_from);
  Future<void> setFromCode(String code) => _p.setString(_from, code);

  List<String> get recent => _p.getStringList(_recent) ?? const [];
  Future<void> addRecent(String code) => _p.setStringList(
    _recent,
    [code, ...recent.where((c) => c != code)].take(5).toList(),
  );

  List<Follow> get follows {
    final raw = _p.getString(_follows);
    if (raw == null) return const [];
    return (jsonDecode(raw) as List)
        .map((f) => Follow.fromJson((f as Map).cast<String, dynamic>()))
        .toList();
  }

  Future<void> setFollows(List<Follow> value) =>
      _p.setString(_follows, jsonEncode(value.map((f) => f.toJson()).toList()));

  /// The station list and its version, kept so the pickers work offline (spec 10 rule 11).
  ({String version, List<Station> stations})? get stationCache {
    final raw = _p.getString(_stations);
    final version = _p.getString(_stationsVersion);
    if (raw == null || version == null) return null;
    return (
      version: version,
      stations: (jsonDecode(raw) as List)
          .map((s) => Station.fromJson((s as Map).cast<String, dynamic>()))
          .toList(),
    );
  }

  Future<void> setStationCache(String version, List<Station> stations) async {
    await _p.setString(
      _stations,
      jsonEncode(stations.map((s) => s.toJson()).toList()),
    );
    await _p.setString(_stationsVersion, version);
  }

  /// The last status of a train with the server's time of it, for "last known" when the phone is offline.
  String? snapshot(String trainNumber) =>
      _p.getString('$_snapshotPrefix$trainNumber');
  Future<void> setSnapshot(String trainNumber, String json) =>
      _p.setString('$_snapshotPrefix$trainNumber', json);

  /// The last food menu of a train, kept so the list still opens offline (spec 11).
  String? menuSnapshot(String trainNumber) => _p.getString('menu_$trainNumber');
  Future<void> setMenuSnapshot(String trainNumber, String json) =>
      _p.setString('menu_$trainNumber', json);
}
