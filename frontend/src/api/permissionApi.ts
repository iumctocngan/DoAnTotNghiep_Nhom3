import { apiClient } from './apiClient';
import type {
  PermissionDto,
  RolePermissionsMatrixDto,
  UpdateRolePermissionsRequest,
} from '../types/permission';

export const permissionApi = {
  getPermissions: () => apiClient<PermissionDto[]>('/api/permissions'),
  getMatrix: () => apiClient<RolePermissionsMatrixDto>('/api/permissions/matrix'),
  getRolePermissions: (roleName: string) =>
    apiClient<string[]>(`/api/permissions/roles/${encodeURIComponent(roleName)}`),
  updateRolePermissions: (roleName: string, permissions: string[]) =>
    apiClient<void>(`/api/permissions/roles/${encodeURIComponent(roleName)}`, {
      method: 'PUT',
      body: JSON.stringify({ permissions } as UpdateRolePermissionsRequest),
    }),
};
