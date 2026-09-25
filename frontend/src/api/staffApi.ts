import { apiClient } from './apiClient';
import type {
  ChangeStaffRoleRequest,
  CreateStaffRequest,
  PagedResult,
  ResetStaffPasswordRequest,
  StaffFilterParams,
  StaffResponse,
  UpdateStaffRequest,
} from '../types/staff';

export const staffApi = {
  getStaff: (params?: StaffFilterParams) =>
    apiClient<PagedResult<StaffResponse>>('/api/staff', { params: params as Record<string, unknown> }),
  getStaffById: (id: string) => apiClient<StaffResponse>(`/api/staff/${id}`),
  createStaff: (body: CreateStaffRequest) =>
    apiClient<StaffResponse>('/api/staff', { method: 'POST', body: JSON.stringify(body) }),
  updateStaff: (id: string, body: UpdateStaffRequest) =>
    apiClient<StaffResponse>(`/api/staff/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  changeRole: (id: string, body: ChangeStaffRoleRequest) =>
    apiClient<void>(`/api/staff/${id}/role`, { method: 'PUT', body: JSON.stringify(body) }),
  resetPassword: (id: string, body: ResetStaffPasswordRequest) =>
    apiClient<void>(`/api/staff/${id}/reset-password`, { method: 'POST', body: JSON.stringify(body) }),
  activateStaff: (id: string) => apiClient<void>(`/api/staff/${id}/activate`, { method: 'POST' }),
  deactivateStaff: (id: string) => apiClient<void>(`/api/staff/${id}/deactivate`, { method: 'POST' }),
};
