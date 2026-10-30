import React from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './context/AuthContext';
import { ProtectedRoute } from './components/common/ProtectedRoute';
import { AppLayout } from './components/layout/AppLayout';
import { LoginPage } from './pages/auth/LoginPage';
import { DashboardPage } from './pages/dashboard/DashboardPage';
import { StaffListPage } from './pages/staff/StaffListPage';
import { RolePermissionMatrixPage } from './pages/permissions/RolePermissionMatrixPage';
import { CurriculumPage } from './pages/curriculum/CurriculumPage';
import { TeacherClassesPage } from './pages/teacher/TeacherClassesPage';
import { TeacherSchedulePage } from './pages/teacher/TeacherSchedulePage';
import { SessionListPage } from './pages/sessions/SessionListPage';
import { InvoiceListPage } from './pages/invoices/InvoiceListPage';
import { ToastProvider } from './components/common/ToastProvider';
import { TrangQuanLy } from './pages/quan-ly/TrangQuanLy';
import { AssessmentPage } from './pages/teacher/AssessmentPage';
import { ClassAssistantPage } from './pages/assistant/ClassAssistantPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

export const App: React.FC = () => {
  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthProvider>
          <BrowserRouter>
            <Routes>
              {/* Tuyến công khai */}
              <Route path="/login" element={<LoginPage />} />

              {/* Tuyến bảo vệ: Yêu cầu đăng nhập */}
              <Route element={<ProtectedRoute />}>
                <Route element={<AppLayout />}>
                  <Route path="/" element={<DashboardPage />} />

                  {/* Quản lý học viên & người giám hộ */}
                  <Route element={<ProtectedRoute requiredPermission="students.view" />}>
                    <Route path="/hoc-vien" element={<TrangQuanLy key="hoc-vien" phanHe="hoc-vien" />} />
                    <Route path="/hoc-vien/:id/nguoi-giam-ho" element={<TrangQuanLy key="lien-ket" phanHe="lien-ket" />} />
                  </Route>

                  <Route element={<ProtectedRoute requiredPermission="guardians.view" />}>
                    <Route path="/nguoi-giam-ho" element={<TrangQuanLy key="nguoi-giam-ho" phanHe="nguoi-giam-ho" />} />
                  </Route>

                  {/* Vận hành: Lớp học & Ghi danh */}
                  <Route element={<ProtectedRoute requiredPermission="classes.view" />}>
                    <Route path="/lop-hoc" element={<TrangQuanLy key="lop-hoc" phanHe="lop-hoc" />} />
                  </Route>

                  <Route element={<ProtectedRoute requiredPermission="enrollments.view" />}>
                    <Route path="/ghi-danh" element={<TrangQuanLy key="ghi-danh" phanHe="ghi-danh" />} />
                  </Route>

                  <Route element={<ProtectedRoute requiredPermission="enrollments.remarks" />}>
                    <Route path="/ghi-danh/:id/nhan-xet" element={<TrangQuanLy key="nhan-xet" phanHe="nhan-xet" />} />
                  </Route>

                  {/* Buổi học & Điểm danh */}
                  <Route element={<ProtectedRoute requiredPermission="sessions.view" />}>
                    <Route path="/sessions" element={<SessionListPage />} />
                  </Route>

                  {/* Học phí & Hóa đơn */}
                  <Route element={<ProtectedRoute requiredPermission="invoices.view" />}>
                    <Route path="/invoices" element={<InvoiceListPage />} />
                  </Route>

                  {/* Chương trình học */}
                  <Route element={<ProtectedRoute requiredPermission="curriculum.view" />}>
                    <Route path="/curriculum" element={<CurriculumPage />} />
                  </Route>

                  {/* Quản lý hệ thống */}
                  <Route element={<ProtectedRoute requiredPermission="staff.view" />}>
                    <Route path="/staff" element={<StaffListPage />} />
                  </Route>

                  <Route element={<ProtectedRoute requiredPermission="roles.manage" />}>
                    <Route path="/permissions" element={<RolePermissionMatrixPage />} />
                  </Route>

                  {/* Giảng dạy & Đánh giá AI */}
                  <Route element={<ProtectedRoute requiredPermission="teacher.classes" />}>
                    <Route path="/teacher/classes" element={<TeacherClassesPage />} />
                    <Route path="/teacher/schedule" element={<TeacherSchedulePage />} />
                  </Route>

                  <Route element={<ProtectedRoute requiredPermission="assessments.view" />}>
                    <Route path="/assessments" element={<AssessmentPage />} />
                  </Route>

                  {/* Trợ lý học liệu AI */}
                  <Route element={<ProtectedRoute requiredPermission="assistant.view" />}>
                    <Route path="/class-assistant" element={<ClassAssistantPage />} />
                    <Route path="/notebooks" element={<Navigate to="/class-assistant" replace />} />
                  </Route>
                </Route>
              </Route>

              {/* Điều hướng mặc định */}
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </BrowserRouter>
        </AuthProvider>
      </ToastProvider>
    </QueryClientProvider>
  );
};

export default App;
