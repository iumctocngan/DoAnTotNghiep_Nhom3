export type UserRole = 'Admin' | 'Teacher' | 'Accountant' | 'CustomerCare';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  employeeCode: string;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface CurrentUserResponse {
  userId: string;
  employeeCode: string;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ApiError {
  message: string;
  statusCode: number;
  errors?: Record<string, string[]>;
}
