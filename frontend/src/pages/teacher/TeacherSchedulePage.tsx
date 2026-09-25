import React, { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { teacherApi } from '../../api/teacherApi';
import { staffApi } from '../../api/staffApi';
import { useAuth } from '../../context/AuthContext';
import type { SessionStatus } from '../../types/teacher';
import { formatDateDisplay } from '../../utils/date';
import { TeacherSelect } from '../../components/TeacherSelect';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { Pagination, PAGE_SIZE } from '../../components/common/Pagination';

export const TeacherSchedulePage: React.FC = () => {
  const { user } = useAuth();
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>(user?.role === 'Teacher' ? user.userId : '');
  const [teacherSearch, setTeacherSearch] = useState('');
  const debouncedTeacherSearch = useDebouncedValue(teacherSearch);
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');
  const [page, setPage] = useState(1);

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
    queryKey: ['teacher-schedule', selectedTeacherId, fromDate, toDate, page, PAGE_SIZE],
    queryFn: () =>
      teacherApi.getSchedule(selectedTeacherId, {
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        page,
        pageSize: PAGE_SIZE,
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

  return (
    <div>
      <div className="page-header">
        <h1>Lịch giảng dạy</h1>
      </div>

      {/* Bộ lọc khoảng ngày */}
      <div className="card grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 p-4 mb-4 items-start">
        {user?.role === 'Admin' && (
          <div>
            <label htmlFor="scheduleTeacherSelect">
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
          <label htmlFor="fromDate">Từ ngày:</label>
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
          <label htmlFor="toDate">Đến ngày:</label>
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

      {pagedData && <Pagination totalItems={pagedData.totalItems} page={page} onPageChange={setPage} itemLabel="buổi học" />}
    </div>
  );
};
