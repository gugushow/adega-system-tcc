import type { PagedResult } from '../types/api'
import type { AuditLog, AuditLogQuery } from '../types/audit'
import { http, unwrap } from './http'

export const auditApi = {
  list: (query: AuditLogQuery) => unwrap<PagedResult<AuditLog>>(http.get('/audit-logs', { params: query })),
}
