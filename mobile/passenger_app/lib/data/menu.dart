DateTime _utc(Object? v) => DateTime.parse(v as String).toUtc();

Map<String, String> _names(Object? v) =>
    (v as Map).map((k, v) => MapEntry(k as String, v as String));

/// The name in the chosen language, English when the caterer sent none (FR-14, EC-184).
String localName(Map<String, String> names, String langCode) =>
    names[langCode] ?? names['en'] ?? names.values.firstOrNull ?? '';

class MenuItem {
  const MenuItem(this.id, this.names, this.priceLkr, this.available);

  factory MenuItem.fromJson(Map<String, dynamic> j) => MenuItem(
    j['itemId'] as String,
    _names(j['names']),
    (j['priceLkr'] as num).toDouble(),
    j['available'] as bool,
  );

  final String id;
  final Map<String, String> names;
  final double priceLkr;
  final bool available;
}

/// One station's menu in a trip: the destination, or a long stop on the way (FR-91).
class MenuStation {
  const MenuStation({
    required this.code,
    required this.names,
    required this.reason,
    required this.updatedAt,
    required this.stale,
    required this.items,
  });

  factory MenuStation.fromJson(Map<String, dynamic> j) => MenuStation(
    code: j['code'] as String,
    names: _names(j['names']),
    reason: j['reason'] as String,
    updatedAt: _utc(j['menuUpdatedAt']),
    stale: j['stale'] as bool,
    items: (j['items'] as List)
        .map((i) => MenuItem.fromJson((i as Map).cast<String, dynamic>()))
        .toList(),
  );

  final String code;
  final Map<String, String> names;
  final String reason;
  final DateTime updatedAt;
  final bool stale;
  final List<MenuItem> items;

  bool get isLongStop => reason == 'LongStop';
}

class TrainMenu {
  const TrainMenu(this.serverTime, this.orderUrl, this.stations, this.raw);

  factory TrainMenu.fromJson(Map<String, dynamic> j) => TrainMenu(
    _utc(j['serverTime']),
    j['orderUrl'] as String?,
    (j['stations'] as List)
        .map((s) => MenuStation.fromJson((s as Map).cast<String, dynamic>()))
        .toList(),
    j,
  );

  final DateTime serverTime;

  /// The caterer website with the station and the train, built by the server; null means no button (FR-124).
  final String? orderUrl;
  final List<MenuStation> stations;

  /// Kept to save the last answer for offline use.
  final Map<String, dynamic> raw;

  /// No station with a menu: the Food menu row is not shown (AC-E08-01 7).
  bool get isEmpty => stations.isEmpty;
}
