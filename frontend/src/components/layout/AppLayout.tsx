import React, { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { authApi } from '../../api/authApi';
import { useAuth } from '../../context/AuthContext';
import { ChangePasswordModal } from '../../pages/auth/ChangePasswordModal';
import { getRoleLabel } from '../../utils/role';
import { ConfirmDialog } from '../common/ConfirmDialog';
import { useToast } from '../common/ToastProvider';

export const AppLayout: React.FC = () => {
  const { user, logout } = useAuth();
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [isLogoutConfirmationOpen, setIsLogoutConfirmationOpen] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => window.innerWidth > 760);
  const userMenuRef = useRef<HTMLDivElement>(null);
  const showToast = useToast();
  const { data: claimProfile } = useQuery({
    queryKey: ['auth', 'me', user?.userId],
    queryFn: authApi.getMe,
    enabled: isUserMenuOpen && !!user,
    staleTime: 60_000,
  });
  const profile = claimProfile ?? user;
  const roleLabel = getRoleLabel(profile?.role);
  const avatarLetter = profile?.fullName.trim().split(/\s+/).at(-1)?.charAt(0).toLocaleUpperCase('vi-VN') ?? 'N';

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    };
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsUserMenuOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleEscape);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleEscape);
    };
  }, []);

  const handleLogout = async () => {
    setIsLogoutConfirmationOpen(false);
    setIsLoggingOut(true);
    try {
      await logout();
    } catch {
      showToast('Đã đăng xuất trên trình duyệt này, nhưng chưa xác nhận được phiên trên máy chủ đã bị thu hồi.', 'error');
    } finally {
      setIsLoggingOut(false);
    }
  };

  return (
    <div className="layout-container">
      <aside className={`sidebar ${isSidebarOpen ? '' : 'collapsed'}`}>
        <div className="sidebar-brand">
          <Link to="/" className="sidebar-brand-logo" title="Về trang chủ CMS EDU">
            <img src="/logo.png" alt="CMS EDU" />
          </Link>
          <div className="sidebar-brand-subtitle">Hệ thống Quản trị Trung tâm</div>
        </div>

        <nav
          className="sidebar-nav"
          onClick={(event) => {
            if (window.innerWidth <= 760 && (event.target as HTMLElement).closest('a'))
              setIsSidebarOpen(false);
          }}
        >
          <NavLink
            to="/"
            end
            className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
          >
            Tổng quan
          </NavLink>

          <NavLink
            to="/curriculum"
            className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
          >
            Chương trình học
          </NavLink>

          {(user?.role === 'Teacher' || user?.role === 'Admin') && (
            <>
              <NavLink
                to="/teacher/classes"
                className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
              >
                Lớp học phụ trách
              </NavLink>
              <NavLink
                to="/teacher/schedule"
                className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
              >
                Lịch giảng dạy
              </NavLink>
            </>
          )}

          {user?.role === 'Admin' && (
            <>
              <NavLink
                to="/staff"
                className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
              >
                Quản lý nhân sự
              </NavLink>
            </>
          )}
        </nav>

      </aside>
      {isSidebarOpen && (
        <button
          type="button"
          className="sidebar-backdrop"
          aria-label="Đóng thanh điều hướng"
          onClick={() => setIsSidebarOpen(false)}
        />
      )}

      <div className="main-wrapper">
        <header className="top-header">
          <div className="header-left">
            <button
              type="button"
              className="sidebar-toggle-btn"
              onClick={() => setIsSidebarOpen((prev) => !prev)}
              title={isSidebarOpen ? 'Thu gọn thanh điều hướng' : 'Mở rộng thanh điều hướng'}
              aria-label="Đóng mở thanh điều hướng"
              aria-expanded={isSidebarOpen}
            >
              ☰
            </button>
            <div className="header-greeting">
              <strong>Chào mừng trở lại</strong>
              <span>{roleLabel}</span>
            </div>
          </div>

          <div className="user-menu-container" ref={userMenuRef}>
            <button
              type="button"
              className="user-menu-trigger"
              onClick={() => setIsUserMenuOpen((prev) => !prev)}
              aria-expanded={isUserMenuOpen}
              aria-controls="account-panel"
              title="Thông tin tài khoản"
            >
              <span className="user-avatar" aria-hidden="true">{avatarLetter}</span>
              <span className="user-menu-name">
                <strong>{profile?.fullName}</strong>
                <small>{roleLabel}</small>
              </span>
              <span className="user-menu-chevron" aria-hidden="true">⌄</span>
            </button>

            {isUserMenuOpen && (
              <div className="user-menu-dropdown" id="account-panel">
                <div className="user-menu-profile">
                  <strong>{profile?.fullName}</strong>
                  <span>{roleLabel}</span>
                  <dl>
                    <div><dt>Mã nhân viên</dt><dd>{profile?.employeeCode}</dd></div>
                    <div><dt>Email</dt><dd>{profile?.email}</dd></div>
                  </dl>
                </div>
                <div className="user-menu-divider" />
                <button
                  type="button"
                  className="user-menu-item"
                  onClick={() => {
                    setIsUserMenuOpen(false);
                    setIsPasswordModalOpen(true);
                  }}
                >
                  Đổi mật khẩu
                </button>

                <div className="user-menu-divider" />

                <button
                  type="button"
                  className="user-menu-item"
                  onClick={() => {
                    setIsUserMenuOpen(false);
                    setIsLogoutConfirmationOpen(true);
                  }}
                  disabled={isLoggingOut}
                >
                  Đăng xuất
                </button>

              </div>
            )}
          </div>
        </header>

        {/* Content Outlet */}
        <main className="content-area">
          <Outlet />
        </main>
      </div>

      <ChangePasswordModal
        isOpen={isPasswordModalOpen}
        onClose={() => setIsPasswordModalOpen(false)}
      />
      {isLogoutConfirmationOpen && (
        <ConfirmDialog
          title="Đăng xuất"
          message="Bạn có chắc chắn muốn đăng xuất khỏi hệ thống?"
          confirmText="Đăng xuất"
          isPending={isLoggingOut}
          onCancel={() => setIsLogoutConfirmationOpen(false)}
          onConfirm={() => { void handleLogout(); }}
        />
      )}
    </div>
  );
};
