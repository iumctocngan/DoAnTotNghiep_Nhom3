import { MaterialIcon } from '../../components/common/MaterialIcon';
import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { sessionApi } from '../../api/sessionApi';
import { Modal } from '../../components/common/Modal';
import { useToast } from '../../components/common/ToastProvider';
import { formatDateDisplay, formatTimeDisplay } from '../../utils/date';
import type { ApiError } from '../../types/auth';
import type { AttendanceStatus, SaveAttendanceItem } from '../../types/session';

interface AttendanceModalProps {
  sessionId: number | null;
  isOpen: boolean;
  onClose: () => void;
}

export const AttendanceModal: React.FC<AttendanceModalProps> = ({ sessionId, isOpen, onClose }) => {
  const queryClient = useQueryClient();
  const showToast = useToast();

  const [attendanceRecords, setAttendanceRecords] = useState<
    Record<number, { status: AttendanceStatus; note: string }>
  >({});
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const {
    data: attendanceData,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: ['sessions', sessionId, 'attendance'],
    queryFn: () => sessionApi.getAttendance(sessionId!),
    enabled: isOpen && !!sessionId,
  });

  // Đồng bộ state khi tải dữ liệu điểm danh
  useEffect(() => {
    if (attendanceData?.danhSachHocVien) {
      const initial: Record<number, { status: AttendanceStatus; note: string }> = {};
      for (const item of attendanceData.danhSachHocVien) {
        initial[item.enrollmentId] = {
          // Mặc định nếu chưa điểm danh là Có mặt (1) hoặc giữ trạng thái cũ
          status: item.status ?? 1,
          note: item.note ?? '',
        };
      }
      setAttendanceRecords(initial);
      setErrorMessage(null);
    }
  }, [attendanceData]);

  const saveMutation = useMutation({
    mutationFn: async (shouldComplete: boolean) => {
      if (!sessionId || !attendanceData) return;
      const items: SaveAttendanceItem[] = attendanceData.danhSachHocVien.map((item) => ({
        enrollmentId: item.enrollmentId,
        status: attendanceRecords[item.enrollmentId]?.status ?? 1,
        note: attendanceRecords[item.enrollmentId]?.note?.trim() || null,
      }));

      await sessionApi.saveAttendance(sessionId, { items });

      if (shouldComplete) {
        await sessionApi.completeSession(sessionId);
      }
    },
    onSuccess: (_, shouldComplete) => {
      showToast(
        shouldComplete
          ? 'Đã lưu điểm danh và hoàn tất buổi học!'
          : 'Lưu điểm danh thành công!',
        'success'
      );
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['sessions', sessionId] });
      queryClient.invalidateQueries({ queryKey: ['teacher-schedule'] });
      onClose();
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setErrorMessage(apiErr.message || 'Lỗi khi lưu điểm danh. Vui lòng kiểm tra lại.');
    },
  });

  if (!isOpen) return null;

  const isReadOnly = attendanceData?.sessionStatus === 2 || attendanceData?.sessionStatus === 3;

  const setStatusAll = (status: AttendanceStatus) => {
    if (isReadOnly) return;
    setAttendanceRecords((prev) => {
      const next = { ...prev };
      for (const k of Object.keys(next)) {
        next[Number(k)] = { ...next[Number(k)], status };
      }
      return next;
    });
  };

  const handleStatusChange = (enrollmentId: number, status: AttendanceStatus) => {
    if (isReadOnly) return;
    setAttendanceRecords((prev) => ({
      ...prev,
      [enrollmentId]: {
        ...prev[enrollmentId],
        status,
      },
    }));
  };

  const handleNoteChange = (enrollmentId: number, note: string) => {
    if (isReadOnly) return;
    setAttendanceRecords((prev) => ({
      ...prev,
      [enrollmentId]: {
        ...prev[enrollmentId],
        note,
      },
    }));
  };

  // Thống kê nhanh
  const total = attendanceData?.danhSachHocVien.length ?? 0;
  const presentCount = Object.values(attendanceRecords).filter((r) => r.status === 1).length;
  const absentCount = Object.values(attendanceRecords).filter((r) => r.status === 2).length;

  return (
    <Modal
      title={
        attendanceData
          ? `Điểm danh: ${attendanceData.className} (${attendanceData.classCode})`
          : 'Điểm danh buổi học'
      }
      onClose={onClose}
      closeDisabled={saveMutation.isPending}
      wide
    >
      <div className="space-y-4">
        {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

        {isLoading ? (
          <div className="p-8 text-center text-slate-500">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-sky-600 border-t-transparent mb-3" />
            <p>Đang tải danh sách điểm danh...</p>
          </div>
        ) : isError ? (
          <div className="p-4 text-center text-red-600">
            <p>{(error as unknown as ApiError)?.message || 'Không thể tải danh sách điểm danh của buổi học.'}</p>
          </div>
        ) : !attendanceData ? null : (
          <>
            {/* Session info banner */}
            <div className="bg-slate-50 p-3 rounded border border-slate-200 text-xs text-slate-700 flex flex-wrap items-center justify-between gap-2">
              <div>
                <strong>Ngày học:</strong> {formatDateDisplay(attendanceData.sessionDate)} |{' '}
                <strong>Giờ:</strong> {formatTimeDisplay(attendanceData.startTime)} -{' '}
                {formatTimeDisplay(attendanceData.endTime)}
              </div>
              <div>
                {attendanceData.sessionStatus === 1 && (
                  <span className="badge badge-teacher">Đã lên lịch</span>
                )}
                {attendanceData.sessionStatus === 2 && (
                  <span className="badge badge-active">Buổi học đã hoàn tất</span>
                )}
                {attendanceData.sessionStatus === 3 && (
                  <span className="badge badge-danger">Buổi học đã bị hủy</span>
                )}
              </div>
            </div>

            {/* Thống kê & Nút thao tác nhanh */}
            <div className="flex flex-wrap items-center justify-between gap-3 bg-white p-2.5 rounded border border-slate-200">
              <div className="flex items-center gap-4 text-xs">
                <span>
                  Sĩ số: <strong>{total}</strong>
                </span>
                <span className="text-emerald-700 font-semibold">
                  <MaterialIcon name="check" /> Có mặt: {presentCount}
                </span>
                <span className="text-red-700 font-semibold">
                  <MaterialIcon name="close" /> Vắng: {absentCount}
                </span>
              </div>

              {!isReadOnly && (
                <div className="flex items-center gap-1.5">
                  <button
                    type="button"
                    className="btn btn-secondary text-xs !py-1 !px-2.5 text-emerald-700 border-emerald-300 hover:bg-emerald-50"
                    onClick={() => setStatusAll(1)}
                  >
                    <MaterialIcon name="check" /> Tất cả có mặt
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary text-xs !py-1 !px-2.5 text-red-700 border-red-300 hover:bg-red-50"
                    onClick={() => setStatusAll(2)}
                  >
                    <MaterialIcon name="close" /> Tất cả vắng
                  </button>
                </div>
              )}
            </div>

            {/* Danh sách học viên */}
            <div className="table-container max-h-[360px] overflow-y-auto border border-slate-200 rounded">
              <table className="data-table text-xs">
                <thead className="sticky top-0 bg-slate-100 z-10">
                  <tr>
                    <th>STT</th>
                    <th>Mã HV</th>
                    <th>Họ và tên</th>
                    <th className="text-center w-[180px]">Trạng thái</th>
                    <th>Ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {attendanceData.danhSachHocVien.length === 0 ? (
                    <tr>
                      <td colSpan={5} className="text-center py-6 text-slate-500">
                        Lớp học chưa có học viên nào ghi danh đang hoạt động trong ngày này.
                      </td>
                    </tr>
                  ) : (
                    attendanceData.danhSachHocVien.map((item, idx) => {
                      const current = attendanceRecords[item.enrollmentId] ?? {
                        status: 1,
                        note: '',
                      };
                      return (
                        <tr
                          key={item.enrollmentId}
                          className={current.status === 2 ? 'bg-red-50/40' : ''}
                        >
                          <td className="text-slate-500">{idx + 1}</td>
                          <td className="font-mono font-medium">{item.studentCode}</td>
                          <td className="font-semibold text-slate-900">{item.studentFullName}<Link className="block text-sky-700 underline text-xs mt-1" to={`/ghi-danh/${item.enrollmentId}/nhan-xet`}>Nhận xét học viên</Link></td>
                          <td className="text-center">
                            <div className="inline-flex rounded border border-slate-300 p-0.5 bg-white">
                              <button
                                type="button"
                                className={`px-2.5 py-1 text-xs font-semibold rounded transition-colors ${
                                  current.status === 1
                                    ? 'bg-emerald-600 text-white'
                                    : 'text-slate-600 hover:bg-slate-100'
                                }`}
                                onClick={() => handleStatusChange(item.enrollmentId, 1)}
                                disabled={isReadOnly}
                              >
                                Có mặt
                              </button>
                              <button
                                type="button"
                                className={`px-2.5 py-1 text-xs font-semibold rounded transition-colors ${
                                  current.status === 2
                                    ? 'bg-red-600 text-white'
                                    : 'text-slate-600 hover:bg-slate-100'
                                }`}
                                onClick={() => handleStatusChange(item.enrollmentId, 2)}
                                disabled={isReadOnly}
                              >
                                Vắng mặt
                              </button>
                            </div>
                          </td>
                          <td>
                            <input
                              type="text"
                              placeholder="Lý do, đi trễ, v.v..."
                              value={current.note}
                              onChange={(e) =>
                                handleNoteChange(item.enrollmentId, e.target.value)
                              }
                              disabled={isReadOnly}
                              className="!h-7 !text-xs !py-0"
                              maxLength={200}
                            />
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>

            {/* Footer Buttons */}
            <div className="flex flex-wrap items-center justify-between gap-2 pt-3 border-t border-slate-200">
              <button
                type="button"
                className="btn btn-secondary text-xs"
                onClick={onClose}
                disabled={saveMutation.isPending}
              >
                Đóng
              </button>

              {!isReadOnly && attendanceData.danhSachHocVien.length > 0 && (
                <div className="flex gap-2">
                  <button
                    type="button"
                    className="btn btn-secondary text-xs"
                    onClick={() => saveMutation.mutate(false)}
                    disabled={saveMutation.isPending}
                  >
                    {saveMutation.isPending ? 'Đang lưu...' : 'Lưu điểm danh'}
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary text-xs"
                    onClick={() => saveMutation.mutate(true)}
                    disabled={saveMutation.isPending}
                  >
                    {saveMutation.isPending ? 'Đang xử lý...' : 'Lưu & Hoàn tất buổi học'}
                  </button>
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </Modal>
  );
};
