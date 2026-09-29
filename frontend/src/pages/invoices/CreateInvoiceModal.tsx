import React, { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { invoiceApi } from '../../api/invoiceApi';
import { classApi } from '../../api/classApi';
import { studentApi } from '../../api/studentApi';
import { Modal } from '../../components/common/Modal';
import { useToast } from '../../components/common/ToastProvider';
import type { ApiError } from '../../types/auth';

interface CreateInvoiceModalProps {
  isOpen: boolean;
  onClose: () => void;
  defaultEnrollmentId?: number;
}

export const CreateInvoiceModal: React.FC<CreateInvoiceModalProps> = ({
  isOpen,
  onClose,
  defaultEnrollmentId,
}) => {
  const queryClient = useQueryClient();
  const showToast = useToast();

  const [selectedClassId, setSelectedClassId] = useState<number | ''>('');
  const [enrollmentId, setEnrollmentId] = useState<number | ''>(defaultEnrollmentId || '');
  const [periodStart, setPeriodStart] = useState(() => new Date().toISOString().substring(0, 10));
  const [amountDue, setAmountDue] = useState<number | ''>(3000000);
  const [dueDate, setDueDate] = useState('');
  const [note, setNote] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Danh sách các lớp học
  const { data: classes } = useQuery({
    queryKey: ['classes'],
    queryFn: () => classApi.getClasses(),
    enabled: isOpen && !defaultEnrollmentId,
  });

  // Danh sách ghi danh trong lớp đã chọn
  const { data: enrollments } = useQuery({
    queryKey: ['enrollments', { lopId: selectedClassId }],
    queryFn: () => classApi.getEnrollments({ lopId: Number(selectedClassId) }),
    enabled: isOpen && !!selectedClassId,
  });

  // Danh sách học sinh để map tên
  const { data: studentsData } = useQuery({
    queryKey: ['students', 'lookup'],
    queryFn: () => studentApi.getStudents({ pageSize: 100 }),
    enabled: isOpen,
  });

  // Tự động tính hạn nộp (ví dụ 15 ngày sau ngày bắt đầu kỳ hoặc bằng kỳ kết thúc)
  useEffect(() => {
    if (periodStart) {
      const d = new Date(periodStart);
      d.setDate(d.getDate() + 15);
      setDueDate(d.toISOString().substring(0, 10));
    }
  }, [periodStart]);

  useEffect(() => {
    if (defaultEnrollmentId) {
      setEnrollmentId(defaultEnrollmentId);
    }
    setErrorMessage(null);
  }, [defaultEnrollmentId, isOpen]);

  const mutation = useMutation({
    mutationFn: () => {
      if (!enrollmentId) throw new Error('Vui lòng chọn ghi danh học viên.');
      return invoiceApi.createInvoice({
        enrollmentId: Number(enrollmentId),
        periodStart,
        amountDue: Number(amountDue),
        dueDate,
        note: note.trim() || null,
      });
    },
    onSuccess: (data) => {
      showToast(`Tạo hóa đơn ${data.soHoaDon} thành công!`, 'success');
      queryClient.invalidateQueries({ queryKey: ['invoices'] });
      queryClient.invalidateQueries({ queryKey: ['debtors'] });
      queryClient.invalidateQueries({ queryKey: ['students'] });
      onClose();
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setErrorMessage(apiErr.message || 'Lỗi khi tạo hóa đơn. Kỳ học không được chồng lấn với hóa đơn trước.');
    },
  });

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!enrollmentId) {
      setErrorMessage('Vui lòng chọn học viên (ghi danh) cần lập hóa đơn.');
      return;
    }
    if (!periodStart) {
      setErrorMessage('Vui lòng chọn ngày bắt đầu kỳ học phí.');
      return;
    }
    if (!amountDue || Number(amountDue) <= 0) {
      setErrorMessage('Số tiền học phí phải lớn hơn 0.');
      return;
    }
    if (!dueDate) {
      setErrorMessage('Vui lòng chọn hạn thanh toán.');
      return;
    }

    setErrorMessage(null);
    mutation.mutate();
  };

  const activeEnrollments = enrollments?.filter((e) => e.trangThai === 1) || [];

  return (
    <Modal
      title="Lập hóa đơn học phí mới"
      onClose={onClose}
      closeDisabled={mutation.isPending}
      wide
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

        {!defaultEnrollmentId && (
          <>
            <div>
              <label htmlFor="invClass">
                1. Chọn lớp học <span className="text-red-500">*</span>
              </label>
              <select
                id="invClass"
                value={selectedClassId}
                onChange={(e) => {
                  setSelectedClassId(e.target.value ? Number(e.target.value) : '');
                  setEnrollmentId('');
                }}
                disabled={mutation.isPending}
                required
              >
                <option value="">-- Chọn lớp học --</option>
                {classes?.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.tenLop} ({c.maLop})
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label htmlFor="invEnroll">
                2. Chọn học viên ghi danh <span className="text-red-500">*</span>
              </label>
              <select
                id="invEnroll"
                value={enrollmentId}
                onChange={(e) => setEnrollmentId(e.target.value ? Number(e.target.value) : '')}
                disabled={mutation.isPending || !selectedClassId}
                required
              >
                <option value="">-- Chọn học viên đang theo học --</option>
                {activeEnrollments.map((enr) => {
                  const student = studentsData?.items.find((s) => s.id === enr.hocVienId);
                  return (
                    <option key={enr.id} value={enr.id}>
                      {student ? `${student.fullName} (${student.studentCode})` : 'Học viên chưa có thông tin'}
                    </option>
                  );
                })}
              </select>
              {selectedClassId && activeEnrollments.length === 0 && (
                <p className="text-xs text-amber-600 mt-1">Lớp này hiện không có học viên nào ở trạng thái Đang học.</p>
              )}
            </div>
          </>
        )}

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label htmlFor="pStart">
              Ngày bắt đầu kỳ <span className="text-red-500">*</span>
            </label>
            <input
              id="pStart"
              type="date"
              value={periodStart}
              onChange={(e) => setPeriodStart(e.target.value)}
              disabled={mutation.isPending}
              required
            />
            <span className="text-[11px] text-slate-500 mt-0.5 block">
              * Kỳ học 6 tháng tự tính
            </span>
          </div>

          <div>
            <label htmlFor="dDate">
              Hạn thanh toán <span className="text-red-500">*</span>
            </label>
            <input
              id="dDate"
              type="date"
              value={dueDate}
              onChange={(e) => setDueDate(e.target.value)}
              disabled={mutation.isPending}
              required
            />
          </div>
        </div>

        <div>
          <label htmlFor="amt">
            Số tiền học phí (VNĐ) <span className="text-red-500">*</span>
          </label>
          <input
            id="amt"
            type="number"
            min="1000"
            step="10000"
            value={amountDue}
            onChange={(e) => setAmountDue(e.target.value ? Number(e.target.value) : '')}
            disabled={mutation.isPending}
            required
            placeholder="VD: 3000000"
          />
        </div>

        <div>
          <label htmlFor="invNote">Ghi chú hóa đơn</label>
          <textarea
            id="invNote"
            rows={2}
            placeholder="Học phí khóa cơ bản, ưu đãi đầu khóa..."
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
            {mutation.isPending ? 'Đang tạo...' : 'Phát hành hóa đơn'}
          </button>
        </div>
      </form>
    </Modal>
  );
};
