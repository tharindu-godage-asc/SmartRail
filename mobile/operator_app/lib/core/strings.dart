/// Every word on screen, in one place, so Sinhala and Tamil can be added later (Q9: English only for now).
abstract final class Strings {
  static const appName = 'SmartRail';
  static const roleOperator = 'Operator';

  // Sign in
  static const signInTitle = 'Start your shift';
  static const signInBody =
      'Sign in with your SmartRail staff account to see your journey.';
  static const signIn = 'Sign in';
  static const signInNeedsConnection =
      'Signing in needs a connection. After that, reports can be saved without one.';
  static const signInHelp =
      'Staff accounts are set up by Operations. If you cannot sign in, phone the line desk.';
  static const signInInterruptedTitle = 'Sign-in was interrupted';
  static const signInInterruptedBody =
      'The phone closed the app while you were signing in. Please sign in again.';
  static const signInCancelledTitle = 'Sign-in cancelled';
  static const signInFailedTitle = 'Could not sign in';
  static const signInFailedBody =
      'Check your connection and try again. If it keeps failing, phone the line desk.';
  static const signInCancelledBody =
      'You closed the sign-in page. Sign in when you are ready.';

  // Session expired
  static const sessionExpiredTitle = 'Your session has expired';
  static const sessionExpiredNothingLost =
      'Nothing is lost. Sign in again and these reports send automatically.';
  static const signInToSend = 'Sign in to send';
  static String waitingOnPhone(int count) => 'Waiting on this phone · $count';

  // Journey
  static const noJourneyTitle = 'No journey assigned';
  static const noJourneyBody =
      'Ask Operations to assign you to a journey. You can’t report until then.';
  static const refresh = 'Refresh';
  static const phoneDesk = 'Phone the desk';
  static String trainTitle(String number, String from, String to) =>
      'Train $number · $from to $to';
  static String nextDeparture(String station) => 'Next: $station (departure)';
  static String nextArrival(String station) =>
      'Next: $station (arrival report required)';
  static String nextArrivalOrDeparture(String station) =>
      'Next: $station (arrival or departure)';
  static String departedButton(String station) => 'Departed $station';
  static String arrivedButton(String station) => 'Arrived $station';
  static String departedLabel(String station) => 'Departed $station';
  static String arrivedLabel(String station) => 'Arrived $station';
  static String delayLabel(String reason, String total) =>
      'Delay: $reason, $total';

  /// A length of time as the operator reads it: 60 minutes is "1 hour", everything else is "N min".
  static String minutesShort(int minutes) =>
      minutes == 60 ? '1 hour' : '$minutes min';
  static String minutesLong(int minutes) =>
      minutes == 60 ? '1 hour' : '$minutes minutes';
  static String delayTotalShort(int minutes) => '~${minutesShort(minutes)}';
  static const delayDurationUnknownShort = 'duration unknown';

  // Journey completed (AC-E03-03 4)
  static const completedTitle = 'Journey completed';
  static String completedBody(String station) => 'Reached $station at';
  static const yourReports = 'Your reports';
  static const countSent = 'Sent';
  static const countWaiting = 'Waiting to send';
  static const countRejected = 'Rejected';
  static const done = 'Done';
  static const reportDelay = 'Report delay';
  static const lastReport = 'Last report';
  static const journey = 'Journey';
  static const loadFailed = 'Could not load your journey.';
  static const loadFailedHelp = 'Check your connection and try again.';

  // Offline and delivery
  static const offlineTitle = 'You’re offline';
  static const offlineBody = 'Reports are saved and sent later.';
  static String lastUpdated(String time) => 'Last updated $time.';
  static const delivered = 'Delivered';
  static const notSentYet = 'Not sent yet';
  static const notSentDetail = 'Not sent yet. We’ll keep trying.';
  static const rejected = 'Rejected';
  static const rejectedPhoneHelp =
      'Phone the desk so a controller can enter this report.';
  static const checkPhoneClock = 'Check your phone’s date and time.';

  // Delay (AC-E03-04)
  static const delayReasonTitle = 'What is the reason?';
  static const delayEstimateTitle = 'How late will the train be in total?';
  static const delayTotalHelp =
      'Total delay against the timetable, not extra minutes. Options below your current delay are not offered.';
  static String runningLate(int minutes) => 'Running +$minutes min late now';
  static const runningLateUnknown = 'The length of the delay is unknown now';
  static String plusMinutes(int minutes) => '+${minutesShort(minutes)}';
  static const delaySentTitle = 'Delay reported';
  static const delaySavedTitle = 'Delay saved on this phone';
  static const delayRejectedTitle = 'Delay not accepted';
  static String delaySummary(String reason, String total) => '$reason · $total';
  static String aboutLate(int minutes) =>
      'about ${minutesLong(minutes)} late in total';
  static const durationUnknown = 'how late is not known yet';
  static String delaySentDetail(String shown) =>
      'Passengers now see $shown. The reason appears after Operations verifies it.';
  static String delayShownKnown(int minutes) =>
      'Delayed (~${minutesShort(minutes)})';
  static const delayShownUnknown = 'Delayed, duration unknown';
  static const delaySavedDetail =
      'It will send as soon as the connection returns. If it is urgent, phone your line desk.';
  static const delayRejectedDetail =
      'The server did not accept it, for example because the delay is not longer than the one already reported. Phone the desk if it still matters.';
  static const backToJourney = 'Back to journey';
  static String waitingReports(int count) => count == 1
      ? '1 report waiting to send'
      : '$count reports waiting to send';
  static const next = 'Next';
  static const back = 'Back';
  static const cancel = 'Cancel';
  static const sendReport = 'Send report';
  static String stepOfTwo(int step) => 'Step $step of 2';
  static const reasonSignal = 'Signal issue';
  static const reasonTechnical = 'Technical fault';
  static const reasonObstruction = 'Obstruction';
  static const reasonWeather = 'Weather or natural hazard';
  static const reasonUnknown = 'Unknown';

  // Help and sign out
  static const helpTitle = 'Help: can’t send reports?';
  static const helpBody =
      'Phone the operations desk for your line. The controller enters your report for you.';
  static const deskName = 'Operations desk';
  static const deskHours = '24 hours';
  static const callDesk = 'Call the desk';
  static const helpNoDoubleCount =
      'Your own report will link to the controller’s entry when it sends. It will not be counted twice.';
  static const account = 'Account';
  static const signOut = 'Sign out';
  static const signOutTitle = 'Sign out?';
  static const stayIn = 'Stay signed in';
  static String signOutWaiting(int count) => count == 1
      ? '1 report is still waiting to send from this phone.'
      : '$count reports are still waiting to send from this phone.';
  static const signOutKept =
      'They stay on this phone and send automatically the next time you sign in. If it is urgent, phone the line desk first.';
  static const signOutNothingWaiting = 'No reports are waiting on this phone.';

  // Navigation
  static const tabJourney = 'Journey';
  static const tabHelp = 'Help';
  static const tabReports = 'Reports';

  // Reports tab (spec 12)
  static const reportsTitle = 'Reports';
  static const segmentMyReports = 'My reports';
  static const segmentPrevious = 'Previous journeys';
  static const noPreviousJourneys = 'No previous journeys yet.';
  static const previousNeedsConnection =
      'Connect to see your previous journeys.';
  static const previousLoadFailed =
      'Could not refresh. Pull down or try again.';
  static const showEarlier = 'Show earlier';
  static const tryAgain = 'Try again';
  static const statusCompleted = 'Completed';
  static const statusNotFinished = 'Not finished';
  static String arrivedLate(int minutes) => 'arrived +$minutes min late';
  static const journeyRecordTitle = 'Journey record';
  static const recordNeedsConnection =
      'Connect to open this journey. It is not kept on this phone.';
  static const recordNotFound = 'This journey was not found.';
  static const recordReadOnly =
      'This record cannot be changed. It shows what the server recorded.';
  static const stopsTitle = 'Stops';
  static const stopReported = 'Reported';
  static const stopPassedInferred = 'Passed (not reported)';
  static const stopNotReached = 'Not reached';
  static const timeArrival = 'Arr';
  static const timeDeparture = 'Dep';
  static const timesLegend = 'Actual time, then the timetable in brackets.';
  static const noReportsInRecord = 'You made no reports on this journey.';
  static const reportsTapHint =
      'Tap a journey to show or hide its reports. Tap a report for details.';
  static String trainNumber(String number) => 'Train $number';
  static String reportCount(int count) =>
      count == 1 ? '1 report' : '$count reports';
  static String waitingCount(int count) => '$count waiting';
  static const noReportsYet = 'No reports on this phone yet.';
  static const reportDetailsTitle = 'Report details';
  static const backToReports = 'Back to reports';
  static const notAccepted = 'Not accepted';
  static const notAcceptedTitle = 'Not accepted';
  static const rejectedDetailBody =
      'The server did not accept this report because it conflicts with what is already recorded.';
  static const rejectedDetailAdvice =
      'You do not need to send it again. Operations is not told about it automatically: phone the Main Line desk if the train’s status is wrong.';
  static const deliveredTitle = 'Delivered';
  static const deliveredDetail = 'The server has this report.';
  static const reportMissingTitle = 'Report not found';
  static const reportMissingBody =
      'This report is no longer on this phone. Delivered reports are removed after 7 days.';
}
