import type { ReactNode } from 'react';

interface ModalProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  titleClassName?: string;
  closeDisabled?: boolean;
  wide?: boolean;
}

export function Modal({ title, titleClassName, onClose, children, closeDisabled = false, wide = false }: ModalProps) {
  return (
    <div className="fixed inset-0 bg-slate-900/40 flex items-center justify-center z-50 p-4 overflow-y-auto">
      <section
        className={`bg-white rounded-md border border-slate-200 ${
          wide ? 'max-w-[500px]' : 'max-w-[440px]'
        } w-full max-h-[calc(100vh-2rem)] p-6 shadow-xl overflow-y-auto m-auto`}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
      >
        <div className="flex justify-between items-center mb-4">
          <h2 id="modal-title" className={titleClassName || 'text-lg font-semibold text-slate-900'}>
            {title}
          </h2>
          <button
            type="button"
            className="border-0 bg-transparent text-slate-400 hover:text-slate-600 text-2xl leading-none cursor-pointer p-1"
            aria-label="Đóng hộp thoại"
            onClick={onClose}
            disabled={closeDisabled}
          >
            ×
          </button>
        </div>
        {children}
      </section>
    </div>
  );
}
