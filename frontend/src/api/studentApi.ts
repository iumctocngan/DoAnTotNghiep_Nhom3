import { apiClient } from './apiClient';
import type {
  StudentFilterParams,
  StudentResponse,
  PagedResult,
} from '../types/student';

export const studentApi = {
  getStudents: (params?: StudentFilterParams) =>
    apiClient<PagedResult<StudentResponse>>('/api/students', {
      params: params as Record<string, unknown>,
    }),

  getStudentById: (id: number) =>
    apiClient<StudentResponse>(`/api/students/${id}`),
};
