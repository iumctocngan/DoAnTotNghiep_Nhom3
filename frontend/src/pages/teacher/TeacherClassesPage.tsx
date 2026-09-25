import React, { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { teacherApi } from '../../api/teacherApi';
import { staffApi } from '../../api/staffApi';
import { useAuth } from '../../context/AuthContext';
import type { ClassStatus } from '../../types/teacher';
import { formatDateDisplay } from '../../utils/date';
import { TeacherSelect } from '../../components/TeacherSelect';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { Pagination, PAGE_SIZE } from '../../components/common/Pagination';

export const TeacherClassesPage: React.FC = () => {
  const { user } = useAuth();
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>(user?.role === 'Teacher' ? user.userId : '');
  const [teacherSearch, setTeacherSearch] = useState('');
  const debouncedTeacherSearch = useDebouncedValue(teacherSearch);
  const [status, setStatus] = useState<ClassStatus | undefined>(undefined);
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
    queryKey: ['teacher-classes', selectedTeacherId, status, page, PAGE_SIZE],
    queryFn: () => teacherApi.getClasses(selectedTeacherId, { status, page, pageSize: PAGE_SIZE }),
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

  return (
    <div>
      <div className="page-header">
        <h1>
          Lớp học phụ trách
        </h1>
      </div>

      {/* Thanh bộ lọc */}
      <div className="card grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 p-4 mb-4 items-start">
        {user?.role === 'Admin' && (
          <div>
            <label htmlFor="teacherSelect">
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
          <label htmlFor="classStatusFilter">
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

      {pagedData && <Pagination totalItems={pagedData.totalItems} page={page} onPageChange={setPage} itemLabel="lớp" />}
    </div>
  );
};
