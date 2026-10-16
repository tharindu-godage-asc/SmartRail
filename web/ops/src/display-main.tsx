import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import DisplayApp from './modes/display/DisplayApp'
import './index.css'

// The entry of the display-only build (display.html). Unlike main.tsx it has no identity provider: a display never signs in.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={new QueryClient()}>
      <BrowserRouter>
        <DisplayApp />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
