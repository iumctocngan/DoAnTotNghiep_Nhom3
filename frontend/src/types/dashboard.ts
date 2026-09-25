import type { UserRole } from './auth';

export { type UserRole };

export interface StatusCountResponse {
  status: string;
  count: number;
}

export interface MonthlyCountResponse {
  year: number;
  month: number;
  count: number;
}

export interface AdminDashboardResponse {
  activeStaffCount: number;
  unarchivedStudentCount: number;
  activeClassCount: number;
  activeEnrollmentCount: number;
  classesByStatus: StatusCountResponse[];
  enrollmentStartsByMonth: MonthlyCountResponse[];
}

export interface UpcomingSessionResponse {
  id: number;
  classId: number;
  classCode: string;
  className: string;
  sessionDate: string;
  startTime: string;
  endTime: string;
}

export interface TeacherDashboardResponse {
  assignedClassCount: number;
  activeStudentCount: number;
  todaySessionCount: number;
  pendingSessionCount: number;
  upcomingSessions: UpcomingSessionResponse[];
}

export interface StudentSummaryResponse {
  id: number;
  studentCode: string;
  fullName: string;
}

export interface CustomerCareDashboardResponse {
  unarchivedStudentCount: number;
  activeEnrollmentCount: number;
  pausedEnrollmentCount: number;
  studentsWithoutGuardianCount: number;
  enrollmentsByStatus: StatusCountResponse[];
  studentsWithoutGuardian: StudentSummaryResponse[];
}

export interface MonthlyRevenueResponse {
  year: number;
  month: number;
  amount: number;
}

export interface AccountingTransactionResponse {
  paymentId: number;
  paymentNumber: string;
  receiptNumber: string;
  invoiceId: number;
  invoiceNumber: string;
  studentId: number;
  studentName: string;
  amount: number;
  paidAt: string;
  method: string;
  status: string;
  note: string | null;
}

export interface AccountingAuditLogResponse {
  id: number;
  userId: string | null;
  action: string;
  entityType: string;
  entityId: string;
  description: string;
  occurredAt: string;
}

export interface AccountingDashboardResponse {
  revenue: number;
  currentDebt: number;
  overdueDebt: number;
  fromDate: string | null;
  toDate: string | null;
  revenueByMonth: MonthlyRevenueResponse[];
  transactions: AccountingTransactionResponse[];
  auditLogs: AccountingAuditLogResponse[];
}

export interface AccountingDashboardFilter {
  fromDate?: string;
  toDate?: string;
}
