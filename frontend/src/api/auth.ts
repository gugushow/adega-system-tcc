import type { ChangePasswordInput, LoginInput, LoginResponse, User } from '../types/user'
import { http, unwrap } from './http'

export const authApi = {
  login: (input: LoginInput) => unwrap<LoginResponse>(http.post('/auth/login', input)),
  me: () => unwrap<User>(http.get('/auth/me')),
  changePassword: (input: ChangePasswordInput) => http.put('/auth/me/password', input),
}
