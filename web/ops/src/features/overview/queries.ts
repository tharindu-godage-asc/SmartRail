import type { Overview } from '../../core/api/types'
import { usePolled } from '../../core/usePolled'

export const useOverview = () => usePolled<Overview>('overview', '/api/v1/overview')
