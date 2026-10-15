import 'models.dart';

/// The two reads the journey screen needs. Everything that can go wrong is one of the exceptions below.
abstract interface class JourneyApi {
  /// The journey the signed-in operator is assigned to now; null when there is none (404).
  Future<OperatorJourney?> fetchAssignedJourney();

  /// The public status of the train: which stops have departed, the delay and the server's clock. Null when none (404).
  Future<TrainStatus?> fetchTrainStatus(String trainNumber);
}

/// The session is over: the refresh token was refused or the server kept answering 401. Ask the operator to sign in.
class SessionExpiredException implements Exception {
  const SessionExpiredException();
}

/// No answer from the server (offline, timeout, or a token that could not be checked offline).
class NetworkException implements Exception {
  const NetworkException([this.cause]);
  final Object? cause;
}

/// The server answered, but not with what the app can use (5xx, or an unexpected status).
class ServerException implements Exception {
  const ServerException(this.statusCode);
  final int statusCode;
}
