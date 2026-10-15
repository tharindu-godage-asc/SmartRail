/// The outcome of one attempt to send a report. Decided by status and `reason` only (spec section 4).
sealed class SendResult {
  const SendResult();

  /// Maps an HTTP answer. 200 (repeat) and 201 (recorded) both mean Delivered (AC-E03-02 4).
  factory SendResult.fromResponse(int statusCode, {String? reason}) {
    if (statusCode == 200 || statusCode == 201) {
      return const Delivered();
    }
    if (statusCode == 401) {
      return const Unauthorized();
    }
    // The journey changed while the server was saving: asking again is the right answer, not a refusal.
    if (statusCode == 409 && reason == 'version_conflict') {
      return RetryLater(reason);
    }
    if (statusCode >= 500 || statusCode == 408 || statusCode == 429) {
      return RetryLater(reason ?? 'server_error');
    }
    return Rejected(statusCode: statusCode, reason: reason ?? 'rejected');
  }
}

final class Delivered extends SendResult {
  const Delivered();
}

/// Try again later with backoff. Never counts as a refusal.
final class RetryLater extends SendResult {
  const RetryLater([this.reason]);
  final String? reason;
}

/// The server said no for good. The report stays on the phone, marked Rejected.
final class Rejected extends SendResult {
  const Rejected({required this.statusCode, required this.reason});
  final int statusCode;
  final String reason;
}

/// The token was refused even after a refresh: the queue pauses until the operator signs in again (AC-E01-01 4).
final class Unauthorized extends SendResult {
  const Unauthorized();
}
