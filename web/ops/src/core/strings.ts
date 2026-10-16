/**
 * Every word the app shows, in one place so Sinhala and Tamil can be added next to it (spec 08). The API sends codes; this file
 * words them. An unknown code is shown as it is, so a new type or status on the server never breaks a screen.
 */
import type { OverviewJourney } from './api/types'

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

  nav: { label: 'Main', line: 'Line', mainLine: 'Main Line', overview: 'Overview', exceptions: 'Exceptions' },
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
    return journey.delayKnown ? `Delayed ${journey.delayMinutes} min` : 'Delay unknown'
  }

  const labels: Record<string, string> = { Scheduled: 'Scheduled', OnTime: 'On time', Completed: 'Completed' }
  return labels[journey.statusKey] ?? journey.statusKey
}
