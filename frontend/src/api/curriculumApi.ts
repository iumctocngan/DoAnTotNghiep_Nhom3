import { apiClient } from './apiClient';
import type {
  CourseRequest,
  CourseResponse,
  CurriculumFilterParams,
  LessonRequest,
  LessonResponse,
  LevelRequest,
  LevelResponse,
  PagedResult,
} from '../types/curriculum';

export const curriculumApi = {
  // COURSES
  getCourses: (params?: CurriculumFilterParams) =>
    apiClient<PagedResult<CourseResponse>>('/api/courses', { params: params as Record<string, unknown> }),
  getCourse: (id: number) => apiClient<CourseResponse>(`/api/courses/${id}`),
  createCourse: (body: CourseRequest) =>
    apiClient<CourseResponse>('/api/courses', { method: 'POST', body: JSON.stringify(body) }),
  updateCourse: (id: number, body: CourseRequest) =>
    apiClient<CourseResponse>(`/api/courses/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deactivateCourse: (id: number) => apiClient<void>(`/api/courses/${id}/deactivate`, { method: 'POST' }),

  // LEVELS
  getLevels: (courseId: number, params?: CurriculumFilterParams) =>
    apiClient<PagedResult<LevelResponse>>('/api/levels', {
      params: { courseId, ...params } as Record<string, unknown>,
    }),
  getLevel: (id: number) => apiClient<LevelResponse>(`/api/levels/${id}`),
  createLevel: (body: LevelRequest) =>
    apiClient<LevelResponse>('/api/levels', { method: 'POST', body: JSON.stringify(body) }),
  updateLevel: (id: number, body: LevelRequest) =>
    apiClient<LevelResponse>(`/api/levels/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deactivateLevel: (id: number) => apiClient<void>(`/api/levels/${id}/deactivate`, { method: 'POST' }),

  // LESSONS
  getLessons: (levelId: number, params?: CurriculumFilterParams) =>
    apiClient<PagedResult<LessonResponse>>('/api/lessons', {
      params: { levelId, ...params } as Record<string, unknown>,
    }),
  getLesson: (id: number) => apiClient<LessonResponse>(`/api/lessons/${id}`),
  createLesson: (body: LessonRequest) =>
    apiClient<LessonResponse>('/api/lessons', { method: 'POST', body: JSON.stringify(body) }),
  updateLesson: (id: number, body: LessonRequest) =>
    apiClient<LessonResponse>(`/api/lessons/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deactivateLesson: (id: number) => apiClient<void>(`/api/lessons/${id}/deactivate`, { method: 'POST' }),
};
