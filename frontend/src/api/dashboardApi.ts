import { apiClient } from './apiClient';
import type {
  AccountingDashboardFilter,
  AccountingDashboardResponse,
  AdminDashboardResponse,
  CustomerCareDashboardResponse,
  TeacherDashboardResponse,
} from '../types/dashboard';

export const dashboardApi = {
  getAdminDashboard(): Promise<AdminDashboardResponse> {
    return apiClient<AdminDashboardResponse>('/api/dashboard/admin');
  },

  getTeacherDashboard(): Promise<TeacherDashboardResponse> {
    return apiClient<TeacherDashboardResponse>('/api/dashboard/teacher');
  },

  getCustomerCareDashboard(): Promise<CustomerCareDashboardResponse> {
    return apiClient<CustomerCareDashboardResponse>('/api/dashboard/customer-care');
  },

  getAccountingDashboard(filter?: AccountingDashboardFilter): Promise<AccountingDashboardResponse> {
    return apiClient<AccountingDashboardResponse>('/api/dashboard/accounting', {
      params: filter as Record<string, unknown>,
    });
  },
};
