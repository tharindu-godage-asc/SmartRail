import type { ReactNode } from 'react'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'
import { Button, Footer, SafetyBar, Wordmark } from '../../core/ui'

/** The screens a person sees before they are in: sign in, no access, session ended (designs Dash-Login, Dash-NoAccess). */
function CenteredCard({ width, children }: { width: string; children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col bg-surface text-ink">
      <SafetyBar />
      <main className="flex grow items-center justify-center p-lg">
        <section className={`flex w-full flex-col gap-[18px] rounded-lg border border-line bg-page p-xl ${width}`}>
          <div className="flex items-center gap-[14px]">
            <Wordmark large />
            <span className="border-l border-line pl-[14px] text-label font-semibold text-muted">{t.operations}</span>
          </div>
          {children}
        </section>
      </main>
      <Footer />
    </div>
  )
}

export function LoadingScreen({ text }: { text: string }) {
  return (
    <CenteredCard width="max-w-[440px]">
      <p role="status" className="m-0 text-[15px]">{text}</p>
    </CenteredCard>
  )
}

export function SignInScreen({ error, onSignIn }: { error?: boolean; onSignIn: () => void }) {
  return (
    <CenteredCard width="max-w-[440px]">
      <div>
        <h1 className="m-0 text-[26px]">{t.signIn.title}</h1>
        <p className="mb-0 mt-sm text-[15px] leading-normal">{t.signIn.body}</p>
      </div>
      {error ? (
        <p role="alert" className="m-0 flex items-start gap-sm rounded-md border-[3px] border-warning bg-warning-bg p-[12px] text-label font-semibold text-warning">
          <Icon name="warning" size={22} />
          {t.signIn.failed}
        </p>
      ) : null}
      <Button variant="primary" className="w-full" onClick={onSignIn}>
        {t.signIn.button}
      </Button>
      <div className="flex items-start gap-[10px] text-label leading-snug text-muted">
        <Icon name="lock" size={20} />
        <span>{t.signIn.secure}</span>
      </div>
      <div className="border-t border-line pt-[14px] text-label leading-normal text-muted">{t.signIn.help}</div>
    </CenteredCard>
  )
}

export function NoAccessScreen({ username, onSignOut }: { username: string; onSignOut: () => void }) {
  return (
    <CenteredCard width="max-w-[520px]">
      <div className="flex items-start gap-[12px]">
        <Icon name="lock" size={32} />
        <div>
          <h1 className="m-0 text-heading">{t.noAccess.title}</h1>
          <p className="mb-0 mt-sm text-[15px] leading-normal">
            {t.noAccess.signedInAs} <b>{username}</b>, {t.noAccess.withoutRole}
          </p>
        </div>
      </div>
      <p className="m-0 text-label leading-normal text-muted">{t.noAccess.body}</p>
      <div className="flex gap-[10px]">
        <Button variant="primary" onClick={onSignOut}>
          {t.noAccess.signOut}
        </Button>
      </div>
    </CenteredCard>
  )
}

export function ForbiddenScreen({ onSignOut }: { onSignOut: () => void }) {
  return (
    <CenteredCard width="max-w-[520px]">
      <div className="flex items-start gap-[12px]">
        <Icon name="lock" size={32} />
        <div>
          <h1 className="m-0 text-heading">{t.forbidden.title}</h1>
          <p className="mb-0 mt-sm text-[15px] leading-normal">{t.forbidden.body}</p>
        </div>
      </div>
      <div className="flex gap-[10px]">
        <Button variant="primary" onClick={onSignOut}>
          {t.noAccess.signOut}
        </Button>
      </div>
    </CenteredCard>
  )
}

export function SessionEndedScreen({ onSignIn }: { onSignIn: () => void }) {
  return (
    <CenteredCard width="max-w-[440px]">
      <div className="flex items-start gap-[12px]">
        <Icon name="warning" size={32} />
        <div>
          <h1 className="m-0 text-heading">{t.sessionEnded.title}</h1>
          <p className="mb-0 mt-sm text-[15px] leading-normal">{t.sessionEnded.body}</p>
        </div>
      </div>
      <Button variant="primary" className="w-full" onClick={onSignIn}>
        {t.sessionEnded.button}
      </Button>
    </CenteredCard>
  )
}
