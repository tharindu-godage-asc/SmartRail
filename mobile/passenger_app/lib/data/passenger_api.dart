import 'package:dio/dio.dart';

import '../core/config.dart';
import 'menu.dart';
import 'models.dart';

/// The stations answer: the list and the version (also the ETag).
class StationsAnswer {
  const StationsAnswer(this.version, this.stations);

  final String version;
  final List<Station> stations;
}

/// What the app reads from the public API. No token, no account (FR-05). An interface so tests use fakes.
abstract interface class PassengerApi {
  /// `null` means "not modified" (304): the cached list is still current.
  Future<StationsAnswer?> stations({String? knownVersion});

  Future<TrainsAnswer> trains({
    required String from,
    required String to,
    String? date,
  });

  /// `null` when there is no such train or no journey today (404).
  Future<TrainStatus?> trainStatus(String trainNumber);

  Future<LineStatus> lineStatus();

  /// The food menu of a trip (spec 11). null when the train is unknown (404).
  Future<TrainMenu?> menu(String trainNumber, {String? from, String? to});
}

class DioPassengerApi implements PassengerApi {
  DioPassengerApi(AppConfig config, {Dio? dio})
    : _dio =
          dio ??
          Dio(
            BaseOptions(
              baseUrl: config.apiBaseUrl,
              connectTimeout: const Duration(seconds: 8),
              receiveTimeout: const Duration(seconds: 8),
            ),
          );

  final Dio _dio;

  @override
  Future<StationsAnswer?> stations({String? knownVersion}) async {
    final r = await _dio.get<Map<String, dynamic>>(
      '/api/v1/stations',
      options: Options(
        headers: knownVersion == null
            ? const <String, String>{}
            : <String, String>{'If-None-Match': '"$knownVersion"'},
        validateStatus: (s) => s == 200 || s == 304,
      ),
    );
    if (r.statusCode == 304) return null;
    final body = r.data!;
    return StationsAnswer(
      body['version'] as String,
      (body['stations'] as List)
          .map((s) => Station.fromJson((s as Map).cast<String, dynamic>()))
          .toList(),
    );
  }

  @override
  Future<TrainsAnswer> trains({
    required String from,
    required String to,
    String? date,
  }) async {
    final r = await _dio.get<Map<String, dynamic>>(
      '/api/v1/trains',
      queryParameters: {'from': from, 'to': to, 'date': ?date},
    );
    return TrainsAnswer.fromJson(r.data!);
  }

  @override
  Future<TrainStatus?> trainStatus(String trainNumber) async {
    final r = await _dio.get<Map<String, dynamic>>(
      '/api/v1/trains/${Uri.encodeComponent(trainNumber)}/status',
      options: Options(validateStatus: (s) => s == 200 || s == 404),
    );
    return r.statusCode == 404 ? null : TrainStatus.fromJson(r.data!);
  }

  @override
  Future<LineStatus> lineStatus() async {
    final r = await _dio.get<Map<String, dynamic>>('/api/v1/lines/MAIN/status');
    return LineStatus.fromJson(r.data!);
  }

  @override
  Future<TrainMenu?> menu(
    String trainNumber, {
    String? from,
    String? to,
  }) async {
    final r = await _dio.get<Map<String, dynamic>>(
      '/api/v1/trains/${Uri.encodeComponent(trainNumber)}/menu',
      queryParameters: {'from': ?from, 'to': ?to},
      options: Options(validateStatus: (s) => s == 200 || s == 404),
    );
    return r.statusCode == 404 ? null : TrainMenu.fromJson(r.data!);
  }
}
