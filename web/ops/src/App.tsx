import { Navigate, Route, Routes } from 'react-router'
import { AuthGate } from './features/auth/AuthGate'
import { ClosedExceptionsTab } from './features/exceptions/ClosedExceptionsTab'
import { ExceptionsPage } from './features/exceptions/ExceptionsPage'
import { OpenExceptionsTab } from './features/exceptions/OpenExceptionsTab'
import { OverviewPage } from './features/overview/OverviewPage'
import { CancelledTab } from './features/timetable/CancelledTab'
import { ExtraTab } from './features/timetable/ExtraTab'
import { ServiceDetailPage } from './features/timetable/ServiceDetailPage'
import { TimetablePage } from './features/timetable/TimetablePage'
import { WeeklyTab } from './features/timetable/WeeklyTab'
import { AmendServicePage, NewServicePage } from './features/timetable/wizard/NewServicePage'
import { MainBoard } from './modes/display/MainBoard'
import { PlatformDisplay } from './modes/display/PlatformDisplay'
import { OpsShell } from './modes/ops/OpsShell'

/**
 * Two modes of one app (D-23). The station displays (spec 09) are public and make no sign-in request, so their routes are outside the
 * gate. Everything else is the operations mode (spec 08) behind it.
 */
export default function App() {
  return (
    <Routes>
      <Route path="display/platform/:stationCode/:platform" element={<PlatformDisplay />} />
      <Route path="display/board/:stationCode" element={<MainBoard />} />
      <Route path="*" element={<AuthGate><OpsRoutes /></AuthGate>} />
    </Routes>
  )
}

/** The operations mode (spec 08). */
function OpsRoutes() {
  return (
      <Routes>
        <Route element={<OpsShell />}>
          <Route index element={<OverviewPage />} />
          <Route path="exceptions" element={<ExceptionsPage />}>
            <Route index element={<OpenExceptionsTab />} />
            <Route path="closed" element={<ClosedExceptionsTab />} />
          </Route>
          <Route path="timetable" element={<TimetablePage />}>
            <Route index element={<WeeklyTab />} />
            <Route path="extra" element={<ExtraTab />} />
            <Route path="cancelled" element={<CancelledTab />} />
          </Route>
          <Route path="timetable/new" element={<NewServicePage kind="Weekly" />} />
          <Route path="timetable/extra/new" element={<NewServicePage kind="Extra" />} />
          <Route path="timetable/:serviceId" element={<ServiceDetailPage />} />
          <Route path="timetable/:serviceId/amend" element={<AmendServicePage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
  )
}
