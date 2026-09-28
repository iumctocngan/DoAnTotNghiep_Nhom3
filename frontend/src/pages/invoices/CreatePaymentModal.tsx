import React, { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { invoiceApi } from '../../api/invoiceApi';
import { Modal } from '../../components/common/Modal';
import { useToast } from '../../components/common/ToastProvider';
import { formatCurrency } from '../../utils/date';
import type { ApiError } from '../../types/auth';
import type { InvoiceResponse, PaymentMethod } from '../../types/invoice';

interface CreatePaymentModalProps {
  invoice: InvoiceResponse | null;
  isOpen: boolean;
  onClose: () => void;
}

export const CreatePaymentModal: React.FC<CreatePaymentModalProps> = ({
  invoice,
  isOpen,
  onClose,
}) => {
  const queryClient = useQueryClient();
  const showToast = useToast();

  const [amount, setAmount] = useState<number | ''>('');
  const [method, setMethod] = useState<PaymentMethod>(1); // 1: Cash, 2: BankTransfer
  const [paidAtDate, setPaidAtDate] = useState(() => new Date().toISOString().substring(0, 10));
  const [paidAtTime, setPaidAtTime] = useState(() => {
    const now = new Date();
    return `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
  });
  const [note, setNote] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (invoice) {
      setAmount(invoice.conNo > 0 ? invoice.conNo : invoice.soTienPhaiTra);
      setMethod(1);
      const now = new Date();
      setPaidAtDate(now.toISOString().substring(0, 10));
      setPaidAtTime(
        `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
      );
      setNote('');
    }
    setErrorMessage(null);
  }, [invoice, isOpen]);

  const mutation = useMutation({
    mutationFn: () => {
      if (!invoice) throw new Error('Không có thông tin hóa đơn.');
      const paidAtIso = new Date(`${paidAtDate}T${paidAtTime}:00`).toISOString();
      return invoiceApi.createPayment(invoice.id, {
        amount: Number(amount),
        paidAt: paidAtIso,
        method,
        note: note.trim() || null,
      });
    },
    onSuccess: (data) => {
      showToast(
        `Ghi nhận thanh toán thành công! Mã phiếu thu: ${data.receiptNumber}`,
        'success'
      );
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
      queryClient.invalidateQueries({ queryKey: ['payments'] });
      queryClient.invalidateQueries({ queryKey: ['debtors'] });
      queryClient.invalidateQueries({ queryKey: ['students'] });
      onClose();
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setErrorMessage(
        apiErr.message || 'Lỗi ghi nhận thanh toán. Số tiền không được vượt quá số nợ còn lại.'
      );
    },
  });

  if (!isOpen || !invoice) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!amount || Number(amount) <= 0) {
      setErrorMessage('Số tiền thanh toán phải lớn hơn 0.');
      return;
    }
    if (Number(amount) > invoice.conNo) {
      setErrorMessage(
        `Số tiền thanh toán (${formatCurrency(Number(amount))}) không được lớn hơn số còn nợ (${formatCurrency(invoice.conNo)}).`
      );
      return;
    }

    setErrorMessage(null);
    mutation.mutate();
  };

  return (
    <Modal
      title={`Thu tiền học phí: ${invoice.soHoaDon}`}
      onClose={onClose}
      closeDisabled={mutation.isPending}
      wide
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

        {/* Tóm tắt hóa đơn */}
        <div className="bg-slate-50 p-3 rounded border border-slate-200 text-xs text-slate-700 space-y-1">
          <div className="flex justify-between">
            <span>Học viên:</span>
            <strong>{invoice.tenHocVien} ({invoice.maHocVien})</strong>
          </div>
          <div className="flex justify-between">
            <span>Tổng số tiền hóa đơn:</span>
            <span>{formatCurrency(invoice.soTienPhaiTra)}</span>
          </div>
          <div className="flex justify-between">
            <span>Đã thanh toán trước đó:</span>
            <span className="text-emerald-700 font-semibold">{formatCurrency(invoice.tongDaXacNhan)}</span>
          </div>
          <div className="flex justify-between pt-1 border-t border-slate-200 text-sm">
            <span className="font-semibold text-slate-900">Số tiền còn nợ:</span>
            <strong className="text-amber-700">{formatCurrency(invoice.conNo)}</strong>
          </div>
        </div>

        <div>
          <label htmlFor="payAmount">
            Số tiền thu lần này (VNĐ) <span className="text-red-500">*</span>
          </label>
          <input
            id="payAmount"
            type="number"
            min="1000"
            max={invoice.conNo}
            step="10000"
            value={amount}
            onChange={(e) => setAmount(e.target.value ? Number(e.target.value) : '')}
            disabled={mutation.isPending}
            required
          />
          <div className="flex gap-2 mt-1">
            <button
              type="button"
              className="text-xs text-sky-600 hover:underline"
              onClick={() => setAmount(invoice.conNo)}
            >
              Thu đủ toàn bộ ({formatCurrency(invoice.conNo)})
            </button>
            {invoice.conNo > 1000000 && (
              <button
                type="button"
                className="text-xs text-sky-600 hover:underline"
                onClick={() => setAmount(Math.floor(invoice.conNo / 2))}
              >
                Thu 50% ({formatCurrency(Math.floor(invoice.conNo / 2))})
              </button>
            )}
          </div>
        </div>

        <div>
          <label htmlFor="payMethod">
            Hình thức thanh toán <span className="text-red-500">*</span>
          </label>
          <select
            id="payMethod"
            value={method}
            onChange={(e) => setMethod(Number(e.target.value) as PaymentMethod)}
            disabled={mutation.isPending}
          >
            <option value="1">💵 Tiền mặt (Cash)</option>
            <option value="2">🏦 Chuyển khoản ngân hàng (Bank Transfer)</option>
          </select>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label htmlFor="pDate">Ngày thu</label>
            <input
              id="pDate"
              type="date"
              value={paidAtDate}
              onChange={(e) => setPaidAtDate(e.target.value)}
              disabled={mutation.isPending}
              required
            />
          </div>
          <div>
            <label htmlFor="pTime">Giờ thu</label>
            <input
              id="pTime"
              type="time"
              value={paidAtTime}
              onChange={(e) => setPaidAtTime(e.target.value)}
              disabled={mutation.isPending}
              required
            />
          </div>
        </div>

        <div>
          <label htmlFor="payNote">Ghi chú phiếu thu</label>
          <input
            id="payNote"
            type="text"
            placeholder="Mã giao dịch ngân hàng, người nộp thay..."
            value={note}
            onChange={(e) => setNote(e.target.value)}
            disabled={mutation.isPending}
            maxLength={200}
          />
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-slate-200">
          <button
            type="button"
            className="btn btn-secondary text-xs"
            onClick={onClose}
            disabled={mutation.isPending}
          >
            Hủy
          </button>
          <button
            type="submit"
            className="btn btn-primary text-xs"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Đang xử lý...' : 'Xác nhận thu tiền & Tạo phiếu thu'}
          </button>
        </div>
      </form>
    </Modal>
  );
};
