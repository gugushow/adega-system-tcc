import type { LoyaltySettings } from '../types/loyalty'
import { http, unwrap } from './http'

export interface MarginSettings {
  minimumMarginPercent: number
}

export const settingsApi = {
  getMargin: () => unwrap<MarginSettings>(http.get('/settings/margin')),
  updateMargin: (input: MarginSettings) => unwrap<MarginSettings>(http.put('/settings/margin', input)),
  getLoyalty: () => unwrap<LoyaltySettings>(http.get('/settings/loyalty')),
  updateLoyalty: (input: LoyaltySettings) => unwrap<LoyaltySettings>(http.put('/settings/loyalty', input)),
}
