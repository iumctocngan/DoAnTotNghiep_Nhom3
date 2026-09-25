import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { dashboardApi } from '../../api/dashboardApi';
import type { ApiError } from '../../types/auth';
import type {
  AdminDashboardResponse,
  CustomerCareDashboardResponse,
  TeacherDashboardResponse,
  AccountingDashboardResponse,
} from '../../types/dashboard';
import {
  formatCurrency,
  formatDateDisplay,
  formatDateTimeDisplay,
  formatTimeDisplay,
} from '../../utils/date';
import { getRoleLabel } from '../../utils/role';

const compactNumber = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 });
const percentNumber = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 });
const auditActionLabels: Record<string, string> = {
  CREATE_INVOICE: 'Tạo hóa đơn',
  CANCEL_INVOICE: 'Hủy hóa đơn',
  CREATE_PAYMENT: 'Tạo thanh toán',
  CANCEL_PAYMENT: 'Hủy thanh toán',
};

const IconUsers = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
    <circle cx="9" cy="7" r="4" />
    <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
  </svg>
);

const IconAcademicCap = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="m22 10-10 5L2 10l10-5 10 5Z" />
    <path d="M6 12v5c3 2 9 2 12 0v-5M22 10v6" />
  </svg>
);

const IconBookOpen = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" />
    <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" />
  </svg>
);

const IconCalendar = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="4" width="18" height="18" rx="2" />
    <path d="M16 2v4M8 2v4M3 10h18" />
  </svg>
);

const IconAlertCircle = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10" />
    <path d="M12 8v4M12 16h.01" />
  </svg>
);

const IconClipboard = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="5" y="4" width="14" height="18" rx="2" />
    <path d="M9 4V2h6v2M9 12h6M9 16h6" />
  </svg>
);

const IconClock = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10" />
    <path d="M12 6v6l4 2" />
  </svg>
);

const IconCurrencyDollar = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
  </svg>
);

const IconReceipt = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M4 2h16v20l-4-2-4 2-4-2-4 2V2Z" />
    <path d="M8 7h8M8 11h8M8 15h5" />
  </svg>
);

function formatCompactCurrency(amount: number): string {
  const format = (value: number) => compactNumber.format(value);
  if (amount >= 1_000_000_000) {
    return format(amount / 1_000_000_000) + ' tỷ';
  }
  if (amount >= 1_000_000) {
    return format(amount / 1_000_000) + ' triệu';
  }
  if (amount >= 1_000) {
    return format(amount / 1_000) + ' nghìn';
  }
  return String(amount);
}

function getMonthIndex(date: string): number {
  const [year, month] = date.slice(0, 7).split('-').map(Number);
  return year * 12 + month - 1;
}

const FriendlyErrorView: React.FC<{ error: unknown; title?: string }> = ({ error, title }) => {
  const apiErr = error as ApiError;
  const is403 = apiErr?.statusCode === 403;

  return (
    <div
      className={`p-4 rounded-md border text-sm mb-4 ${
        is403
          ? 'bg-amber-50 border-amber-200 text-amber-800'
          : 'bg-red-50 border-red-200 text-red-700'
      }`}
    >
      <strong>{is403 ? 'Không có quyền truy cập:' : (title || 'Lỗi tải dữ liệu:')}</strong>{' '}
      {is403
        ? 'Bạn không có quyền xem thông tin bảng điều khiển này (Mã lỗi 403 Forbidden).'
        : (apiErr?.message || 'Không thể kết nối đến máy chủ.')}
    </div>
  );
};

const DashboardLoading: React.FC<{ cardCount?: 3 | 4 }> = ({ cardCount = 4 }) => (
  <div aria-label="Đang tải bảng điều khiển" aria-busy="true">
    <div className={`kpi-grid${cardCount === 3 ? ' kpi-grid-3' : ''}`}>
      {Array.from({ length: cardCount }, (_, index) => <div key={index} className="skeleton-card" />)}
    </div>
    <div className="dashboard-grid-2">
      <div className="skeleton-panel" />
      <div className="skeleton-panel" />
    </div>
  </div>
);

interface KpiCardProps {
  icon: React.ReactNode;
  iconType?: 'primary' | 'success' | 'warning' | 'danger' | 'neutral';
  value: string | number;
  label: string;
  subtext?: string;
  badgeText?: string;
  badgeType?: 'warning' | 'danger' | 'success' | 'neutral';
}

const KpiCard: React.FC<KpiCardProps> = ({
  icon,
  iconType = 'primary',
  value,
  label,
  subtext,
  badgeText,
  badgeType = 'warning',
}) => {
  return (
    <div className="kpi-card">
      <div className="kpi-card-top">
        <div className={`kpi-icon-box ${iconType}`}>{icon}</div>
        <div className="kpi-body">
          <span className="kpi-value">{value}</span>
          <span className="kpi-label">{label}</span>
        </div>
      </div>
      {(subtext || badgeText) && (
        <div className="kpi-bottom">
          <span className="kpi-subtext">{subtext}</span>
          {badgeText && <span className={`kpi-badge kpi-badge-${badgeType}`}>{badgeText}</span>}
        </div>
      )}
    </div>
  );
};

interface HorizontalBarItem {
  label: string;
  count: number;
  color?: string;
}

interface HorizontalBarChartProps {
  title: string;
  subtitle?: string;
  items: HorizontalBarItem[];
}

const HorizontalBarChart: React.FC<HorizontalBarChartProps> = ({
  title,
  subtitle,
  items,
}) => {
  const total = items.reduce((acc, curr) => acc + curr.count, 0);

  return (
    <div className="chart-box">
      <div className="chart-header">
        <h3 className="chart-title">{title}</h3>
        {subtitle && <span className="chart-subtitle">{subtitle}</span>}
      </div>

      {total === 0 ? (
        <div className="chart-empty">Chưa có dữ liệu thống kê trạng thái.</div>
      ) : (
        items.map((item) => {
          const percent = (item.count / total) * 100;
          const percentLabel = percent > 0 && percent < 0.1
            ? '<0,1%'
            : `${percentNumber.format(percent)}%`;
          return (
            <div key={item.label} className="bar-row">
              <span className="bar-label" title={item.label}>
                {item.label}
              </span>
              <div
                className="bar-track"
                role="progressbar"
                aria-label={item.label}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={percent}
                title={`${item.label}: ${item.count} (${percentLabel})`}
              >
                <div
                  className="bar-fill"
                  style={{
                    width: `${percent}%`,
                    backgroundColor: item.color || 'var(--primary)',
                  }}
                />
              </div>
              <span className="bar-count">
                {item.count} ({percentLabel})
              </span>
            </div>
          );
        })
      )}
    </div>
  );
};

interface VerticalColumnItem {
  label: string;
  value: number;
  formattedValue?: string;
  tooltipText?: string;
}

interface VerticalColumnChartProps {
  title: string;
  subtitle?: string;
  items: VerticalColumnItem[];
  emptyText?: string;
}

const VerticalColumnChart: React.FC<VerticalColumnChartProps> = ({
  title,
  subtitle,
  items,
  emptyText = 'Chưa có dữ liệu thống kê theo tháng.',
}) => {
  const maxValue = items.reduce((max, item) => Math.max(max, item.value), 0);

  return (
    <div className="chart-box">
      <div className="chart-header">
        <h3 className="chart-title">{title}</h3>
        {subtitle && <span className="chart-subtitle">{subtitle}</span>}
      </div>

      {items.length === 0 || maxValue === 0 ? (
        <div className="chart-empty">{emptyText}</div>
      ) : (
        <div className="column-chart-wrapper">
          <div className="column-chart-content">
            {items.map((item) => {
              const heightPercent = (item.value / maxValue) * 100;
              return (
                <div key={item.label} className="column-chart-item">
                  <div className="column-chart-plot">
                    <div
                      className="column-chart-bar"
                      title={item.tooltipText || `${item.label}: ${item.formattedValue ?? item.value}`}
                      style={{
                        height: `${heightPercent}%`,
                      }}
                    >
                      <span className="column-chart-value">{item.formattedValue ?? item.value}</span>
                    </div>
                  </div>
                  <span className="column-chart-axis">{item.label}</span>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
};

const AdminDashboardView: React.FC = () => {
  const { user } = useAuth();
  const { data, isLoading, isError, error } = useQuery<AdminDashboardResponse>({
    queryKey: ['dashboard', 'admin', user?.userId],
    queryFn: dashboardApi.getAdminDashboard,
  });

  if (isLoading) return <DashboardLoading />;
  if (isError) return <FriendlyErrorView error={error} title="Bảng điều khiển Quản trị" />;
  if (!data) return null;

  const getClassLabel = (status: string) => {
    switch (status) {
      case 'Active': return 'Đang học';
      case 'Preparing': return 'Sắp khai giảng';
      case 'Completed': return 'Đã kết thúc';
      case 'Cancelled': return 'Đã hủy';
      default: return status;
    }
  };

  const getClassColor = (status: string) => {
    switch (status) {
      case 'Active': return 'var(--primary)';
      case 'Preparing': return '#94a3b8';
      case 'Completed': return 'var(--success)';
      case 'Cancelled': return '#cbd5e1';
      default: return 'var(--primary)';
    }
  };

  const classesChartItems: HorizontalBarItem[] = data.classesByStatus.map((c) => ({
    label: getClassLabel(c.status),
    count: c.count,
    color: getClassColor(c.status),
  }));

  const enrollmentChartItems: VerticalColumnItem[] = data.enrollmentStartsByMonth.map((m) => ({
    label: `${String(m.month).padStart(2, '0')}/${m.year}`,
    value: m.count,
    formattedValue: String(m.count),
    tooltipText: `Tháng ${m.month}-${m.year}: ${m.count} lượt ghi danh mới`,
  }));

  return (
    <div>
      <div className="kpi-grid">
        <KpiCard
          icon={<IconUsers />}
          iconType="primary"
          value={data.activeStaffCount}
          label="Nhân viên đang làm việc"
          subtext="Tài khoản đang hoạt động"
        />
        <KpiCard
          icon={<IconAcademicCap />}
          iconType="primary"
          value={data.unarchivedStudentCount}
          label="Học viên chưa lưu trữ"
          subtext="Hồ sơ chưa lưu trữ"
        />
        <KpiCard
          icon={<IconBookOpen />}
          iconType="primary"
          value={data.activeClassCount}
          label="Lớp đang mở"
          subtext="Lớp học đang diễn ra"
        />
        <KpiCard
          icon={<IconClipboard />}
          iconType="primary"
          value={data.activeEnrollmentCount}
          label="Ghi danh đang học"
          subtext="Học viên đang theo học"
        />
      </div>
      <div className="dashboard-grid-2">
        <HorizontalBarChart
          title="Lớp theo trạng thái"
          subtitle="Tỉ lệ phân bổ các lớp học trong hệ thống"
          items={classesChartItems}
        />
        <VerticalColumnChart
          title="Ghi danh 6 tháng"
          subtitle="Số lượt học viên bắt đầu ghi danh 6 tháng gần nhất"
          items={enrollmentChartItems}
        />
      </div>
    </div>
  );
};

const TeacherDashboardView: React.FC = () => {
  const { user } = useAuth();
  const { data, isLoading, isError, error } = useQuery<TeacherDashboardResponse>({
    queryKey: ['dashboard', 'teacher', user?.userId],
    queryFn: dashboardApi.getTeacherDashboard,
  });

  if (isLoading) return <DashboardLoading />;
  if (isError) return <FriendlyErrorView error={error} title="Bảng điều khiển Giảng dạy" />;
  if (!data) return null;

  const pendingCount = data.pendingSessionCount;

  return (
    <div>
      <div className="kpi-grid">
        <KpiCard
          icon={<IconBookOpen />}
          iconType="primary"
          value={data.assignedClassCount}
          label="Lớp đang phụ trách"
          subtext="Lớp giáo viên được phân công"
        />
        <KpiCard
          icon={<IconAcademicCap />}
          iconType="primary"
          value={data.activeStudentCount}
          label="Học viên của bạn"
          subtext="Học sinh đang theo học các lớp"
        />
        <KpiCard
          icon={<IconClock />}
          iconType={pendingCount > 0 ? 'warning' : 'neutral'}
          value={pendingCount}
          label="Buổi cần hoàn tất"
          subtext="Buổi đã qua chưa hoàn tất"
          badgeText={pendingCount > 0 ? 'Cần xử lý ngay' : undefined}
          badgeType="warning"
        />
        <KpiCard
          icon={<IconCalendar />}
          iconType="primary"
          value={data.todaySessionCount}
          label="Buổi dạy hôm nay"
          subtext="Lịch dạy trong ngày hôm nay"
        />
      </div>
      <div className="card">
        <div className="chart-header">
          <div>
            <h3 className="chart-title">Lịch dạy sắp tới</h3>
            <span className="chart-subtitle">Danh sách 5 buổi học tiếp theo theo phân công</span>
          </div>
          <Link
            to="/teacher/schedule"
            className="text-sm text-[#0284c7] hover:underline font-medium"
          >
            Xem toàn bộ lịch dạy →
          </Link>
        </div>

        {data.upcomingSessions.length === 0 ? (
          <div className="chart-empty">Hiện tại không có buổi học nào sắp tới trong lịch phân công.</div>
        ) : (
          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th className="w-[130px]">Thời gian</th>
                  <th className="w-[110px]">Ngày</th>
                  <th className="w-[120px]">Mã lớp</th>
                  <th>Tên lớp học</th>
                </tr>
              </thead>
              <tbody>
                {data.upcomingSessions.map((session) => (
                  <tr key={session.id}>
                    <td className="font-semibold">
                      {formatTimeDisplay(session.startTime)} - {formatTimeDisplay(session.endTime)}
                    </td>
                    <td className="whitespace-nowrap">{formatDateDisplay(session.sessionDate)}</td>
                    <td className="font-mono font-semibold">
                      <span className="badge badge-teacher">{session.classCode}</span>
                    </td>
                    <td className="font-medium">{session.className}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};

const CustomerCareDashboardView: React.FC = () => {
  const { user } = useAuth();
  const { data, isLoading, isError, error } = useQuery<CustomerCareDashboardResponse>({
    queryKey: ['dashboard', 'customer-care', user?.userId],
    queryFn: dashboardApi.getCustomerCareDashboard,
  });

  if (isLoading) return <DashboardLoading />;
  if (isError) return <FriendlyErrorView error={error} title="Bảng điều khiển Chăm sóc học viên" />;
  if (!data) return null;

  const noGuardianCount = data.studentsWithoutGuardianCount;

  const getEnrollmentLabel = (status: string) => {
    switch (status) {
      case 'Active': return 'Đang theo học';
      case 'Paused': return 'Bảo lưu';
      case 'Completed': return 'Hoàn thành';
      case 'Withdrawn': return 'Đã rút';
      default: return status;
    }
  };

  const getEnrollmentColor = (status: string) => {
    switch (status) {
      case 'Active': return 'var(--primary)';
      case 'Paused': return '#d97706';
      case 'Completed': return 'var(--success)';
      case 'Withdrawn': return '#94a3b8';
      default: return 'var(--primary)';
    }
  };

  const enrollmentBarItems: HorizontalBarItem[] = data.enrollmentsByStatus.map((e) => ({
    label: getEnrollmentLabel(e.status),
    count: e.count,
    color: getEnrollmentColor(e.status),
  }));

  const studentsWithoutGuardianList = data.studentsWithoutGuardian;

  return (
    <div>
      <div className="kpi-grid">
        <KpiCard
          icon={<IconAcademicCap />}
          iconType="primary"
          value={data.unarchivedStudentCount}
          label="Học viên chưa lưu trữ"
          subtext="Hồ sơ học sinh đang quản lý"
        />
        <KpiCard
          icon={<IconClipboard />}
          iconType="primary"
          value={data.activeEnrollmentCount}
          label="Ghi danh đang học"
          subtext="Lượt ghi danh còn hiệu lực"
        />
        <KpiCard
          icon={<IconClock />}
          iconType="warning"
          value={data.pausedEnrollmentCount}
          label="Bảo lưu"
          subtext="Ghi danh tạm ngừng lớp"
        />
        <KpiCard
          icon={<IconAlertCircle />}
          iconType={noGuardianCount > 0 ? 'danger' : 'neutral'}
          value={noGuardianCount}
          label="Thiếu thông tin PH"
          subtext="Chưa có người giám hộ"
          badgeText={noGuardianCount > 0 ? 'Cần xử lý ngay' : undefined}
          badgeType="danger"
        />
      </div>
      <div className="dashboard-grid-2">
        <HorizontalBarChart
          title="Trạng thái ghi danh"
          subtitle="Tỉ lệ phân bổ ghi danh theo từng trạng thái"
          items={enrollmentBarItems}
        />
        <div className="chart-box">
          <div className="chart-header">
            <div>
              <h3 className="chart-title">Học viên thiếu thông tin phụ huynh</h3>
              <span className="chart-subtitle">Khu vực cần xử lý hồ sơ thông tin người giám hộ</span>
            </div>
            {noGuardianCount > 0 && (
              <span className="kpi-badge kpi-badge-danger">
                Cần xử lý ({noGuardianCount})
              </span>
            )}
          </div>

          {studentsWithoutGuardianList.length === 0 ? (
            <div className="chart-empty">Tất cả học viên hiện tại đã có đầy đủ thông tin người giám hộ.</div>
          ) : (
            <div className="table-container">
              <table className="data-table">
                <thead>
                  <tr>
                    <th className="w-[120px]">Mã HV</th>
                    <th>Họ và tên</th>
                  </tr>
                </thead>
                <tbody>
                  {studentsWithoutGuardianList.map((st) => (
                    <tr key={st.id}>
                      <td className="font-mono font-semibold">
                        {st.studentCode}
                      </td>
                      <td className="font-medium">{st.fullName}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

const AccountingDashboardView: React.FC = () => {
  const { user } = useAuth();
  const [fromDateInput, setFromDateInput] = useState('');
  const [toDateInput, setToDateInput] = useState('');
  const [appliedFrom, setAppliedFrom] = useState<string | undefined>(undefined);
  const [appliedTo, setAppliedTo] = useState<string | undefined>(undefined);
  const [inputWarning, setInputWarning] = useState<string | null>(null);

  const handleFilter = (e: React.FormEvent) => {
    e.preventDefault();
    setInputWarning(null);

    if (fromDateInput && toDateInput && fromDateInput > toDateInput) {
      setInputWarning('Từ ngày không được sau Đến ngày.');
      return;
    }

    setAppliedFrom(fromDateInput || undefined);
    setAppliedTo(toDateInput || undefined);
  };

  const handleReset = () => {
    setFromDateInput('');
    setToDateInput('');
    setAppliedFrom(undefined);
    setAppliedTo(undefined);
    setInputWarning(null);
  };

  const { data, isLoading, isError, error } = useQuery<AccountingDashboardResponse>({
    queryKey: ['dashboard', 'accounting', user?.userId, appliedFrom, appliedTo],
    queryFn: () => dashboardApi.getAccountingDashboard({ fromDate: appliedFrom, toDate: appliedTo }),
  });
  const periodText = data?.fromDate && data.toDate
    ? `Từ ${formatDateDisplay(data.fromDate)} đến ${formatDateDisplay(data.toDate)}`
    : data?.fromDate
      ? `Từ ${formatDateDisplay(data.fromDate)} đến nay`
      : data?.toDate
        ? `Đến ${formatDateDisplay(data.toDate)}`
        : 'Toàn bộ thời gian';

  const revenueByMonth = data?.revenueByMonth ?? [];
  const revenueMap = new Map(revenueByMonth.map((item) => [
    item.year * 12 + item.month - 1,
    item.amount,
  ]));
  const firstDataMonth = revenueByMonth.length
    ? revenueByMonth[0].year * 12 + revenueByMonth[0].month - 1
    : undefined;
  const lastDataMonth = revenueByMonth.length
    ? revenueByMonth[revenueByMonth.length - 1].year * 12 + revenueByMonth[revenueByMonth.length - 1].month - 1
    : undefined;
  const currentMonth = new Date().getFullYear() * 12 + new Date().getMonth();
  const firstMonth = appliedFrom ? getMonthIndex(appliedFrom) : firstDataMonth;
  const lastMonth = appliedTo
    ? getMonthIndex(appliedTo)
    : appliedFrom
      ? currentMonth
      : lastDataMonth;
  const revenueMonthlyItems: VerticalColumnItem[] = Array.from(
    { length: firstMonth !== undefined && lastMonth !== undefined && lastMonth >= firstMonth
      ? lastMonth - firstMonth + 1
      : 0 },
    (_, index) => {
      const monthIndex = firstMonth + index;
      const month = monthIndex % 12 + 1;
      const year = Math.floor(monthIndex / 12);
      const amount = revenueMap.get(monthIndex) ?? 0;
      return {
        label: `${String(month).padStart(2, '0')}/${year}`,
        value: amount,
        formattedValue: formatCompactCurrency(amount),
        tooltipText: `Tháng ${month}/${year}: ${formatCurrency(amount)}`,
      };
    },
  );

  return (
    <div>
      <div className="card mb-5 p-4 sm:p-5">
        <form onSubmit={handleFilter} className="flex gap-3 items-end flex-wrap">
          <div>
            <label htmlFor="accFromDate" className="text-sm font-medium text-slate-600 mb-1">
              Từ ngày:
            </label>
            <input
              id="accFromDate"
              type="date"
              value={fromDateInput}
              onChange={(e) => setFromDateInput(e.target.value)}
              className="w-[150px]"
            />
          </div>

          <div>
            <label htmlFor="accToDate" className="text-sm font-medium text-slate-600 mb-1">
              Đến ngày:
            </label>
            <input
              id="accToDate"
              type="date"
              value={toDateInput}
              onChange={(e) => setToDateInput(e.target.value)}
              className="w-[150px]"
            />
          </div>

          <button type="submit" className="btn btn-primary px-4 py-2">
            Lọc dữ liệu
          </button>

          {(appliedFrom || appliedTo || fromDateInput || toDateInput) && (
            <button type="button" className="btn btn-secondary px-4 py-2" onClick={handleReset}>
              Đặt lại
            </button>
          )}
        </form>

        {inputWarning && (
          <div className="alert alert-danger mt-3 mb-0">
            {inputWarning}
          </div>
        )}
      </div>

      {isLoading && <DashboardLoading cardCount={3} />}
      {isError && <FriendlyErrorView error={error} title="Bảng điều khiển Kế toán" />}

      {data && (
        <>
          <div className="kpi-grid kpi-grid-3">
            <KpiCard
              icon={<IconCurrencyDollar />}
              iconType="success"
              value={formatCurrency(data.revenue)}
              label="Doanh thu đã thu"
              subtext={periodText}
            />
            <KpiCard
              icon={<IconReceipt />}
              iconType="warning"
              value={formatCurrency(data.currentDebt)}
              label="Công nợ hiện tại"
              subtext="Toàn bộ thời gian, không theo kỳ lọc"
            />
            <KpiCard
              icon={<IconAlertCircle />}
              iconType={data.overdueDebt > 0 ? 'danger' : 'neutral'}
              value={formatCurrency(data.overdueDebt)}
              label="Công nợ quá hạn"
              subtext="Toàn bộ thời gian, không theo kỳ lọc"
              badgeText={data.overdueDebt > 0 ? 'Cần đôn đốc' : undefined}
              badgeType="danger"
            />
          </div>
          <div className="dashboard-grid-2">
            <VerticalColumnChart
              title="Doanh thu theo tháng"
              subtitle="Phát sinh doanh thu thực nhận theo từng tháng"
              items={revenueMonthlyItems}
              emptyText="Chưa có phát sinh doanh thu trong kỳ lọc."
            />
            <div className="chart-box">
              <div className="chart-header">
                <div>
                  <h3 className="chart-title">Giao dịch gần đây</h3>
                  <span className="chart-subtitle">10 phiếu thu và thanh toán mới nhất</span>
                </div>
              </div>

              {data.transactions.length === 0 ? (
                <div className="chart-empty">Không có giao dịch nào phát sinh trong kỳ lọc.</div>
              ) : (
                <div className="table-container max-h-[280px] overflow-y-auto">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Ngày</th>
                        <th>Mã phiếu thu</th>
                        <th>Học viên</th>
                        <th className="text-right">Số tiền</th>
                        <th className="text-center">Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.transactions.slice(0, 10).map((tx) => (
                        <tr key={tx.paymentId}>
                          <td className="whitespace-nowrap">
                            {formatDateTimeDisplay(tx.paidAt)}
                          </td>
                          <td className="font-mono">
                            {tx.receiptNumber || tx.paymentNumber}
                          </td>
                          <td className="font-medium">{tx.studentName}</td>
                          <td className="text-right font-semibold text-slate-900">
                            {formatCurrency(tx.amount)}
                          </td>
                          <td className="text-center">
                            <span className={tx.status === 'Confirmed' ? 'badge badge-active' : 'badge badge-inactive'}>
                              {tx.status === 'Confirmed' ? 'Đã thu' : 'Đã hủy'}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
          <div className="card">
            <div className="chart-header">
              <div>
                <h3 className="chart-title">Nhật ký tài chính</h3>
                <span className="chart-subtitle">Lịch sử kiểm toán và thao tác hóa đơn / thanh toán</span>
              </div>
            </div>

            {data.auditLogs.length === 0 ? (
              <div className="chart-empty">Không có nhật ký kiểm toán tài chính nào trong kỳ lọc.</div>
            ) : (
              <div className="table-container">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th className="w-[140px]">Thời gian</th>
                      <th className="w-[120px]">Mã người thực hiện</th>
                      <th className="w-[120px]">Hành động</th>
                      <th className="w-[160px]">Đối tượng</th>
                      <th>Chi tiết diễn giải</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.auditLogs.slice(0, 15).map((log) => (
                      <tr key={log.id}>
                        <td className="whitespace-nowrap">
                          {formatDateTimeDisplay(log.occurredAt)}
                        </td>
                        <td className="font-mono">{log.userId || 'Hệ thống'}</td>
                        <td>
                          <span className="badge badge-inactive">{auditActionLabels[log.action] ?? log.action}</span>
                        </td>
                        <td className="font-mono">
                          {log.entityType === 'Invoice' ? 'Hóa đơn' : 'Thanh toán'} #{log.entityId}
                        </td>
                        <td className="text-sm text-slate-600">{log.description}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};

export const DashboardPage: React.FC = () => {
  const { user } = useAuth();

  const getRoleSubtitle = (role?: string) => {
    switch (role) {
      case 'Admin': return 'Theo dõi hoạt động toàn hệ thống trung tâm';
      case 'Teacher': return 'Quản lý lịch dạy và lớp học của bạn';
      case 'CustomerCare': return 'Theo dõi, hỗ trợ và chăm sóc học viên';
      case 'Accountant': return 'Theo dõi doanh thu, công nợ và giao dịch';
      default: return 'Bảng điều khiển hoạt động';
    }
  };

  return (
    <div className="dashboard-page">
      <div className="mb-5">
        <h1 className="text-2xl font-semibold mb-1 text-slate-900">
          Tổng quan {getRoleLabel(user?.role)}
        </h1>
        <p className="text-base text-slate-600">
          {getRoleSubtitle(user?.role)}
        </p>
      </div>

      {user?.role === 'Admin' && <AdminDashboardView />}
      {user?.role === 'Teacher' && <TeacherDashboardView />}
      {user?.role === 'CustomerCare' && <CustomerCareDashboardView />}
      {user?.role === 'Accountant' && <AccountingDashboardView />}
    </div>
  );
};
