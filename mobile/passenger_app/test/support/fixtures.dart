import 'package:passenger_app/data/models.dart';

/// Train 8058 as the API describes it: Veyangoda 08:20, Gampaha 08:30/08:31, Ragama 08:42/08:43, Maradana 08:50/08:51, Colombo Fort 08:55
/// (local time; the API sends UTC, which is five and a half hours earlier). `delay` moves every stop that has not been passed.
const _stops = [
  ('VYG', 'Veyangoda', null, '2026-10-16T02:50:00Z'),
  ('GPH', 'Gampaha', '2026-10-16T03:00:00Z', '2026-10-16T03:01:00Z'),
  ('RGM', 'Ragama', '2026-10-16T03:12:00Z', '2026-10-16T03:13:00Z'),
  ('MDA', 'Maradana', '2026-10-16T03:20:00Z', '2026-10-16T03:21:00Z'),
  ('CF', 'Colombo Fort', '2026-10-16T03:25:00Z', null),
];

String _plus(String? iso, int minutes) => iso == null
    ? ''
    : DateTime.parse(iso)
          .add(Duration(minutes: minutes))
          .toUtc()
          .toIso8601String();

/// The answer of `GET /api/v1/trains/8058/status`. The first [reported] stops are reported; with [departed] the last of them has left.
Map<String, dynamic> statusJson({
  int delay = 0,
  int reported = 0,
  bool departed = false,
  bool known = true,
  bool completed = false,
  String? key,
  String serverTime = '2026-10-16T03:12:00Z',
  String? lastEventAt,
}) {
  final stops = <Map<String, dynamic>>[];
  for (var i = 0; i < _stops.length; i++) {
    final (code, name, arr, dep) = _stops[i];
    final isReported = i < reported;
    final isLast = i == reported - 1;
    final left = isReported && (!isLast || departed);
    stops.add({
      'serviceStopId': 'stop-$i',
      'sequence': i + 1,
      'stationCode': code,
      'stationName': name,
      'scheduledArrival': arr,
      'scheduledDeparture': dep,
      'estimatedArrival': known && arr != null ? _plus(arr, delay) : null,
      'estimatedDeparture': known && dep != null ? _plus(dep, delay) : null,
      'actualDeparture': left ? _plus(dep ?? arr, delay) : null,
      'actualArrival': isReported && arr != null ? _plus(arr, delay) : null,
      'confirmation': isReported ? 'Reported' : 'Pending',
    });
  }
  return {
    'journeyId': 'j1',
    'trainNumber': '8058',
    'operatingDate': '2026-10-16',
    'version': 1,
    'serverTime': serverTime,
    'statusGroup': delay > 2 ? 'Warning' : 'Normal',
    'statusKey': completed
        ? 'Completed'
        : (key ?? (delay > 2 || !known ? 'Delayed' : 'OnTime')),
    'delayMinutes': delay,
    'delayKnown': known,
    'stops': stops,
    'lastKnownPosition': reported == 0
        ? null
        : {
            'stationCode': _stops[reported - 1].$1,
            'stationName': _stops[reported - 1].$2,
            'departedAt': departed ? '2026-10-16T02:58:00Z' : null,
            'state': departed ? 'Departed' : 'Arrived',
            'arrivedAt': departed ? null : '2026-10-16T02:58:00Z',
          },
    'lastEventAt': lastEventAt,
  };
}

TrainStatus status({
  int delay = 0,
  int reported = 0,
  bool departed = false,
  bool known = true,
  bool completed = false,
  String? key,
}) => TrainStatus.fromJson(
  statusJson(
    delay: delay,
    reported: reported,
    departed: departed,
    known: known,
    completed: completed,
    key: key,
  ),
);

Map<String, dynamic> _name(String en, String si, String ta) => {
  'en': en,
  'si': si,
  'ta': ta,
};

/// One train of `GET /api/v1/trains`, at Gampaha (arrival 08:30 plus the delay).
Map<String, dynamic> resultJson({
  String number = '8058',
  int delay = 0,
  bool live = true,
  String key = 'OnTime',
  String? platform = '1',
}) => {
  'trainNumber': number,
  'serviceType': 'Slow',
  'operatingDate': '2026-10-16',
  'destination': {
    'code': 'CF',
    'name': _name('Colombo Fort', 'කොළඹ කොටුව', 'கொழும்பு கோட்டை'),
  },
  'live': live,
  'journeyId': live ? 'j1' : null,
  'atFrom': {
    'arrival': {
      'scheduled': '2026-10-16T03:00:00Z',
      'estimated': live ? _plus('2026-10-16T03:00:00Z', delay) : null,
    },
    'departure': {
      'scheduled': '2026-10-16T03:01:00Z',
      'estimated': live ? _plus('2026-10-16T03:01:00Z', delay) : null,
    },
  },
  'atTo': {'arrival': null},
  'delayMinutes': live ? delay : null,
  'statusGroup': 'Normal',
  'statusKey': key,
  'platform': platform,
  'lastEventAt': null,
};

Map<String, dynamic> stationJson(
  String code,
  String en,
  String si,
  String ta,
) => {'code': code, 'name': _name(en, si, ta), 'lines': []};

/// The five stations in line order, with their three names.
final stationsBody = {
  'version': 'v1',
  'stations': [
    stationJson('CF', 'Colombo Fort', 'කොළඹ කොටුව', 'கொழும்பு கோட்டை'),
    stationJson('MDA', 'Maradana', 'මරදාන', 'மருதானை'),
    stationJson('RGM', 'Ragama', 'රාගම', 'ராகம'),
    stationJson('GPH', 'Gampaha', 'ගම්පහ', 'கம்பஹா'),
    stationJson('VYG', 'Veyangoda', 'වේයන්ගොඩ', 'வேயங்கொடை'),
  ],
};

/// `GET /api/v1/trains/8058/menu`: Colombo Fort as the destination with SmallBites items (Fruit cup sold out when [fruitSold]).
Map<String, dynamic> menuJson({
  bool fruitSold = false,
  bool stale = false,
  bool empty = false,
  String? orderUrl = 'http://localhost:8090/?station=CF&train=8058',
}) => {
  'serverTime': '2026-10-16T03:12:00Z',
  'trainNumber': '8058',
  'orderUrl': empty ? null : orderUrl,
  'staleAfterMinutes': 60,
  'stations': empty
      ? []
      : [
          {
            'code': 'CF',
            'names': {
              'en': 'Colombo Fort',
              'si': 'කොළඹ කොටුව',
              'ta': 'கொழும்பு கோட்டை',
            },
            'reason': 'Destination',
            'menuUpdatedAt': '2026-10-16T03:00:00Z',
            'fetchedAt': '2026-10-16T03:11:00Z',
            'stale': stale,
            'items': [
              {
                'itemId': 'egg-sandwich',
                'names': {
                  'en': 'Egg sandwich',
                  'si': 'බිත්තර සැන්ඩ්විච්',
                  'ta': 'முட்டை சாண்ட்விச்',
                },
                'priceLkr': 250.0,
                'available': true,
              },
              {
                'itemId': 'fruit-cup',
                'names': {'en': 'Fruit cup'},
                'priceLkr': 180.0,
                'available': !fruitSold,
              },
            ],
          },
        ],
};
