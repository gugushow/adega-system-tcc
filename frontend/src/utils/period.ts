import type { Period } from '../types/report'
import { addDaysIso, todayIso } from './format'

export type PeriodPreset = 'today' | '7d' | '30d' | 'month' | 'custom'

/** Converte um atalho em datas "de/até". "7 dias" = hoje e os 6 dias anteriores. */
export function presetPeriod(preset: Exclude<PeriodPreset, 'custom'>): Period {
  const today = todayIso()
  switch (preset) {
    case 'today':
      return { from: today, to: today }
    case '7d':
      return { from: addDaysIso(today, -6), to: today }
    case '30d':
      return { from: addDaysIso(today, -29), to: today }
    case 'month':
      return { from: `${today.slice(0, 8)}01`, to: today }
  }
}
