import type { Api } from '../../core/api/client'
import type { CancellationView, LineOptions, ServiceDetail, ServiceRow, ValidationResult } from '../../core/api/types'
import { vi } from 'vitest'

/** The five stations of the Main Line in line order, as `GET /timetable/options` sends them. */
export const options: LineOptions = {
  lineCode: 'MAIN',
  stations: [
    { code: 'CF', sequence: 1, names: { en: 'Colombo Fort', si: 'කොළඹ කොටුව', ta: 'கொழும்பு கோட்டை' }, platforms: ['1', '2', '3'] },
    { code: 'MDA', sequence: 2, names: { en: 'Maradana', si: 'මරදාන', ta: 'மருதானை' }, platforms: ['1', '2'] },
    { code: 'RGM', sequence: 3, names: { en: 'Ragama', si: 'රාගම', ta: 'ராகம' }, platforms: ['1', '2'] },
    { code: 'GPH', sequence: 4, names: { en: 'Gampaha', si: 'ගම්පහ', ta: 'கம்பஹா' }, platforms: ['1', '2'] },
    { code: 'VYG', sequence: 5, names: { en: 'Veyangoda', si: 'වේයන්ගොඩ', ta: 'வேயங்கொடை' }, platforms: ['1', '2'] },
  ],
}

export const row = (overrides: Partial<ServiceRow> = {}): ServiceRow => ({
  serviceId: 's1',
  trainNumber: '8058',
  category: 'Regional',
  serviceType: 'Slow',
  fromName: 'Veyangoda',
  toName: 'Colombo Fort',
  runsOn: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'],
  date: null,
  firstDeparture: '08:20',
  stopCount: 5,
  stopsLabel: 'Every stop (5)',
  version: 1,
  amended: false,
  effectiveFrom: '2000-01-01',
  cancelledRanges: [],
  ...overrides,
})

export const cancellation = (overrides: Partial<CancellationView> = {}): CancellationView => ({
  cancellationId: 'c1',
  serviceId: 's1',
  trainNumber: '8072',
  fromName: 'Veyangoda',
  toName: 'Colombo Fort',
  from: '2026-10-23',
  to: '2026-10-23',
  reason: 'Rolling stock not available',
  cancelledBy: 'jayasinghe',
  cancelledAt: '2026-10-19T06:15:00Z',
  ...overrides,
})

export const detail = (overrides: Partial<ServiceDetail> = {}): ServiceDetail => ({
  serviceId: 's1',
  trainNumber: '8072',
  kind: 'Weekly',
  category: 'Regional',
  serviceType: 'Express',
  direction: 'Inbound',
  runsOn: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'],
  effectiveFrom: '2026-10-20',
  effectiveTo: null,
  version: 2,
  stops: [
    { sequence: 1, stationCode: 'VYG', names: { en: 'Veyangoda', si: 'වේයන්ගොඩ' }, arrival: null, departure: '17:30', platform: '1', isFirst: true, isLast: false },
    { sequence: 2, stationCode: 'CF', names: { en: 'Colombo Fort', si: 'කොළඹ කොටුව' }, arrival: '17:58', departure: null, platform: null, isFirst: false, isLast: true },
  ],
  versions: [
    { version: 1, serviceId: 's0', effectiveFrom: '2026-10-20', effectiveTo: '2026-10-25', createdAt: '2026-10-19T06:00:00Z', createdBy: 'jayasinghe', summary: null },
    { version: 2, serviceId: 's1', effectiveFrom: '2026-10-26', effectiveTo: null, createdAt: '2026-10-19T07:00:00Z', createdBy: 'jayasinghe', summary: 'Veyangoda 17:30 (was 17:25)' },
  ],
  cancellations: [],
  ...overrides,
})

export const valid: ValidationResult = { valid: true, errors: [], warnings: [] }

/** A fake API: reads answer from the table, posts answer from the handler. */
export function fakeApi(
  reads: Partial<Record<'options' | 'weekly' | 'extra' | 'cancelled', unknown>> & { detail?: ServiceDetail } = {},
  post: Api['post'] = vi.fn(async () => valid),
) {
  const get = vi.fn(async (path: string) => {
    if (path.includes('/options')) return reads.options ?? options
    if (path.includes('tab=weekly')) return reads.weekly ?? []
    if (path.includes('tab=extra')) return reads.extra ?? []
    if (path.includes('tab=cancelled')) return reads.cancelled ?? []
    if (path.includes('/services/')) return reads.detail ?? detail()
    throw new Error(`unexpected GET ${path}`)
  })
  return { get: get as unknown as Api['get'], post } satisfies Api
}
