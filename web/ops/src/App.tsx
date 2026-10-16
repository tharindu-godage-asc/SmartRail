import { Navigate, Route, Routes } from 'react-router'
import { AuthGate } from './features/auth/AuthGate'
import { ClosedExceptionsTab } from './features/exceptions/ClosedExceptionsTab'
import { ExceptionsPage } from './features/exceptions/ExceptionsPage'
import { OpenExceptionsTab } from './features/exceptions/OpenExceptionsTab'
import { OverviewPage } from './features/overview/OverviewPage'
import { OpsShell } from './modes/ops/OpsShell'

/** The operations mode (spec 08, D-23). A `display` mode would be a sibling of this one, with its own shell and routes. */
export default function App() {
  return (
    <AuthGate>
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
    </AuthGate>
  )
}
