import React, { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { teacherApi } from '../../api/teacherApi';
import { staffApi } from '../../api/staffApi';
import { useAuth } from '../../context/AuthContext';
import type { SessionStatus } from '../../types/teacher';
import { formatDateDisplay } from '../../utils/date';
import { TeacherSelect } from '../../components/TeacherSelect';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';

export const TeacherSchedulePage: React.FC = () => {
  const { user } = useAuth();
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>(user?.role === 'Teacher' ? user.userId : '');
  const [teacherSearch, setTeacherSearch] = useState('');
  const debouncedTeacherSearch = useDebouncedValue(teacherSearch);
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');
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
    queryKey: ['teacher-schedule', selectedTeacherId, fromDate, toDate, page, pageSize],
    queryFn: () =>
      teacherApi.getSchedule(selectedTeacherId, {
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        page,
        pageSize,
      }),
    enabled: !!selectedTeacherId && !(fromDate && toDate && fromDate > toDate),
  });
  const isInvalidDateRange = Boolean(fromDate && toDate && fromDate > toDate);

  const getSessionStatusBadge = (s: SessionStatus) => {
    switch (s) {
      case 1:
        return <span className="badge badge-teacher">Đã lên lịch</span>;
      case 2:
        return <span className="badge badge-active">Đã hoàn thành</span>;
      case 3:
        return <span className="badge badge-danger">Đã hủy</span>;
      default:
        return <span className="badge">—</span>;
    }
  };

  const totalPages = pagedData ? Math.ceil(pagedData.totalItems / pageSize) : 1;

  return (
    <div>
      <div className="mb-5">
        <h1 className="text-xl font-semibold mb-1 text-slate-900">Lịch giảng dạy</h1>
        <p className="text-sm text-slate-600">
          Xem chi tiết các buổi học, bài học và thời gian giảng dạy được phân công.
        </p>
      </div>

      {/* Bộ lọc khoảng ngày */}
      <div className="card grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 p-4 mb-4 items-start">
        {user?.role === 'Admin' && (
          <div>
            <label htmlFor="scheduleTeacherSelect" className="text-xs font-semibold text-slate-700 mb-1">
              Giáo viên:
            </label>
            <TeacherSelect
              id="scheduleTeacherSelect"
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
          <label htmlFor="fromDate" className="text-xs font-semibold text-slate-700 mb-1">Từ ngày:</label>
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
          <label htmlFor="toDate" className="text-xs font-semibold text-slate-700 mb-1">Đến ngày:</label>
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

      {isInvalidDateRange && (
        <div className="alert alert-danger" role="alert">
          Ngày bắt đầu không được sau ngày kết thúc.
        </div>
      )}

      {isError && (
        <div className="alert alert-danger">
          {error?.message || 'Không thể tải lịch giảng dạy.'}
        </div>
      )}

      {/* Bảng danh sách buổi học */}
      <div className="table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Ngày học</th>
              <th>Khung giờ</th>
              <th>Lớp học</th>
              <th>Bài học</th>
              <th>Trạng thái</th>
              <th>Ghi chú</th>
            </tr>
          </thead>
          <tbody>
            {!selectedTeacherId ? (
              <tr><td colSpan={6} className="text-center py-8 text-slate-500">Chọn giáo viên để xem lịch dạy.</td></tr>
            ) : isInvalidDateRange ? (
              <tr><td colSpan={6} className="text-center py-8 text-red-700">Hãy điều chỉnh khoảng ngày để xem lịch.</td></tr>
            ) : isLoading ? (
              <tr>
                <td colSpan={6} className="text-center py-8 text-slate-500">
                  Đang tải lịch buổi học...
                </td>
              </tr>
            ) : pagedData?.items && pagedData.items.length > 0 ? (
              pagedData.items.map((session) => (
                <tr key={session.sessionId}>
                  <td className="font-semibold">{formatDateDisplay(session.sessionDate)}</td>
                  <td>
                    {session.startTime} - {session.endTime}
                  </td>
                  <td>
                    <span className="font-semibold font-mono">{session.classCode}</span> ({session.className})
                  </td>
                  <td>
                    {session.lessonCode ? (
                      <span>
                        <strong>{session.lessonCode}</strong>: {session.lessonName}
                      </span>
                    ) : (
                      <span className="text-slate-400">Chưa gán bài học</span>
                    )}
                  </td>
                  <td>{getSessionStatusBadge(session.status)}</td>
                  <td className="text-xs text-slate-500">
                    {session.note || '—'}
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6} className="text-center py-8 text-slate-500">
                  Không có buổi học nào trong khoảng thời gian này.
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
            <strong>{pagedData.totalItems}</strong> buổi học
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
