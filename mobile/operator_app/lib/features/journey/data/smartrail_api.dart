import 'package:dio/dio.dart';

import '../../auth/domain/auth.dart';
import '../../auth/domain/auth_repository.dart';
import '../../reports/domain/report.dart';
import '../../reports/domain/report_queue.dart';
import '../../reports/domain/send_result.dart';
import '../../history/domain/history.dart';
import '../domain/journey_api.dart';
import '../domain/models.dart';

/// The only place that talks to the SmartRail API. Adds the access token, refreshes once on a 401 and then gives up
/// with [SessionExpiredException] (AC-E01-01 3, AC-E01-03 1).
class SmartRailApi implements JourneyApi, ReportApi, HistoryApi {
  SmartRailApi(this._dio, this._auth);

  final Dio _dio;
  final AuthRepository _auth;

  @override
  Future<OperatorJourney?> fetchAssignedJourney() async {
    final response = await _call(
      (token) => _get('/api/v1/operator/journey', token),
    );
    if (response.statusCode == 404) {
      return null;
    }
    _ensureOk(response);
    return OperatorJourney.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<HistoryPage> fetchHistory({DateTime? before}) async {
    final query = before == null
        ? ''
        : '?before=${Uri.encodeQueryComponent(before.toUtc().toIso8601String())}';
    final response = await _call(
      (token) => _get('/api/v1/operator/journeys/history$query', token),
    );
    _ensureOk(response);
    return HistoryPage.fromJson(response.data! as Map<String, dynamic>);
  }

  @override
  Future<JourneyRecord?> fetchRecord(String journeyId) async {
    final response = await _call(
      (token) => _get('/api/v1/operator/journeys/$journeyId/record', token),
    );
    if (response.statusCode == 404) {
      return null;
    }
    _ensureOk(response);
    return JourneyRecord.fromJson(response.data! as Map<String, dynamic>);
  }

  @override
  Future<TrainStatus?> fetchTrainStatus(String trainNumber) async {
    // Public read: the token is not needed, so a lapsed session does not stop the screen from showing progress.
    try {
      final response = await _dio.get<Object>(
        '/api/v1/trains/${Uri.encodeComponent(trainNumber)}/status',
        options: Options(validateStatus: (_) => true),
      );
      if (response.statusCode == 404) {
        return null;
      }
      _ensureOk(response);
      return TrainStatus.fromJson(response.data! as Map<String, dynamic>);
    } on DioException catch (error) {
      throw NetworkException(error);
    }
  }

  @override
  Future<SendResult> send(QueuedReport report) async {
    final Response<Object> response;
    try {
      response = await _call(
        (token) => _dio.post<Object>(
          '/api/v1/journeys/${report.journeyId}/events',
          data: requestBody(report),
          options: _options(token),
        ),
      );
    } on SessionExpiredException {
      return const Unauthorized();
    } on NetworkException {
      return const RetryLater('network');
    }

    final body = response.data;
    final reason = body is Map<String, dynamic>
        ? body['reason'] as String?
        : null;
    return SendResult.fromResponse(response.statusCode ?? 0, reason: reason);
  }

  /// The JSON the server expects for each kind of report (spec 06, section 4). A delay report has no stop and carries
  /// exactly one of a total or "unknown"; the other kinds carry a stop and nothing else.
  static Map<String, Object?> requestBody(QueuedReport report) => {
    'clientEventId': report.clientEventId,
    'eventType': switch (report.kind) {
      ReportKind.departed => 'Departed',
      ReportKind.arrived => 'Arrived',
      ReportKind.delay => 'DelayReported',
    },
    if (!report.isDelay) 'serviceStopId': report.serviceStopId,
    'occurredAt': report.occurredAt.toUtc().toIso8601String(),
    if (report.isDelay)
      'reason': report.delayReason?.code ?? DelayReason.unknown.code,
    if (report.isDelay && report.totalDelayMinutes != null)
      'totalDelayMinutes': report.totalDelayMinutes,
    if (report.isDelayUnknown) 'delayUnknown': true,
  };

  Future<Response<Object>> _get(String path, String token) =>
      _dio.get<Object>(path, options: _options(token));

  Options _options(String token) => Options(
    headers: {'Authorization': 'Bearer $token'},
    validateStatus: (_) => true,
  );

  /// Sends with a token, and once more with a fresh one when the first answer is 401.
  Future<Response<Object>> _call(
    Future<Response<Object>> Function(String token) send,
  ) async {
    try {
      final token = await _auth.accessToken();
      if (token == null) {
        throw const SessionExpiredException();
      }

      final first = await send(token);
      if (first.statusCode != 401) {
        return first;
      }

      final fresh = await _auth.refreshNow();
      if (fresh == null) {
        throw const SessionExpiredException();
      }
      final second = await send(fresh);
      if (second.statusCode == 401) {
        throw const SessionExpiredException();
      }
      return second;
    } on RefreshRejected {
      throw const SessionExpiredException();
    } on RefreshUnavailable {
      throw const NetworkException();
    } on DioException catch (error) {
      throw NetworkException(error);
    }
  }

  void _ensureOk(Response<Object> response) {
    final status = response.statusCode ?? 0;
    if (status < 200 || status >= 300) {
      throw ServerException(status);
    }
  }
}
