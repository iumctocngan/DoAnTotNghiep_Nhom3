import { apiClient } from './apiClient';

export interface AttendanceAbsenceReportItem {
  studentId: number;
  studentCode: string;
  studentFullName: string;
  courseId: number;
  courseCode: string;
  courseName: string;
  absenceCount: number;
}

export interface AttendanceAbsenceReportFilter {
  courseId?: number;
  classId?: number;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export interface AttendanceAbsenceReportPage {
  items: AttendanceAbsenceReportItem[];
  page: number;
  pageSize: number;
  totalItems: number;
}

export const attendanceReportApi = {
  getAbsences: (params: AttendanceAbsenceReportFilter) =>
    apiClient<AttendanceAbsenceReportPage>('/api/attendance-reports/absences', {
      params: params as Record<string, unknown>,
    }),
};
