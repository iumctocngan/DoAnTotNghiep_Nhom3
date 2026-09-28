import { apiClient } from './apiClient';
import type { ClassResponse, EnrollmentResponse } from '../types/class';

export const classApi = {
  getClasses: (params?: { search?: string; status?: number; page?: number; pageSize?: number }) =>
    apiClient<ClassResponse[]>('/api/classes', {
      params: params as Record<string, unknown>,
    }),

  getClassById: (id: number) =>
    apiClient<ClassResponse>(`/api/classes/${id}`),

  getEnrollments: (params?: { lopId?: number; hocVienId?: number; trangThai?: number; page?: number; pageSize?: number }) =>
    apiClient<EnrollmentResponse[]>('/api/enrollments', {
      params: params as Record<string, unknown>,
    }),
};
