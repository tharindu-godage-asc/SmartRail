import { useState } from 'react'
import { t } from '../../core/strings'
import { Button } from '../../core/ui'
import { useVerify } from './useVerify'

/** "Select several to verify" (spec 16 F2): turns on the tick boxes, then sends the ticked ones. Each is handled on its own by the API. */
export function BulkVerifyBar({
  selecting,
  ticked,
  onToggle,
  onDone,
}: {
  selecting: boolean
  ticked: string[]
  onToggle: () => void
  onDone: () => void
}) {
  const { many } = useVerify()
  const [message, setMessage] = useState<string | null>(null)

  return (
    <div className="flex flex-wrap items-center gap-sm">
      <Button
        onClick={() => {
          setMessage(null)
          onToggle()
        }}
      >
        {t.verify.selectMany}
      </Button>
      {selecting ? (
        <Button
          variant="primary"
          disabled={ticked.length === 0 || many.isPending}
          onClick={() =>
            many.mutate(ticked, {
              onSuccess: (done) => {
                const verified = done.results.filter((r) => r.status === 'verified').length
                setMessage(t.verify.bulkDone(verified, done.results.length - verified))
                onDone()
              },
              onError: () => setMessage(t.verify.failed),
            })
          }
        >
          {t.verify.verifySelected(ticked.length)}
        </Button>
      ) : null}
      {message ? (
        <span role="status" className="text-label font-semibold">
          {message}
        </span>
      ) : null}
    </div>
  )
}
