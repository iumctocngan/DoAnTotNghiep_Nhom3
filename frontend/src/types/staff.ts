import type { UserRole } from './auth';

export type EmploymentStatus = 1 | 2; // 1: Active, 2: Inactive

export interface StaffResponse {
  id: string;
  employeeCode: string;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  role: UserRole;
  status: EmploymentStatus;
}

export interface CreateStaffRequest {
  employeeCode: string;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  role: UserRole;
  temporaryPassword: string;
}

export interface UpdateStaffRequest {
  fullName: string;
  email: string;
  phoneNumber?: string | null;
}

export interface ChangeStaffRoleRequest {
  role: UserRole;
}

export interface ResetStaffPasswordRequest {
  newPassword: string;
}

export interface StaffFilterParams {
  search?: string;
  role?: string;
  status?: EmploymentStatus;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
}
