import React, { createContext, useContext, useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { authApi } from '../api/authApi';
import { setOnUnauthorizedCallback } from '../api/apiClient';
import type {
  ChangePasswordRequest,
  CurrentUserResponse,
  LoginRequest,
} from '../types/auth';

type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated';

interface AuthContextType {
  user: CurrentUserResponse | null;
  status: AuthStatus;
  login: (credentials: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
  changePassword: (request: ChangePasswordRequest) => Promise<void>;
  refreshSession: () => Promise<boolean>;
  hasPermission: (permission: string) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<CurrentUserResponse | null>(null);
  const [status, setStatus] = useState<AuthStatus>('loading');

  const clearSession = () => {
    queryClient.clear();
    setUser(null);
    setStatus('unauthenticated');
  };

  // Khôi phục phiên làm việc tự động khi mở / F5 ứng dụng
  useEffect(() => {
    let isMounted = true;

    // Đăng ký callback khi apiClient phát hiện phiên đã hết hạn
    setOnUnauthorizedCallback(() => {
      if (isMounted) {
        clearSession();
      }
    });

    const initAuth = async () => {
      try {
        const loginResponse = await authApi.refresh();
        if (isMounted) {
          setUser({
            userId: loginResponse.userId,
            employeeCode: loginResponse.employeeCode,
            fullName: loginResponse.fullName,
            email: loginResponse.email,
            role: loginResponse.role,
            permissions: loginResponse.permissions ?? [],
          });
          setStatus('authenticated');
        }
      } catch {
        if (isMounted) {
          clearSession();
        }
      }
    };

    initAuth();

    return () => {
      isMounted = false;
      setOnUnauthorizedCallback(null);
    };
  }, [queryClient]);

  const login = async (credentials: LoginRequest) => {
    const data = await authApi.login(credentials);
    queryClient.clear();
    setUser({
      userId: data.userId,
      employeeCode: data.employeeCode,
      fullName: data.fullName,
      email: data.email,
      role: data.role,
      permissions: data.permissions ?? [],
    });
    setStatus('authenticated');
  };

  const logout = async () => {
    try {
      await authApi.logout();
    } finally {
      clearSession();
    }
  };

  const changePassword = async (request: ChangePasswordRequest) => {
    await authApi.changePassword(request);
    clearSession();
  };

  const refreshSession = async (): Promise<boolean> => {
    try {
      const data = await authApi.refresh();
      queryClient.clear();
      setUser({
        userId: data.userId,
        employeeCode: data.employeeCode,
        fullName: data.fullName,
        email: data.email,
        role: data.role,
        permissions: data.permissions ?? [],
      });
      setStatus('authenticated');
      return true;
    } catch {
      clearSession();
      return false;
    }
  };

  const hasPermission = (permission: string): boolean => {
    if (!user) return false;
    if (user.role === 'Admin') return true;
    return user.permissions?.includes(permission) ?? false;
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        status,
        login,
        logout,
        changePassword,
        refreshSession,
        hasPermission,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
