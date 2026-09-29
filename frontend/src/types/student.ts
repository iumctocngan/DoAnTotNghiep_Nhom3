import type { PagedResult } from './staff';

export type Gender = 1 | 2 | 3; // 1: Nam, 2: Nữ, 3: Khác

export interface StudentResponse {
  id: number;
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender?: Gender | null;
  learningNote?: string | null;
  isArchived: boolean;
  courseMonths: number;
  remainingSessions: number;
}

export interface CreateStudentRequest {
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender?: Gender | null;
  learningNote?: string | null;
}

export interface UpdateStudentRequest {
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender?: Gender | null;
  learningNote?: string | null;
}

export interface StudentFilterParams {
  search?: string;
  isArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface GuardianResponse {
  id: number;
  fullName: string;
  phone: string;
  email?: string | null;
  isActive: boolean;
}

export interface CreateGuardianRequest {
  fullName: string;
  phone: string;
  email?: string | null;
  isActive: boolean;
}

export interface UpdateGuardianRequest {
  fullName: string;
  phone: string;
  email?: string | null;
  isActive: boolean;
}

export interface StudentGuardianResponse {
  guardianId: number;
  fullName: string;
  phone: string;
  email?: string | null;
  isActive: boolean;
  relationship: string;
  isPrimary: boolean;
}

export interface AssignGuardianRequest {
  guardianId: number;
  relationship: string;
  isPrimary: boolean;
}

export interface UpdateGuardianLinkRequest {
  relationship: string;
  isPrimary: boolean;
}

export type { PagedResult };
