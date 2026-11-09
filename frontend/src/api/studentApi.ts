import { apiClient } from './apiClient';
import type {
  StudentFilterParams,
  StudentResponse,
  StudentGuardianResponse,
  PagedResult,
} from '../types/student';

export const studentApi = {
  getStudents: (params?: StudentFilterParams) =>
    apiClient<PagedResult<StudentResponse>>('/api/students', {
      params: params as Record<string, unknown>,
    }),

  getStudentById: (id: number) =>
    apiClient<StudentResponse>(`/api/students/${id}`),

  getStudentGuardians: (id: number) =>
    apiClient<StudentGuardianResponse[]>(`/api/students/${id}/guardians`),
};
