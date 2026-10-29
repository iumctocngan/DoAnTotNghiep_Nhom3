export interface PermissionDto {
  code: string;
  name: string;
  group: string;
  description: string;
}

export interface RolePermissionsMatrixDto {
  permissions: PermissionDto[];
  roles: string[];
  rolePermissions: Record<string, string[]>;
}

export interface UpdateRolePermissionsRequest {
  permissions: string[];
}
