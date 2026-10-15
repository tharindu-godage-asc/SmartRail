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
  static String nextDeparture(String station) => 'Next: $station departure';
  static String departedButton(String station) => 'Departed $station';
  static String departedLabel(String station) => 'Departed $station';
  static const noMoreReports = 'No more reports to send.';
  static String noMoreReportsDetail(String finalStop) =>
      'Reporting the arrival at $finalStop comes in a later release.';
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
  static const rejected = 'Rejected, requires Operations review';
  static const rejectedPhoneHelp =
      'Phone the desk so a controller can enter this report.';
  static const checkPhoneClock = 'Check your phone’s date and time.';

  // Delay (screens exist, sending does not)
  static const delayReasonTitle = 'What is the reason?';
  static const delayEstimateTitle = 'How late will the train be in total?';
  static const delayUnavailable =
      'Delay reports are not available yet. Phone the desk to report a delay.';
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
}
