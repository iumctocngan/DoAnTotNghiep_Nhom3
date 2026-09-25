import type { PagedResult } from './staff';

export interface CourseResponse {
  id: number;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface CourseRequest {
  code: string;
  name: string;
  description?: string | null;
}

export interface LevelResponse {
  id: number;
  courseId: number;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export interface LevelRequest {
  courseId: number;
  code: string;
  name: string;
  sortOrder: number;
}

export interface LessonResponse {
  id: number;
  levelId: number;
  code: string;
  name: string;
  objective?: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface LessonRequest {
  levelId: number;
  code: string;
  name: string;
  objective?: string | null;
  sortOrder: number;
}

export interface CurriculumFilterParams {
  search?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export type { PagedResult };
