import { MaterialIcon } from '../../components/common/MaterialIcon';
import React, { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { invoiceApi } from '../../api/invoiceApi';
import { Modal } from '../../components/common/Modal';
import { useToast } from '../../components/common/ToastProvider';
import type { ApiError } from '../../types/auth';
import type { PaymentResponse } from '../../types/invoice';

interface CancelPaymentModalProps {
  payment: PaymentResponse | null;
  isOpen: boolean;
  onClose: () => void;
}

export const CancelPaymentModal: React.FC<CancelPaymentModalProps> = ({
  payment,
  isOpen,
  onClose,
}) => {
  const queryClient = useQueryClient();
  const showToast = useToast();

  const [reason, setReason] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => {
      if (!payment) throw new Error('Không có thông tin phiếu thu.');
      return invoiceApi.cancelPayment(payment.id, { lyDoHuy: reason.trim() });
    },
    onSuccess: () => {
      showToast('Đã hủy phiếu thu / khoản thanh toán thành công!', 'success');
      queryClient.invalidateQueries({ queryKey: ['payments'] });
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
      queryClient.invalidateQueries({ queryKey: ['debtors'] });
      onClose();
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setErrorMessage(apiErr.message || 'Lỗi khi hủy khoản thanh toán.');
    },
  });

  if (!isOpen || !payment) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setErrorMessage('Vui lòng nhập lý do hủy phiếu thu.');
      return;
    }

    setErrorMessage(null);
    mutation.mutate();
  };

  return (
    <Modal
      title={`Hủy phiếu thu: ${payment.receiptNumber}`}
      onClose={onClose}
      closeDisabled={mutation.isPending}
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

        <div className="bg-amber-50 p-3 rounded border border-amber-200 text-xs text-amber-900">
          <MaterialIcon name="warning" /> Khi hủy phiếu thu, số tiền thanh toán này sẽ được trừ khỏi tổng đã nộp của hóa đơn{' '}
          <strong>{payment.invoiceNumber}</strong>, và công nợ của học viên sẽ tăng trở lại tương ứng.
        </div>

        <div>
          <label htmlFor="cancelPaymentReason">
            Lý do hủy phiếu thu <span className="text-red-500">*</span>
          </label>
          <textarea
            id="cancelPaymentReason"
            rows={3}
            placeholder="Nhập sai số tiền, sai thông tin giao dịch, hoàn tiền..."
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
            {mutation.isPending ? 'Đang hủy...' : 'Xác nhận hủy phiếu thu'}
          </button>
        </div>
      </form>
    </Modal>
  );
};
