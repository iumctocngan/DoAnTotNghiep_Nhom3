import React, { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { sessionApi } from '../../api/sessionApi';
import { classApi } from '../../api/classApi';
import { useAuth } from '../../context/AuthContext';
import { formatDateDisplay, formatTimeDisplay } from '../../utils/date';
import { AttendanceModal } from './AttendanceModal';
import { SessionModal } from './SessionModal';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { useToast } from '../../components/common/ToastProvider';
import type { SessionResponse } from '../../types/session';
import type { ApiError } from '../../types/auth';

export const SessionListPage: React.FC = () => {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const showToast = useToast();

  const [selectedClassId, setSelectedClassId] = useState<number | ''>('');
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Modals state
  const [attendanceSessionId, setAttendanceSessionId] = useState<number | null>(null);
  const [isSessionModalOpen, setIsSessionModalOpen] = useState(false);
  const [editingSession, setEditingSession] = useState<SessionResponse | null>(null);
  const [confirmDialog, setConfirmDialog] = useState<{
    title: string;
    message: string;
    onConfirm: () => void;
    isDangerous?: boolean;
  } | null>(null);

  // Danh sách lớp để lọc
  const { data: classes } = useQuery({
    queryKey: ['classes'],
    queryFn: () => classApi.getClasses(),
  });

  // TanStack Query lấy danh sách buổi học
  const {
    data: pagedData,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: ['sessions', { classId: selectedClassId, fromDate, toDate, page, pageSize }],
    queryFn: () =>
      sessionApi.getSessions({
        classId: selectedClassId ? Number(selectedClassId) : undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        page,
        pageSize,
      }),
    placeholderData: keepPreviousData,
  });

  // Hủy buổi học mutation
  const cancelSessionMutation = useMutation({
    mutationFn: (id: number) => sessionApi.cancelSession(id),
    onSuccess: () => {
      showToast('Đã hủy buổi học!', 'success');
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      showToast(apiErr.message || 'Lỗi khi hủy buổi học.', 'error');
    },
  });

  // Hoàn tất buổi học mutation
  const completeSessionMutation = useMutation({
    mutationFn: (id: number) => sessionApi.completeSession(id),
    onSuccess: () => {
      showToast('Đã hoàn tất buổi học!', 'success');
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      showToast(apiErr.message || 'Lỗi khi hoàn tất buổi học. Hãy đảm bảo đã điểm danh đầy đủ học sinh.', 'error');
    },
  });

  const isAdmin = user?.role === 'Admin';
  const canAttendance = user?.role === 'Admin' || user?.role === 'Teacher';
  const totalPages = pagedData ? Math.ceil(pagedData.totalItems / pageSize) : 1;

  const handleCreate = () => {
    setEditingSession(null);
    setIsSessionModalOpen(true);
  };

  const handleEdit = (session: SessionResponse) => {
    setEditingSession(session);
    setIsSessionModalOpen(true);
  };

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-bold text-slate-900">Quản lý Buổi học & Điểm danh</h1>
          <p className="text-sm text-slate-600">
            Theo dõi kế hoạch giảng dạy, buổi học theo lớp và thực hiện điểm danh học viên.
          </p>
        </div>

        {isAdmin && (
          <button type="button" className="btn btn-primary" onClick={handleCreate}>
            + Thêm buổi học mới
          </button>
        )}
      </div>

      {/* Bộ lọc */}
      <div className="card p-4">
        <div className="grid grid-cols-1 sm:grid-cols-4 gap-3">
          <div className="sm:col-span-2">
            <label htmlFor="filterClass" className="text-xs uppercase text-slate-500 font-semibold mb-1">
              Lọc theo lớp học
            </label>
            <select
              id="filterClass"
              value={selectedClassId}
              onChange={(e) => {
                setSelectedClassId(e.target.value ? Number(e.target.value) : '');
                setPage(1);
              }}
            >
              <option value="">-- Tất cả các lớp --</option>
              {classes?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.tenLop} ({c.maLop})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="fromDate" className="text-xs uppercase text-slate-500 font-semibold mb-1">
              Từ ngày
            </label>
            <input
              id="fromDate"
              type="date"
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                setPage(1);
              }}
            />
          </div>

          <div>
            <label htmlFor="toDate" className="text-xs uppercase text-slate-500 font-semibold mb-1">
              Đến ngày
            </label>
            <input
              id="toDate"
              type="date"
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                setPage(1);
              }}
            />
          </div>
        </div>
      </div>

      {/* Danh sách buổi học */}
      <div className="card !p-0 overflow-hidden">
        {isLoading ? (
          <div className="p-8 text-center text-slate-500">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-sky-600 border-t-transparent mb-3" />
            <p>Đang tải danh sách buổi học...</p>
          </div>
        ) : isError ? (
          <div className="p-6 text-center text-red-600">
            <p>Đã xảy ra lỗi: {(error as unknown as ApiError)?.message || 'Vui lòng thử lại.'}</p>
          </div>
        ) : !pagedData || pagedData.items.length === 0 ? (
          <div className="p-8 text-center text-slate-500">
            <p className="font-medium text-slate-700 mb-1">Chưa có buổi học nào</p>
            <p className="text-sm">Hãy thử chọn khoảng thời gian khác hoặc tạo buổi học mới.</p>
          </div>
        ) : (
          <div className="table-container">
            <table className="table-custom">
              <thead>
                <tr>
                  <th>Lớp học</th>
                  <th>Bài học</th>
                  <th>Ngày học</th>
                  <th>Thời gian</th>
                  <th>Trạng thái</th>
                  <th>Ghi chú</th>
                  <th className="text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {pagedData.items.map((s) => (
                  <tr key={s.id}>
                    <td>
                      <div className="font-semibold text-slate-900">{s.className}</div>
                      <div className="text-xs font-mono text-slate-500">{s.classCode}</div>
                    </td>
                    <td>
                      {s.lessonName ? (
                        <div>
                          <div className="font-medium text-slate-800">{s.lessonName}</div>
                          <div className="text-xs text-slate-400">{s.lessonCode}</div>
                        </div>
                      ) : (
                        <span className="text-xs text-slate-400 italic">Chưa gắn bài</span>
                      )}
                    </td>
                    <td className="font-medium">{formatDateDisplay(s.sessionDate)}</td>
                    <td className="text-sm">
                      {formatTimeDisplay(s.startTime)} - {formatTimeDisplay(s.endTime)}
                    </td>
                    <td>
                      {s.status === 1 && <span className="badge badge-teacher">Đã lên lịch</span>}
                      {s.status === 2 && <span className="badge badge-active">Hoàn tất</span>}
                      {s.status === 3 && <span className="badge badge-danger">Đã hủy</span>}
                    </td>
                    <td className="text-xs text-slate-500 max-w-[180px] truncate" title={s.note || ''}>
                      {s.note || '—'}
                    </td>
                    <td className="text-right whitespace-nowrap">
                      <div className="inline-flex items-center gap-1.5">
                        {canAttendance && s.status !== 3 && (
                          <button
                            type="button"
                            className="btn btn-secondary text-xs !py-1 !px-2.5 text-sky-700 font-semibold border-sky-300 hover:bg-sky-50"
                            onClick={() => setAttendanceSessionId(s.id)}
                          >
                            📝 Điểm danh
                          </button>
                        )}

                        {canAttendance && s.status === 1 && (
                          <button
                            type="button"
                            className="btn btn-secondary text-xs !py-1 !px-2 text-emerald-700 hover:bg-emerald-50"
                            onClick={() =>
                              setConfirmDialog({
                                title: 'Hoàn tất buổi học',
                                message: `Xác nhận hoàn tất buổi học "${s.className}" ngày ${formatDateDisplay(s.sessionDate)}? Lưu ý: Cần hoàn tất điểm danh trước khi đóng buổi học.`,
                                onConfirm: () => completeSessionMutation.mutate(s.id),
                              })
                            }
                          >
                            ✓ Hoàn tất
                          </button>
                        )}

                        {isAdmin && s.status === 1 && (
                          <>
                            <button
                              type="button"
                              className="btn btn-secondary text-xs !py-1 !px-2"
                              onClick={() => handleEdit(s)}
                            >
                              Sửa
                            </button>
                            <button
                              type="button"
                              className="btn btn-secondary text-xs !py-1 !px-2 text-red-600 hover:text-red-800"
                              onClick={() =>
                                setConfirmDialog({
                                  title: 'Hủy buổi học',
                                  message: `Bạn có chắc chắn muốn hủy buổi học lớp "${s.className}" ngày ${formatDateDisplay(s.sessionDate)}?`,
                                  onConfirm: () => cancelSessionMutation.mutate(s.id),
                                  isDangerous: true,
                                })
                              }
                            >
                              Hủy
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Phân trang */}
        {pagedData && pagedData.totalItems > 0 && (
          <div className="flex flex-wrap items-center justify-between p-4 border-t border-slate-200 text-sm gap-2">
            <span className="text-slate-600">
              Hiển thị <strong>{(page - 1) * pageSize + 1}</strong> -{' '}
              <strong>{Math.min(page * pageSize, pagedData.totalItems)}</strong> trên tổng số{' '}
              <strong>{pagedData.totalItems}</strong> buổi học
            </span>
            <div className="inline-flex items-center gap-2">
              <button
                type="button"
                className="btn btn-secondary text-xs !py-1 !px-3"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(p - 1, 1))}
              >
                Trang trước
              </button>
              <span className="text-slate-700 font-medium px-2">
                Trang {page} / {totalPages}
              </span>
              <button
                type="button"
                className="btn btn-secondary text-xs !py-1 !px-3"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(p + 1, totalPages))}
              >
                Trang sau
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Attendance Modal */}
      <AttendanceModal
        sessionId={attendanceSessionId}
        isOpen={attendanceSessionId !== null}
        onClose={() => setAttendanceSessionId(null)}
      />

      {/* Session Add/Edit Modal */}
      <SessionModal
        isOpen={isSessionModalOpen}
        onClose={() => setIsSessionModalOpen(false)}
        session={editingSession}
        defaultClassId={selectedClassId ? Number(selectedClassId) : undefined}
      />

      {confirmDialog && (
        <ConfirmDialog
          title={confirmDialog.title}
          message={confirmDialog.message}
          confirmText="Xác nhận"
          isPending={cancelSessionMutation.isPending || completeSessionMutation.isPending}
          isDangerous={confirmDialog.isDangerous}
          onCancel={() => setConfirmDialog(null)}
          onConfirm={() => {
            confirmDialog.onConfirm();
            setConfirmDialog(null);
          }}
        />
      )}
    </div>
  );
};
