import { apiClient, refreshAccessTokenSingleFlight, setAccessToken } from './apiClient';
import type {
  ChangePasswordRequest,
  CurrentUserResponse,
  LoginRequest,
  LoginResponse,
} from '../types/auth';

export const authApi = {
  async login(request: LoginRequest): Promise<LoginResponse> {
    const data = await apiClient<LoginResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify(request),
    });
    setAccessToken(data.accessToken);
    return data;
  },

  async refresh(): Promise<LoginResponse> {
    const data = await refreshAccessTokenSingleFlight();
    if (!data) throw new Error('Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.');
    return data;
  },

  async logout(): Promise<void> {
    try {
      await apiClient<void>('/api/auth/logout', {
        method: 'POST',
      });
    } finally {
      setAccessToken(null);
    }
  },

  async getMe(): Promise<CurrentUserResponse> {
    return apiClient<CurrentUserResponse>('/api/auth/me', {
      method: 'GET',
    });
  },

  async changePassword(request: ChangePasswordRequest): Promise<void> {
    await apiClient<void>('/api/auth/change-password', {
      method: 'POST',
      body: JSON.stringify(request),
    });
    // Sau khi đổi mật khẩu, backend hủy refreshToken nên FE xóa token
    setAccessToken(null);
  },
};
