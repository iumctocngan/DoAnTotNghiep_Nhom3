import React, { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { teacherApi } from '../../api/teacherApi';
import { staffApi } from '../../api/staffApi';
import { useAuth } from '../../context/AuthContext';
import type { ClassStatus } from '../../types/teacher';
import { formatDateDisplay } from '../../utils/date';
import { TeacherSelect } from '../../components/TeacherSelect';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';

export const TeacherClassesPage: React.FC = () => {
  const { user } = useAuth();
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>(user?.role === 'Teacher' ? user.userId : '');
  const [teacherSearch, setTeacherSearch] = useState('');
  const debouncedTeacherSearch = useDebouncedValue(teacherSearch);
  const [status, setStatus] = useState<ClassStatus | undefined>(undefined);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data: teachers, isLoading: isTeachersLoading, isFetching: isTeachersFetching, isError: isTeachersError } = useQuery({
    queryKey: ['staff', 'teachers', debouncedTeacherSearch],
    queryFn: () => staffApi.getStaff({ role: 'Teacher', search: debouncedTeacherSearch, pageSize: 100 }),
    enabled: user?.role === 'Admin',
    placeholderData: keepPreviousData,
  });

  const {
    data: pagedData,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: ['teacher-classes', selectedTeacherId, status, page, pageSize],
    queryFn: () => teacherApi.getClasses(selectedTeacherId, { status, page, pageSize }),
    enabled: !!selectedTeacherId,
  });

  const getDayOfWeekText = (day: number) => {
    const days = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];
    return days[day] ?? `Thứ ${day}`;
  };

  const getClassStatusBadge = (s: ClassStatus) => {
    switch (s) {
      case 1:
        return <span className="badge badge-admin">Chuẩn bị</span>;
      case 2:
        return <span className="badge badge-active">Đang học</span>;
      case 3:
        return <span className="badge badge-inactive">Đã kết thúc</span>;
      case 4:
        return <span className="badge badge-danger">Đã hủy</span>;
      default:
        return <span className="badge">—</span>;
    }
  };

  const totalPages = pagedData ? Math.ceil(pagedData.totalItems / pageSize) : 1;

  return (
    <div>
      <div className="mb-5">
        <h1 className="text-xl font-semibold mb-1 text-slate-900">
          Lớp học phụ trách
        </h1>
        <p className="text-sm text-slate-600">
          Danh sách các lớp học được phân công giảng dạy cho giáo viên.
        </p>
      </div>

      {/* Thanh bộ lọc */}
      <div className="card grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 p-4 mb-4 items-start">
        {user?.role === 'Admin' && (
          <div>
            <label htmlFor="teacherSelect" className="text-xs font-semibold text-slate-700 mb-1">
              Giáo viên:
            </label>
            <TeacherSelect
              id="teacherSelect"
              teachers={teachers?.items ?? []}
              value={selectedTeacherId}
              search={teacherSearch}
              isLoading={isTeachersLoading && !teachers}
              isSearching={isTeachersFetching || teacherSearch !== debouncedTeacherSearch}
              disabled={isTeachersError}
              onSearchChange={(value) => {
                setTeacherSearch(value);
                setPage(1);
              }}
              onChange={(teacherId) => {
                setSelectedTeacherId(teacherId);
                setPage(1);
              }}
            />
            {isTeachersError && <div className="alert alert-danger mt-2 mb-0" role="alert">Không tải được danh sách giáo viên. Tải lại trang để thử lại.</div>}
          </div>
        )}

        <div>
          <label htmlFor="classStatusFilter" className="text-xs font-semibold text-slate-700 mb-1">
            Trạng thái lớp:
          </label>
          <select
            id="classStatusFilter"
            value={status === undefined ? '' : status}
            onChange={(e) => {
              const val = e.target.value;
              setStatus(val === '' ? undefined : (Number(val) as ClassStatus));
              setPage(1);
            }}
          >
            <option value="">Tất cả trạng thái</option>
            <option value="1">Chuẩn bị</option>
            <option value="2">Đang học</option>
            <option value="3">Đã kết thúc</option>
            <option value="4">Đã hủy</option>
          </select>
        </div>
      </div>

      {isError && (
        <div className="alert alert-danger">
          {error?.message || 'Không thể tải danh sách lớp học phụ trách.'}
        </div>
      )}

      {/* Bảng danh sách lớp */}
      <div className="table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Mã lớp</th>
              <th>Tên lớp</th>
              <th>Cấp độ (Level)</th>
              <th>Sĩ số</th>
              <th>Lịch học cố định</th>
              <th>Thời gian áp dụng</th>
              <th>Trạng thái</th>
            </tr>
          </thead>
          <tbody>
            {!selectedTeacherId ? (
              <tr><td colSpan={7} className="text-center py-8 text-slate-500">Chọn giáo viên để xem lớp học.</td></tr>
            ) : isLoading ? (
              <tr>
                <td colSpan={7} className="text-center py-8 text-slate-500">
                  Đang tải thông tin lớp học...
                </td>
              </tr>
            ) : pagedData?.items && pagedData.items.length > 0 ? (
              pagedData.items.map((item) => (
                <tr key={item.id}>
                  <td className="font-semibold font-mono">
                    {item.classCode}
                  </td>
                  <td className="font-medium">{item.name}</td>
                  <td>{item.levelName}</td>
                  <td>{item.capacity} học viên</td>
                  <td>
                    <strong>{getDayOfWeekText(item.dayOfWeek)}</strong>: {item.startTime} - {item.endTime}
                  </td>
                  <td className="text-xs text-slate-500">
                    Từ {formatDateDisplay(item.startDate)} {item.endDate ? `đến ${formatDateDisplay(item.endDate)}` : ''}
                  </td>
                  <td>{getClassStatusBadge(item.status)}</td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={7} className="text-center py-8 text-slate-500">
                  Không có lớp học nào được phân công.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Phân trang */}
      {pagedData && pagedData.totalItems > 0 && (
        <div className="pagination">
          <div>
            Hiển thị <strong>{(page - 1) * pageSize + 1}</strong> -{' '}
            <strong>{Math.min(page * pageSize, pagedData.totalItems)}</strong> trên tổng số{' '}
            <strong>{pagedData.totalItems}</strong> lớp
          </div>

          <div className="flex gap-2">
            <button
              type="button"
              className="btn btn-secondary px-3 py-1.5 text-xs"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Trang trước
            </button>
            <span className="flex items-center px-2 text-sm text-slate-600">
              Trang {page} / {totalPages || 1}
            </span>
            <button
              type="button"
              className="btn btn-secondary px-3 py-1.5 text-xs"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Trang sau
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
