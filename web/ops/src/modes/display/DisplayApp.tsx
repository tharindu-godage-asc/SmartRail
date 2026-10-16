import { Route, Routes } from 'react-router'
import { d } from './displayStrings'
import { MainBoard } from './MainBoard'
import { Message } from './parts'
import { PlatformDisplay } from './PlatformDisplay'

/**
 * The whole app of the display-only build (spec 09 section 15): the two display routes and nothing else. It has no sign-in, no
 * operations screens and no identity provider code, so it is a separate service that can run, and fail, without the controller dashboard.
 * Any other address says the display is not set up.
 */
export default function DisplayApp() {
  return (
    <Routes>
      <Route path="display/platform/:stationCode/:platform" element={<PlatformDisplay />} />
      <Route path="display/board/:stationCode" element={<MainBoard />} />
      <Route path="*" element={<Message text={d.notSetUp} />} />
    </Routes>
  )
}
