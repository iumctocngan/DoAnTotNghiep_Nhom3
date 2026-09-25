import React from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './context/AuthContext';
import { ProtectedRoute } from './components/common/ProtectedRoute';
import { AppLayout } from './components/layout/AppLayout';
import { LoginPage } from './pages/auth/LoginPage';
import { DashboardPage } from './pages/dashboard/DashboardPage';
import { StaffListPage } from './pages/staff/StaffListPage';
import { CurriculumPage } from './pages/curriculum/CurriculumPage';
import { TeacherClassesPage } from './pages/teacher/TeacherClassesPage';
import { TeacherSchedulePage } from './pages/teacher/TeacherSchedulePage';
import { ToastProvider } from './components/common/ToastProvider';

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
                <Route path="/curriculum" element={<CurriculumPage />} />
                <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
                  <Route path="/staff" element={<StaffListPage />} />
                </Route>
                <Route element={<ProtectedRoute allowedRoles={['Admin', 'Teacher']} />}>
                  <Route path="/teacher/classes" element={<TeacherClassesPage />} />
                  <Route path="/teacher/schedule" element={<TeacherSchedulePage />} />
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
