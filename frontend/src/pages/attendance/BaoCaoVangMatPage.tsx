import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { attendanceReportApi } from '../../api/baoCaoVangMatApi';
import { curriculumApi } from '../../api/curriculumApi';
import { classApi } from '../../api/classApi';

const pageSize = 20;

export function AttendanceAbsenceReportPage() {
  const [courseId, setCourseId] = useState<number | ''>('');
  const [classId, setClassId] = useState<number | ''>('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [page, setPage] = useState(1);

  const courses = useQuery({
    queryKey: ['attendance-report-courses'],
    queryFn: () => curriculumApi.getCourses({ page: 1, pageSize: 100 }),
  });
  const classes = useQuery({
    queryKey: ['attendance-report-classes'],
    queryFn: () => classApi.getClasses(),
  });
  const report = useQuery({
    queryKey: ['attendance-absence-report', { courseId, classId, fromDate, toDate, page }],
    queryFn: () =>
      attendanceReportApi.getAbsences({
        courseId: courseId || undefined,
        classId: classId || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        page,
        pageSize,
      }),
  });

  const totalPages = report.data
    ? Math.max(1, Math.ceil(report.data.totalItems / pageSize))
    : 1;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-bold text-slate-900">Báo cáo chuyên cần</h1>
        <p className="mt-1 text-sm text-slate-600">
          Số buổi vắng theo học viên, khóa học và lớp; chỉ tính các buổi đã hoàn tất.
        </p>
      </div>

      <section className="card p-4">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <div>
            <label htmlFor="attendance-report-course" className="text-xs font-semibold uppercase text-slate-500">
              Khóa học
            </label>
            <select
              id="attendance-report-course"
              value={courseId}
              onChange={(event) => {
                setCourseId(event.target.value ? Number(event.target.value) : '');
                setClassId('');
                setPage(1);
              }}
            >
              <option value="">Tất cả khóa học</option>
              {courses.data?.items.map((course) => (
                <option key={course.id} value={course.id}>
                  {course.name} ({course.code})
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="attendance-report-class" className="text-xs font-semibold uppercase text-slate-500">
              Lớp học
            </label>
            <select
              id="attendance-report-class"
              value={classId}
              onChange={(event) => {
                setClassId(event.target.value ? Number(event.target.value) : '');
                setPage(1);
              }}
            >
              <option value="">Tất cả lớp</option>
              {classes.data?.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.tenLop} ({item.maLop})
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="attendance-report-from" className="text-xs font-semibold uppercase text-slate-500">
              Từ ngày
            </label>
            <input
              id="attendance-report-from"
              type="date"
              value={fromDate}
              onChange={(event) => {
                setFromDate(event.target.value);
                setPage(1);
              }}
            />
          </div>
          <div>
            <label htmlFor="attendance-report-to" className="text-xs font-semibold uppercase text-slate-500">
              Đến ngày
            </label>
            <input
              id="attendance-report-to"
              type="date"
              value={toDate}
              onChange={(event) => {
                setToDate(event.target.value);
                setPage(1);
              }}
            />
          </div>
        </div>
      </section>

      {courses.isError && (
        <div className="alert alert-danger" role="alert">
          Không thể tải danh sách khóa học: {courses.error.message}
        </div>
      )}
      {classes.isError && (
        <div className="alert alert-danger" role="alert">
          Không thể tải danh sách lớp học: {classes.error.message}
        </div>
      )}

      <section className="card !p-0 overflow-hidden">
        {report.isLoading ? (
          <div className="p-8 text-center text-slate-500">Đang tải báo cáo...</div>
        ) : report.isError ? (
          <div className="p-6 text-center text-red-600" role="alert">
            Không thể tải báo cáo: {report.error.message}
          </div>
        ) : !report.data || report.data.items.length === 0 ? (
          <div className="p-8 text-center text-slate-500">
            Không có buổi vắng phù hợp với bộ lọc.
          </div>
        ) : (
          <>
            <div className="table-container">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Mã học viên</th>
                    <th>Họ và tên</th>
                    <th>Khóa học</th>
                    <th className="text-right">Số buổi vắng</th>
                  </tr>
                </thead>
                <tbody>
                  {report.data.items.map((item) => (
                    <tr key={`${item.courseId}-${item.studentId}`}>
                      <td className="font-mono">{item.studentCode}</td>
                      <td className="font-medium">{item.studentFullName}</td>
                      <td>{item.courseName} ({item.courseCode})</td>
                      <td className="text-right font-semibold">{item.absenceCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="flex items-center justify-between gap-3 border-t border-slate-200 p-4 text-sm">
              <span className="text-slate-600">
                Tổng {report.data.totalItems} học viên-khóa học
              </span>
              <div className="inline-flex items-center gap-2">
                <button
                  type="button"
                  className="btn btn-secondary text-xs !py-1 !px-3"
                  disabled={page <= 1}
                  onClick={() => setPage((current) => Math.max(current - 1, 1))}
                >
                  Trang trước
                </button>
                <span>Trang {page} / {totalPages}</span>
                <button
                  type="button"
                  className="btn btn-secondary text-xs !py-1 !px-3"
                  disabled={page >= totalPages}
                  onClick={() => setPage((current) => Math.min(current + 1, totalPages))}
                >
                  Trang sau
                </button>
              </div>
            </div>
          </>
        )}
      </section>
    </div>
  );
}
