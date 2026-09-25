import React, { createContext, useContext, useEffect, useState } from 'react';

type ToastType = 'success' | 'error';
interface ToastMessage {
  text: string;
  type: ToastType;
}

const ToastContext = createContext<(text: string, type?: ToastType) => void>(() => {});

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toast, setToast] = useState<ToastMessage | null>(null);

  useEffect(() => {
    if (!toast) return;
    const timeout = window.setTimeout(() => setToast(null), 4000);
    return () => window.clearTimeout(timeout);
  }, [toast]);

  const showToast = (text: string, type: ToastType = 'success') => {
    setToast({ text, type });
  };

  return (
    <ToastContext.Provider value={showToast}>
      {children}
      {toast && (
        <div
          className={`fixed top-4 right-4 z-50 flex items-center justify-between gap-4 p-4 border rounded-md shadow-lg text-sm max-w-sm w-full ${
            toast.type === 'error'
              ? 'bg-red-50 border-red-200 text-red-800'
              : 'bg-green-50 border-green-200 text-green-800'
          }`}
          role={toast.type === 'error' ? 'alert' : 'status'}
        >
          <span>{toast.text}</span>
          <button
            type="button"
            className="p-0 border-0 bg-transparent text-inherit text-xl leading-none cursor-pointer"
            aria-label="Đóng thông báo"
            onClick={() => setToast(null)}
          >
            ×
          </button>
        </div>
      )}
    </ToastContext.Provider>
  );
}

export const useToast = () => useContext(ToastContext);
