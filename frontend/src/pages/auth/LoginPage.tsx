import React, { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import type { ApiError } from '../../types/auth';

export const LoginPage: React.FC = () => {
  const { status, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isLoading = status === 'loading';
  const requestedPath = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname;
  const returnPath = requestedPath?.startsWith('/') && !requestedPath.startsWith('//') ? requestedPath : '/';

  // Nếu đã đăng nhập, chuyển thẳng về trang chủ
  if (status === 'authenticated') {
    return <Navigate to={returnPath} replace />;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password) {
      setError('Vui lòng nhập đầy đủ Email và Mật khẩu.');
      return;
    }

    setError(null);
    setIsSubmitting(true);

    try {
      await login({ email: email.trim(), password });
      navigate(returnPath, { replace: true });
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      setError(apiErr.message || 'Đăng nhập không thành công. Vui lòng kiểm tra lại tài khoản.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4">
      <div className="w-full max-w-[400px] bg-white border border-slate-200 rounded-md p-8 shadow-sm">
        <div className="mb-6 text-center">
          <img
            src="/logo.png"
            alt="CMS Logo"
            className="h-16 w-auto object-contain inline-block"
          />
        </div>

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="email">Email tài khoản</label>
            <input
              id="email"
              type="email"
              placeholder="nhanvien@cms.edu.vn"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isSubmitting || isLoading}
              autoComplete="username"
              required
            />
          </div>

          <div>
            <label htmlFor="password">Mật khẩu</label>
            <input
              id="password"
              type="password"
              placeholder="••••••••"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={isSubmitting || isLoading}
              autoComplete="current-password"
              required
            />
          </div>

          <button
            type="submit"
            className="btn btn-primary w-full py-2.5 mt-2"
            disabled={isSubmitting || isLoading}
          >
            {isLoading ? 'Đang kiểm tra phiên...' : isSubmitting ? 'Đang xác thực...' : 'Đăng nhập'}
          </button>
        </form>

        <div className="mt-6 pt-4 border-t border-slate-200 text-xs text-slate-500 text-center">
          Hệ thống dành riêng cho cán bộ, giáo viên và nhân viên nội bộ CMS EDU.
        </div>
      </div>
    </div>
  );
};
