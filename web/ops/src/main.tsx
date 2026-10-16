import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AuthProvider } from 'react-oidc-context'
import { BrowserRouter } from 'react-router'
import App from './App'
import { createOidcSettings, onSigninCallback } from './features/auth/oidc'
import './index.css'

const queryClient = new QueryClient()
const oidc = { ...createOidcSettings(), onSigninCallback }

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider {...oidc}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </QueryClientProvider>
    </AuthProvider>
  </StrictMode>,
)
