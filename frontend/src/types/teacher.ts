import type { PagedResult } from './staff';

export type ClassStatus = 1 | 2 | 3 | 4; // 1: Preparing, 2: Active, 3: Completed, 4: Cancelled
export type SessionStatus = 1 | 2 | 3; // 1: Scheduled, 2: Completed, 3: Cancelled

export interface TeacherClassResponse {
  id: number;
  classCode: string;
  name: string;
  levelId: number;
  levelName: string;
  capacity: number;
  startDate: string;
  endDate?: string | null;
  dayOfWeek: number; // 0: Sunday, 1: Monday, ..., 6: Saturday
  startTime: string;
  endTime: string;
  status: ClassStatus;
}

export interface TeacherScheduleResponse {
  sessionId: number;
  classId: number;
  classCode: string;
  className: string;
  lessonId?: number | null;
  lessonCode?: string | null;
  lessonName?: string | null;
  sessionDate: string;
  startTime: string;
  endTime: string;
  status: SessionStatus;
  note?: string | null;
}

export interface TeacherClassesParams {
  status?: ClassStatus;
  page?: number;
  pageSize?: number;
}

export interface TeacherScheduleParams {
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export type { PagedResult };
