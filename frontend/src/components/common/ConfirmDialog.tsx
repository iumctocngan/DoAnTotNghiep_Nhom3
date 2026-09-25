import { Modal } from './Modal';

interface ConfirmDialogProps {
  title: string;
  message: string;
  confirmText?: string;
  isDangerous?: boolean;
  isPending?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({
  title,
  message,
  confirmText = 'Xác nhận',
  isDangerous = true,
  isPending = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Modal title={title} onClose={onCancel} closeDisabled={isPending}>
      <p className="mb-5 text-sm text-slate-600 leading-relaxed">{message}</p>
      <div className="flex justify-end gap-2 mt-5">
        <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={isPending}>
          Hủy
        </button>
        <button
          type="button"
          className={`btn ${isDangerous ? 'btn-danger' : 'btn-primary'}`}
          onClick={onConfirm}
          disabled={isPending}
        >
          {isPending ? 'Đang xử lý...' : confirmText}
        </button>
      </div>
    </Modal>
  );
}
