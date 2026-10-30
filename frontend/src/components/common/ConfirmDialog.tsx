import { Modal } from './Modal';

interface ConfirmDialogProps {
  title: string;
  message: string;
  confirmText?: string;
  isDangerous?: boolean;
  confirmVariant?: 'primary' | 'danger' | 'success';
  isPending?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({
  title,
  message,
  confirmText = 'Xác nhận',
  isDangerous = true,
  confirmVariant,
  isPending = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const getButtonClass = () => {
    if (confirmVariant === 'success') return 'btn-success';
    if (confirmVariant === 'danger') return 'btn-danger';
    if (confirmVariant === 'primary') return 'btn-primary';
    return isDangerous ? 'btn-danger' : 'btn-primary';
  };

  return (
    <Modal title={title} onClose={onCancel} closeDisabled={isPending}>
      <p className="mb-5 text-sm text-slate-600 leading-relaxed">{message}</p>
      <div className="flex justify-end gap-2 mt-5">
        <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={isPending}>
          Hủy
        </button>
        <button
          type="button"
          className={`btn ${getButtonClass()}`}
          onClick={onConfirm}
          disabled={isPending}
        >
          {isPending ? 'Đang xử lý...' : confirmText}
        </button>
      </div>
    </Modal>
  );
}
