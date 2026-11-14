import type { PagedResult } from '../types/api'
import type { UpdateUserInput, User, UserInput, UserQuery } from '../types/user'
import { http, unwrap } from './http'

export const usersApi = {
  list: (query: UserQuery) => unwrap<PagedResult<User>>(http.get('/users', { params: query })),
  create: (input: UserInput) => unwrap<User>(http.post('/users', input)),
  update: (id: number, input: UpdateUserInput) => unwrap<User>(http.put(`/users/${id}`, input)),
  deactivate: (id: number) => http.delete(`/users/${id}`),
}
