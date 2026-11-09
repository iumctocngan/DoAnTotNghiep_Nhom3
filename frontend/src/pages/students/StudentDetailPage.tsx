import React from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { studentApi } from '../../api/studentApi';
import { invoiceApi } from '../../api/invoiceApi';
import { formatCurrency, formatDateDisplay } from '../../utils/date';
import type { ApiError } from '../../types/auth';

const genderLabel = (g?: number | null) => {
  if (g === 1) return 'Nam';
  if (g === 2) return 'Nữ';
  if (g === 3) return 'Khác';
  return '—';
};

export const StudentDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const studentId = id ? Number(id) : 0;

  const {
    data: student,
    isLoading: isStudentLoading,
    isError: isStudentError,
    error: studentError,
  } = useQuery({
    queryKey: ['students', studentId],
    queryFn: () => studentApi.getStudentById(studentId),
    enabled: !!studentId,
  });

  const { data: debtInfo, isLoading: isDebtLoading } = useQuery({
    queryKey: ['student-debt', studentId],
    queryFn: () => invoiceApi.getStudentDebt(studentId),
    enabled: !!studentId,
  });

  if (isStudentLoading) {
    return (
      <div className="p-8 text-center text-slate-500">
        <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-sky-600 border-t-transparent mb-3" />
        <p>Đang tải thông tin học viên...</p>
      </div>
    );
  }

  if (isStudentError || !student) {
    return (
      <div className="p-6 text-center">
        <p className="text-red-600">{(studentError as unknown as ApiError)?.message || 'Không tìm thấy học viên.'}</p>
        <Link to="/hoc-vien" className="btn btn-secondary text-sm mt-4 inline-block">
          ← Quay lại danh sách
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-wrap items-center gap-3">
        <Link to="/hoc-vien" className="btn btn-secondary text-xs !py-1 !px-3">
          ← Quay lại
        </Link>
        <div>
          <h1 className="text-xl font-bold text-slate-900">{student.fullName}</h1>
          <p className="text-sm text-slate-500 font-mono">{student.studentCode}</p>
        </div>
        {student.isArchived ? (
          <span className="badge badge-inactive ml-auto">Đã lưu trữ</span>
        ) : (
          <span className="badge badge-active ml-auto">Đang học</span>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        {/* Thông tin cơ bản */}
        <div className="card space-y-3 lg:col-span-1">
          <h2 className="font-semibold text-slate-800 text-sm uppercase tracking-wide border-b border-slate-100 pb-2">
            Thông tin học viên
          </h2>
          <dl className="space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-slate-500">Mã học viên</dt>
              <dd className="font-mono font-medium">{student.studentCode}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-slate-500">Họ và tên</dt>
              <dd className="font-semibold">{student.fullName}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-slate-500">Ngày sinh</dt>
              <dd>{formatDateDisplay(student.dateOfBirth)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-slate-500">Giới tính</dt>
              <dd>{genderLabel(student.gender)}</dd>
            </div>
            {student.learningNote && (
              <div>
                <dt className="text-slate-500 mb-1">Ghi chú học tập</dt>
                <dd className="bg-amber-50 border border-amber-200 rounded p-2 text-xs text-amber-800">
                  {student.learningNote}
                </dd>
              </div>
            )}
          </dl>
        </div>

        {/* Thông tin học phí & công nợ */}
        <div className="card space-y-3 lg:col-span-2">
          <h2 className="font-semibold text-slate-800 text-sm uppercase tracking-wide border-b border-slate-100 pb-2">
            Tình trạng học phí
          </h2>

          {isDebtLoading ? (
            <div className="text-center py-4 text-slate-500 text-sm">Đang tải thông tin công nợ...</div>
          ) : !debtInfo ? (
            <div className="text-center py-4 text-slate-500 text-sm">Chưa có thông tin hóa đơn.</div>
          ) : (
            <>
              <div className="grid grid-cols-3 gap-3 text-center">
                <div className="bg-slate-50 rounded p-3">
                  <div className="text-xs text-slate-500 mb-1">Tổng phải trả</div>
                  <div className="font-bold text-slate-900">{formatCurrency(debtInfo.tongPhaiTra)}</div>
                </div>
                <div className="bg-emerald-50 rounded p-3">
                  <div className="text-xs text-slate-500 mb-1">Đã thanh toán</div>
                  <div className="font-bold text-emerald-700">{formatCurrency(debtInfo.tongDaXacNhan)}</div>
                </div>
                <div className={`rounded p-3 ${debtInfo.conNo > 0 ? 'bg-red-50' : 'bg-slate-50'}`}>
                  <div className="text-xs text-slate-500 mb-1">Còn nợ</div>
                  <div className={`font-bold ${debtInfo.conNo > 0 ? 'text-red-700' : 'text-slate-600'}`}>
                    {formatCurrency(debtInfo.conNo)}
                  </div>
                </div>
              </div>

              {debtInfo.coHoaDonQuaHan && (
                <div className="bg-red-50 border border-red-200 rounded p-2 text-xs text-red-700 font-medium">
                  ⚠️ Học viên có hóa đơn quá hạn thanh toán!
                </div>
              )}

              {/* Danh sách hóa đơn */}
              {debtInfo.danhSachHoaDon && debtInfo.danhSachHoaDon.length > 0 && (
                <div className="mt-2">
                  <h3 className="text-xs font-semibold text-slate-700 uppercase tracking-wide mb-2">
                    Lịch sử hóa đơn
                  </h3>
                  <div className="table-container max-h-[260px] overflow-y-auto border border-slate-200 rounded">
                    <table className="table-custom text-xs">
                      <thead className="sticky top-0 bg-slate-100 z-10">
                        <tr>
                          <th>Số HĐ</th>
                          <th>Kỳ học</th>
                          <th>Phải trả</th>
                          <th>Còn nợ</th>
                          <th>Hạn nộp</th>
                          <th>Trạng thái</th>
                        </tr>
                      </thead>
                      <tbody>
                        {debtInfo.danhSachHoaDon.map((inv) => (
                          <tr key={inv.id}>
                            <td className="font-mono font-bold">{inv.soHoaDon}</td>
                            <td>
                              {formatDateDisplay(inv.ngayBatDauKy)} → {formatDateDisplay(inv.ngayKetThucKy)}
                            </td>
                            <td className="font-semibold">{formatCurrency(inv.soTienPhaiTra)}</td>
                            <td className={inv.conNo > 0 ? 'text-red-700 font-bold' : 'text-slate-500'}>
                              {formatCurrency(inv.conNo)}
                            </td>
                            <td>{formatDateDisplay(inv.ngayDenHan)}</td>
                            <td>
                              {inv.trangThai === 1 && <span className="badge badge-admin">Phát hành</span>}
                              {inv.trangThai === 2 && <span className="badge badge-inactive">Đã hủy</span>}
                              {inv.trangThai === 3 && <span className="badge badge-teacher">Nháp</span>}
                              {inv.trangThai === 4 && <span className="badge badge-accountant">Một phần</span>}
                              {inv.trangThai === 5 && <span className="badge badge-active">Đã thu đủ</span>}
                              {inv.trangThai === 6 && <span className="badge badge-danger">Quá hạn</span>}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
};
