import type { PagedResult } from './staff';

export type SessionStatus = 1 | 2 | 3; // 1: Scheduled (Đã lên lịch), 2: Completed (Hoàn tất), 3: Cancelled (Đã hủy)
export type AttendanceStatus = 1 | 2; // 1: Present (Có mặt), 2: Absent (Vắng mặt)

export interface SessionResponse {
  id: number;
  classId: number;
  classCode: string;
  className: string;
  lessonId?: number | null;
  lessonCode?: string | null;
  lessonName?: string | null;
  sessionDate: string; // YYYY-MM-DD
  startTime: string; // HH:mm:ss
  endTime: string; // HH:mm:ss
  status: SessionStatus;
  note?: string | null;
}

export interface CreateSessionRequest {
  classId: number;
  lessonId?: number | null;
  sessionDate: string;
  startTime: string;
  endTime: string;
  note?: string | null;
}

export interface UpdateSessionRequest {
  lessonId?: number | null;
  sessionDate: string;
  startTime: string;
  endTime: string;
  note?: string | null;
}

export interface SessionFilterParams {
  classId?: number;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export interface AttendanceItemResponse {
  enrollmentId: number;
  studentId: number;
  studentCode: string;
  studentFullName: string;
  remainingSessions: number;
  attendanceId?: number | null;
  status?: AttendanceStatus | null;
  note?: string | null;
  markedBy?: string | null;
  markedAt?: string | null;
  updatedBy?: string | null;
  updatedAt?: string | null;
}

export interface SessionAttendanceResponse {
  sessionId: number;
  classId: number;
  classCode: string;
  className: string;
  sessionDate: string;
  startTime: string;
  endTime: string;
  sessionStatus: SessionStatus;
  tongSoHocVien: number;
  soLuongCoMat: number;
  soLuongVangMat: number;
  soLuongChuaDiemDanh: number;
  danhSachHocVien: AttendanceItemResponse[];
  khongTheDiemDanh: { fullName: string; reason: string; remainingSessions: number }[];
}

export interface SaveAttendanceItem {
  enrollmentId: number;
  status: AttendanceStatus;
  note?: string | null;
}

export interface SaveAttendanceRequest {
  items: SaveAttendanceItem[];
}

export type { PagedResult };
