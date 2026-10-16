import { Navigate, Route, Routes } from 'react-router'
import { AuthGate } from './features/auth/AuthGate'
import { ClosedExceptionsTab } from './features/exceptions/ClosedExceptionsTab'
import { ExceptionsPage } from './features/exceptions/ExceptionsPage'
import { OpenExceptionsTab } from './features/exceptions/OpenExceptionsTab'
import { OverviewPage } from './features/overview/OverviewPage'
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
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
  )
}
