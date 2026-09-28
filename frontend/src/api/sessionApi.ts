import { apiClient } from './apiClient';
import type {
  CreateSessionRequest,
  PagedResult,
  SaveAttendanceRequest,
  SessionAttendanceResponse,
  SessionFilterParams,
  SessionResponse,
  UpdateSessionRequest,
} from '../types/session';

export const sessionApi = {
  getSessions: (params?: SessionFilterParams) =>
    apiClient<PagedResult<SessionResponse>>('/api/sessions', {
      params: params as Record<string, unknown>,
    }),

  getSessionById: (id: number) =>
    apiClient<SessionResponse>(`/api/sessions/${id}`),

  createSession: (body: CreateSessionRequest) =>
    apiClient<SessionResponse>('/api/sessions', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  updateSession: (id: number, body: UpdateSessionRequest) =>
    apiClient<SessionResponse>(`/api/sessions/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),

  cancelSession: (id: number) =>
    apiClient<void>(`/api/sessions/${id}/cancel`, { method: 'POST' }),

  completeSession: (id: number) =>
    apiClient<void>(`/api/sessions/${id}/complete`, { method: 'POST' }),

  getAttendance: (sessionId: number) =>
    apiClient<SessionAttendanceResponse>(`/api/sessions/${sessionId}/attendance`),

  saveAttendance: (sessionId: number, body: SaveAttendanceRequest) =>
    apiClient<void>(`/api/sessions/${sessionId}/attendance`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
};
