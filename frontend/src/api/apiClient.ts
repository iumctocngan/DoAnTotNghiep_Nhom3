import type { ApiError, LoginResponse } from '../types/auth';

let inMemoryAccessToken: string | null = null;
let tokenVersion = 0;
let singleFlightRefreshPromise: Promise<LoginResponse | null> | null = null;
let onUnauthorizedCallback: (() => void) | null = null;

export function setAccessToken(token: string | null): void {
  inMemoryAccessToken = token;
  tokenVersion++;
}

export function getAccessToken(): string | null {
  return inMemoryAccessToken;
}

export function setOnUnauthorizedCallback(cb: (() => void) | null): void {
  onUnauthorizedCallback = cb;
}

/**
 * Single-flight refresh token mutex.
 * Đảm bảo chỉ DUY NHẤT 1 request /api/auth/refresh được gửi đi tại một thời điểm.
 * Các request khác sẽ đợi và dùng chung kết quả token mới.
 */
export async function refreshAccessTokenSingleFlight(): Promise<LoginResponse | null> {
  if (singleFlightRefreshPromise) {
    return singleFlightRefreshPromise;
  }

  const startedAtVersion = tokenVersion;
  singleFlightRefreshPromise = (async () => {
    try {
      const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'same-origin', // Tự động gửi HttpOnly cookie trên cùng origin (Dev proxy / Prod reverse proxy)
      });

      if (tokenVersion !== startedAtVersion) return null;
      if (!response.ok) {
        setAccessToken(null);
        if (onUnauthorizedCallback) {
          onUnauthorizedCallback();
        }
        return null;
      }

      const data: LoginResponse = await response.json();
      if (tokenVersion !== startedAtVersion) return null;
      setAccessToken(data.accessToken);
      return data;
    } catch {
      if (tokenVersion === startedAtVersion) {
        setAccessToken(null);
        onUnauthorizedCallback?.();
      }
      return null;
    } finally {
      singleFlightRefreshPromise = null;
    }
  })();

  return singleFlightRefreshPromise;
}

interface RequestOptions extends RequestInit {
  _retry?: boolean;
  params?: Record<string, unknown>;
}

/**
 * Trình bọc fetch tập trung:
 * - Chuẩn cùng origin (same-origin): dùng relative path /api/... cho cả Dev (Vite proxy) và Production.
 * - Tự động gắn Bearer Token từ bộ nhớ in-memory.
 * - Tự động serialize query params (bỏ qua null/undefined/'').
 * - Single-flight refresh token + Tự động retry 401 đúng một lần.
 */
export async function apiClient<T>(endpoint: string, options: RequestOptions = {}): Promise<T> {
  let url = endpoint;
  if (options.params) {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(options.params)) {
      if (v !== undefined && v !== null && v !== '') {
        qs.set(k, String(v).trim());
      }
    }
    const queryString = qs.toString();
    if (queryString) {
      url += (url.includes('?') ? '&' : '?') + queryString;
    }
  }

  const headers = new Headers(options.headers || {});

  if (!headers.has('Content-Type') && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  if (inMemoryAccessToken && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${inMemoryAccessToken}`);
  }

  const fetchOptions: RequestInit = {
    ...options,
    headers,
    credentials: 'same-origin',
  };

  let response: Response;
  try {
    response = await fetch(url, fetchOptions);
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Lỗi kết nối mạng đến máy chủ.';
    const apiError: ApiError = { message, statusCode: 0 };
    throw apiError;
  }

  // Tự động retry 401 đúng một lần với token mới
  if (
    response.status === 401 &&
    !options._retry &&
    endpoint !== '/api/auth/login' &&
    endpoint !== '/api/auth/refresh'
  ) {
    const refreshed = await refreshAccessTokenSingleFlight();
    if (refreshed) {
      const retryHeaders = new Headers(options.headers || {});
      if (!retryHeaders.has('Content-Type') && !(options.body instanceof FormData)) {
        retryHeaders.set('Content-Type', 'application/json');
      }
      retryHeaders.set('Authorization', `Bearer ${refreshed.accessToken}`);

      return apiClient<T>(endpoint, {
        ...options,
        headers: retryHeaders,
        _retry: true,
      });
    } else {
      const apiError: ApiError = {
        message: 'Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.',
        statusCode: 401,
      };
      throw apiError;
    }
  }

  if (!response.ok) {
    let errorData: { detail?: string; message?: string; title?: string; errors?: Record<string, string[]> } | string | null = null;
    try {
      errorData = await response.json();
    } catch {
      // Body không phải JSON
    }

    let validationMessage = '';
    const errObj = typeof errorData === 'object' && errorData !== null ? errorData : null;
    if (errObj?.errors && typeof errObj.errors === 'object') {
      const errorList = Object.values(errObj.errors).flat();
      if (errorList.length > 0) {
        validationMessage = String(errorList[0]);
      }
    }

    const message =
      errObj?.detail ||
      validationMessage ||
      errObj?.message ||
      errObj?.title ||
      (typeof errorData === 'string' ? errorData : null) ||
      `Yêu cầu thất bại (Mã HTTP ${response.status})`;

    const apiError: ApiError = {
      message,
      statusCode: response.status,
      errors: errObj?.errors,
    };
    throw apiError;
  }

  if (response.status === 204) {
    return null as T;
  }

  return response.json() as Promise<T>;
}
