/**
 * Every word the app shows, in one place so Sinhala and Tamil can be added next to it (spec 08). The API sends codes; this file
 * words them. An unknown code is shown as it is, so a new type or status on the server never breaks a screen.
 */
import type { OverviewJourney } from './api/types'
import { formatMinutes } from './time'

export const t = {
  appName: 'SmartRail',
  operations: 'Operations',
  safetyNotice:
    'SmartRail is an information system, not a safety system. It does not control trains or detect collisions. Safety actions go through the railway’s signalling and control channels.',
  footer: 'For authorised SmartRail staff only. Actions on this dashboard are recorded with your name.',
  loading: 'Loading…',
  signingIn: 'Signing in…',

  signIn: {
    title: 'Sign in',
    body: 'Use your SmartRail staff account to open the controller dashboard.',
    button: 'Sign in with staff account',
    secure: 'You will go to the secure sign-in page. SmartRail never sees your password.',
    help: 'Need an account or a role change? Ask your System Administrator. Operators use the operator app, not this dashboard.',
    failed: 'Could not sign you in. Try again.',
  },
  noAccess: {
    title: 'This account cannot open the dashboard',
    signedInAs: 'You are signed in as',
    withoutRole: 'which does not have the controller role.',
    body: 'Operators report from the operator app. If you should have dashboard access, ask your System Administrator to check your role.',
    signOut: 'Sign out',
  },
  forbidden: {
    title: 'You do not have access',
    body: 'The server refused this request: your role no longer allows it. Sign out and sign in again, or ask your System Administrator.',
  },
  sessionEnded: {
    title: 'Your session has ended',
    body: 'Sign in again to continue.',
    button: 'Sign in again',
  },

  nav: { label: 'Main', line: 'Line', mainLine: 'Main Line', overview: 'Overview', exceptions: 'Exceptions', timetable: 'Timetable' },
  header: { connected: 'Connected', notUpToDate: 'Not up to date', signOut: 'Sign out', controller: 'Controller' },

  freshness: {
    updated: 'Updated',
    banner: 'Not up to date',
    lastGood: 'Last updated',
    neverLoaded: 'Could not load this yet. Trying again every 10 seconds.',
  },

  overview: {
    title: 'Overview',
    runningNow: 'Running now',
    openExceptions: 'Open exceptions',
    awaitingVerification: 'Awaiting verification',
    critical: (n: number) => `${n} critical`,
    oldest: (age: string) => `oldest ${age}`,
    running: 'Running journeys',
    noTrains: 'No trains running now',
    latest: 'Latest exceptions',
    viewAll: 'View all exceptions',
    columns: {
      train: 'Train',
      route: 'Route',
      nextStop: 'Next stop',
      nextTime: 'Next time',
      status: 'Status',
      lastReport: 'Last report',
      verification: 'Verification',
    },
    noNextStop: '—',
    noReportYet: 'No report yet',
    ago: (age: string) => `${age} ago`,
    verificationNone: '—',
    verificationAwaiting: 'Awaiting verification',
  },

  exceptions: {
    title: 'Exceptions',
    tabs: { label: 'Exceptions views', open: 'Open', closed: 'Closed' },
    open: 'Open exceptions',
    none: 'No open exceptions',
    columns: { severity: 'Severity', exception: 'Exception', where: 'Where', raised: 'Raised' },
    detail: 'detail',
    pick: 'Select an exception to see it and resolve it.',
    selectRow: (train: string, type: string) => `Select ${type} for train ${train}`,
    advice: 'Resolving records your decision. It does not change what passengers see and it does not control any train.',
    resolveWith: 'Resolve as',
    descriptions: {
      ReasonAwaitingVerification: (train: string, station: string | null) =>
        `A delay was reported for train ${train}${station ? `, last reported at ${station}` : ''}. The reason is waiting for verification. Showing and verifying the reason comes in a later release.`,
      EarlyDeparture: (train: string, station: string | null) =>
        `Train ${train} left ${station ?? 'a stop'} before its scheduled time.`,
    } as Record<string, (train: string, station: string | null) => string>,
  },

  closed: {
    title: 'Closed exceptions',
    none: 'No closed exceptions yet',
    status: (action: string) => `Closed · ${action}`,
    raised: 'Raised',
    closedAt: 'Closed',
    closedBy: 'Closed by',
    unnamedController: 'Controller',
    took: (age: string) => `${age} after it was raised`,
    showing: (shown: number, total: number) => `Showing the latest ${shown} of ${total}`,
  },

  actions: {
    Acknowledged: 'Acknowledge',
    NoActionNeeded: 'No action needed',
    Escalated: 'Escalate',
  } as Record<string, string>,
  actionsPast: {
    Acknowledged: 'Acknowledged',
    NoActionNeeded: 'No action needed',
    Escalated: 'Escalated',
  } as Record<string, string>,

  resolveDialog: {
    title: (train: string) => `Resolve exception for train ${train}`,
    body: (action: string) => `This records “${action}” with your name. It does not change what passengers see and it does not control any train.`,
    cancel: 'Cancel',
    confirm: 'Confirm',
    sending: 'Sending…',
    alreadyResolved: 'Already resolved by another controller.',
    notFound: 'This exception no longer exists.',
    failed: 'Could not resolve this exception. Try again.',
  },

  announce: {
    count: (n: number) => `Open exceptions: ${n}`,
    newCritical: (train: string) => `New critical exception for train ${train}`,
  },

  timetable: {
    title: 'Timetable',
    tabs: { label: 'Timetable sections', weekly: 'Weekly schedule', extra: 'Extra trains', cancelled: 'Cancelled' },
    newWeekly: 'New weekly service',
    addExtra: 'Add extra train',
    note: 'The weekly schedule is the base timetable. Journeys are generated from it 7 days ahead. An extra train is a one-off service for chosen dates. A service is never deleted.',
    cols: {
      service: 'Service',
      category: 'Category',
      route: 'Route',
      runsOn: 'Runs on',
      date: 'Date',
      departs: 'Departs',
      stops: 'Stops',
      version: 'Version',
      range: 'Dates',
      reason: 'Reason',
      cancelledBy: 'Cancelled by',
      cancelledAt: 'When',
    },
    categories: { Regional: 'Regional', LongDistance: 'Long-distance' } as Record<string, string>,
    types: { Slow: 'Slow', Express: 'Express' } as Record<string, string>,
    version: (n: number) => `Version ${n}`,
    amended: 'Amended',
    runsOnLabel: (days: string[]) => `Runs on ${days.join(', ')}`,
    empty: {
      weekly: 'There are no weekly services yet.',
      extra: 'There are no extra trains. An extra train is a one-off service for chosen dates.',
      cancelled: 'No service is cancelled for today or later.',
    },
    loadFailed: 'Could not load the timetable. Try again.',
    retry: 'Try again',
    loading: 'Loading…',
    rangeOne: (date: string) => date,
    rangeFrom: (date: string) => `${date} onward`,
    rangeBetween: (from: string, to: string) => `${from} to ${to}`,
    to: (from: string, to: string) => `${from} to ${to}`,
    back: 'Back to the timetable',
    detail: {
      stops: 'Stops',
      versions: 'Versions',
      cancellations: 'Cancellations',
      neverDeleted: 'A service is never deleted. A change creates a new version.',
      station: 'Station',
      arrives: 'Arrives',
      leaves: 'Leaves',
      platform: 'Platform',
      notSet: 'Not set',
      first: 'First stop',
      last: 'Last stop',
      from: 'From',
      by: 'By',
      noSummary: 'First version',
      extraOn: (date: string) => `Extra train on ${date}`,
      weeklyFrom: (date: string) => `Runs from ${date}`,
      journeysNote: 'Journeys of this service are generated 7 days ahead. They have no operator until one is assigned.',
      unknownService: 'This service was not found.',
    },
    wizard: {
      newTitle: 'New service',
      newExtraTitle: 'New extra train',
      stepper: 'Steps',
      steps: ['Details', 'Stops and times', 'Review and schedule'],
      next: 'Next',
      back: 'Back',
      nextReview: 'Next: review',
      cancel: 'Cancel',
      schedule: 'Schedule service',
      scheduling: 'Scheduling…',
      trainNumber: 'Train number',
      trainNumberHint: '1 to 10 letters or digits, for example 8072',
      category: 'Category',
      type: 'Type',
      direction: 'Direction',
      directionInbound: 'Toward Colombo Fort',
      directionOutbound: 'Away from Colombo Fort',
      firstDate: 'First date it runs',
      runsOn: 'Runs on',
      dates: 'Dates',
      addDate: 'Add date',
      removeDate: (date: string) => `Remove ${date}`,
      noDates: 'No date chosen yet.',
      days: { Mon: 'Monday', Tue: 'Tuesday', Wed: 'Wednesday', Thu: 'Thursday', Fri: 'Friday', Sat: 'Saturday', Sun: 'Sunday' } as Record<string, string>,
      everyStop: 'Every stop',
      everyStopHint: 'The train stops at every station on the line, in line order.',
      chooseStops: 'Choose stops',
      chooseStopsHint: 'Pick the stations, for example for an express. First and last stop are always included.',
      include: 'Include',
      station: 'Station',
      time: 'Time',
      timeHint: '24 hours, HH:mm. First stop: departure. Last stop: arrival. Other stops: arrival, the train leaves 1 minute later.',
      platform: 'Platform',
      notes: 'Notes',
      firstStop: 'First stop',
      lastStop: 'Last stop',
      doesNotStop: 'Train does not stop here',
      platformNotSet: 'Not set',
      needNumber: 'Enter the train number.',
      needDays: 'Choose at least one day.',
      needDate: 'Choose the first date.',
      needDates: 'Choose at least one date.',
      needTimes: 'Enter a time for every stop that is included.',
      needTwoStops: 'Include at least two stops.',
      reviewTitle: 'Review and schedule',
      checking: 'Checking the service…',
      checkFailed: 'Could not check the service. Nothing was saved. Try again.',
      errorsCount: (n: number) => (n === 1 ? '1 error. Fix it to schedule.' : `${n} errors. Fix them to schedule.`),
      adviceTitle: 'Advice',
      adviceHelp: 'Advice never stops a service from being scheduled. Read it, then confirm.',
      adviceConfirm: 'I have read the advice and want to schedule',
      noProblems: 'The server found no problems.',
      error: 'Error',
      advice: 'Advice',
      summary: 'Summary',
      summaryTrain: 'Train',
      summaryRuns: 'Runs',
      summaryStops: 'Stops',
      summaryDates: 'Dates',
      scheduleFailed: 'Could not schedule. Nothing was saved. Try again.',
      editStops: 'Edit stops and times',
      editDetails: 'Edit details',
      scheduled: (number: string, journeys: number) => `Service ${number} scheduled. ${journeys === 1 ? '1 journey' : `${journeys} journeys`} created.`,
      scheduledExtra: (count: number) => (count === 1 ? '1 extra train added.' : `${count} extra trains added.`),
    },
    reasons: {
      train_number_in_use: 'This train number already runs on these dates.',
      warnings_not_acknowledged: 'The advice changed. Read it again and confirm.',
      invalid_service: 'The service is not valid. See the errors.',
      line_not_found: 'The line was not found.',
      service_not_found: 'This service was not found.',
      invalid_tab: 'That tab does not exist.',
    },
    issues: {
      train_number_invalid: 'The train number is 1 to 10 letters or digits.',
      category_invalid: 'Choose Regional or Long-distance.',
      service_type_invalid: 'Choose Slow or Express.',
      days_required: 'Choose at least one day.',
      effective_from_required: 'Say the first date the service runs.',
      dates_required: 'An extra train needs at least one date.',
      date_in_past: 'The date is in the past.',
      stops_too_few: 'A service needs at least two stops.',
      station_not_on_line: 'This station is not on the line.',
      station_repeated: 'A station appears twice.',
      stops_not_in_line_order: 'The stops are not in line order.',
      first_stop_departure_only: 'The first stop has a departure only.',
      last_stop_arrival_only: 'The last stop has an arrival only.',
      stop_time_required: 'A time is missing.',
      time_invalid: 'A time must be written HH:mm.',
      times_not_increasing: 'Times must increase from stop to stop.',
      platform_not_at_station: 'This platform does not belong to the station.',
      starts_in_past: 'The first departure is not later than now.',
      platform_close: 'Another train uses this platform close to this time.',
    } as Record<string, string>,
  },
}

const typeLabels: Record<string, string> = {
  ReasonAwaitingVerification: 'Delay reason waiting for verification',
  EarlyDeparture: 'Departed before scheduled time',
}
export const exceptionTypeLabel = (code: string): string => typeLabels[code] ?? code

const severityLabels: Record<string, string> = { Critical: 'Critical', High: 'High' }
export const severityLabel = (code: string): string => severityLabels[code] ?? code

const eventLabels: Record<string, string> = { Departed: 'Departed', Arrived: 'Arrived', DelayReported: 'Delay reported' }
export const eventLabel = (code: string): string => eventLabels[code] ?? code

const sourceLabels: Record<string, string> = {
  OperatorReport: 'Operator report',
  ControllerEntry: 'Controller entry',
  Inference: 'Inferred',
  Override: 'Override',
}
export const sourceLabel = (code: string): string => sourceLabels[code] ?? code

/** The status words for a journey row. An unknown length of delay is said as such, never as a number (spec 06 R7). */
export function journeyStatusLabel(journey: Pick<OverviewJourney, 'statusKey' | 'delayKnown' | 'delayMinutes'>): string {
  if (journey.statusKey === 'Delayed') {
    return journey.delayKnown ? `Delayed ${formatMinutes(journey.delayMinutes)}` : 'Delay unknown'
  }

  const labels: Record<string, string> = { Scheduled: 'Scheduled', OnTime: 'On time', Completed: 'Completed' }
  return labels[journey.statusKey] ?? journey.statusKey
}
