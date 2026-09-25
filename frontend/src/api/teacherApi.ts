import { apiClient } from './apiClient';
import type {
  PagedResult,
  TeacherClassesParams,
  TeacherClassResponse,
  TeacherScheduleParams,
  TeacherScheduleResponse,
} from '../types/teacher';

export const teacherApi = {
  getClasses: (teacherId: string, params?: TeacherClassesParams) =>
    apiClient<PagedResult<TeacherClassResponse>>(`/api/teachers/${teacherId}/classes`, {
      params: params as Record<string, unknown>,
    }),

  getSchedule: (teacherId: string, params?: TeacherScheduleParams) =>
    apiClient<PagedResult<TeacherScheduleResponse>>(`/api/teachers/${teacherId}/schedule`, {
      params: params as Record<string, unknown>,
    }),
};
