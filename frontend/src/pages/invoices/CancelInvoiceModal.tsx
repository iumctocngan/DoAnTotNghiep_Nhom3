import React, { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { invoiceApi } from '../../api/invoiceApi';
import { Modal } from '../../components/common/Modal';
import { useToast } from '../../components/common/ToastProvider';
import type { ApiError } from '../../types/auth';
import type { InvoiceResponse } from '../../types/invoice';

interface CancelInvoiceModalProps {
  invoice: InvoiceResponse | null;
  isOpen: boolean;
  onClose: () => void;
}

export const CancelInvoiceModal: React.FC<CancelInvoiceModalProps> = ({
  invoice,
  isOpen,
  onClose,
}) => {
  const queryClient = useQueryClient();
  const showToast = useToast();

  const [reason, setReason] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => {
      if (!invoice) throw new Error('Không có thông tin hóa đơn.');
      return invoiceApi.cancelInvoice(invoice.id, { lyDoHuy: reason.trim() });
    },
    onSuccess: () => {
      showToast('Đã hủy hóa đơn học phí thành công!', 'success');
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
      queryClient.invalidateQueries({ queryKey: ['debtors'] });
      queryClient.invalidateQueries({ queryKey: ['students'] });
      onClose();
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setErrorMessage(
        apiErr.message || 'Lỗi khi hủy hóa đơn. Hóa đơn đã có thanh toán xác nhận không thể hủy.'
      );
    },
  });

  if (!isOpen || !invoice) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setErrorMessage('Vui lòng nhập lý do hủy hóa đơn (bắt buộc để lưu nhật ký kiểm toán).');
      return;
    }

    setErrorMessage(null);
    mutation.mutate();
  };

  return (
    <Modal
      title={`Hủy hóa đơn học phí: ${invoice.soHoaDon}`}
      onClose={onClose}
      closeDisabled={mutation.isPending}
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

        <div className="bg-amber-50 p-3 rounded border border-amber-200 text-xs text-amber-900">
          ⚠️ Hóa đơn sẽ được chuyển sang trạng thái <strong>Đã hủy</strong> và được ghi vết trong
          nhật ký kiểm toán hệ thống. Dữ liệu sẽ không bị xóa vật lý.
        </div>

        <div>
          <label htmlFor="cancelReason">
            Lý do hủy hóa đơn <span className="text-red-500">*</span>
          </label>
          <textarea
            id="cancelReason"
            rows={3}
            placeholder="Lập sai số tiền, học sinh thôi học trước kỳ, trùng lặp..."
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={mutation.isPending}
            required
            maxLength={300}
          />
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-slate-200">
          <button
            type="button"
            className="btn btn-secondary text-xs"
            onClick={onClose}
            disabled={mutation.isPending}
          >
            Đóng
          </button>
          <button
            type="submit"
            className="btn btn-danger text-xs"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Đang hủy...' : 'Xác nhận hủy hóa đơn'}
          </button>
        </div>
      </form>
    </Modal>
  );
};
