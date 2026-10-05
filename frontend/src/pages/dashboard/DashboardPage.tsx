import React from 'react';
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
import { MaterialIcon } from '../../components/common/MaterialIcon';

const compactNumber = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 });
const percentNumber = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 });

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
  items: HorizontalBarItem[];
}

const HorizontalBarChart: React.FC<HorizontalBarChartProps> = ({
  title,
  items,
}) => {
  const total = items.reduce((acc, curr) => acc + curr.count, 0);

  return (
    <div className="chart-box">
      <div className="chart-header">
        <h3 className="chart-title">{title}</h3>
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
  items: VerticalColumnItem[];
  emptyText?: string;
}

const VerticalColumnChart: React.FC<VerticalColumnChartProps> = ({
  title,
  items,
  emptyText = 'Chưa có dữ liệu thống kê theo tháng.',
}) => {
  const maxValue = items.reduce((max, item) => Math.max(max, item.value), 0);

  return (
    <div className="chart-box">
      <div className="chart-header">
        <h3 className="chart-title">{title}</h3>
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
          icon={<MaterialIcon name="group" />}
          iconType="primary"
          value={data.activeStaffCount}
          label="Nhân viên đang làm việc"
          subtext="Tài khoản đang hoạt động"
        />
        <KpiCard
          icon={<MaterialIcon name="school" />}
          iconType="primary"
          value={data.unarchivedStudentCount}
          label="Học viên chưa lưu trữ"
          subtext="Hồ sơ chưa lưu trữ"
        />
        <KpiCard
          icon={<MaterialIcon name="menu_book" />}
          iconType="primary"
          value={data.activeClassCount}
          label="Lớp đang mở"
          subtext="Lớp học đang diễn ra"
        />
        <KpiCard
          icon={<MaterialIcon name="assignment" />}
          iconType="primary"
          value={data.activeEnrollmentCount}
          label="Ghi danh đang học"
          subtext="Học viên đang theo học"
        />
      </div>
      <div className="dashboard-grid-2">
        <HorizontalBarChart
          title="Lớp theo trạng thái"
          items={classesChartItems}
        />
        <VerticalColumnChart
          title="Ghi danh 6 tháng"
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
          icon={<MaterialIcon name="menu_book" />}
          iconType="primary"
          value={data.assignedClassCount}
          label="Lớp đang phụ trách"
          subtext="Lớp giáo viên được phân công"
        />
        <KpiCard
          icon={<MaterialIcon name="school" />}
          iconType="primary"
          value={data.activeStudentCount}
          label="Học viên của bạn"
          subtext="Học sinh đang theo học các lớp"
        />
        <KpiCard
          icon={<MaterialIcon name="schedule" />}
          iconType={pendingCount > 0 ? 'warning' : 'neutral'}
          value={pendingCount}
          label="Buổi cần hoàn tất"
          subtext="Buổi đã qua chưa hoàn tất"
          badgeText={pendingCount > 0 ? 'Cần xử lý ngay' : undefined}
          badgeType="warning"
        />
        <KpiCard
          icon={<MaterialIcon name="calendar_month" />}
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
          </div>
          <Link
            to="/teacher/schedule"
            className="text-sm text-[#0284c7] hover:underline font-medium"
          >
            Xem toàn bộ lịch dạy <MaterialIcon name="arrow_forward" />
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
          icon={<MaterialIcon name="school" />}
          iconType="primary"
          value={data.unarchivedStudentCount}
          label="Học viên chưa lưu trữ"
          subtext="Hồ sơ học sinh đang quản lý"
        />
        <KpiCard
          icon={<MaterialIcon name="assignment" />}
          iconType="primary"
          value={data.activeEnrollmentCount}
          label="Ghi danh đang học"
          subtext="Lượt ghi danh còn hiệu lực"
        />
        <KpiCard
          icon={<MaterialIcon name="schedule" />}
          iconType="warning"
          value={data.pausedEnrollmentCount}
          label="Bảo lưu"
          subtext="Ghi danh tạm ngừng lớp"
        />
        <KpiCard
          icon={<MaterialIcon name="error" />}
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
          items={enrollmentBarItems}
        />
        <div className="chart-box">
          <div className="chart-header">
            <div>
              <h3 className="chart-title">Học viên thiếu thông tin phụ huynh</h3>
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

  const { data, isLoading, isError, error } = useQuery<AccountingDashboardResponse>({
    queryKey: ['dashboard', 'accounting', user?.userId],
    queryFn: () => dashboardApi.getAccountingDashboard(),
  });

  const revenueByMonth = data?.revenueByMonth ?? [];
  const revenueMonthlyItems: VerticalColumnItem[] = revenueByMonth.map((item) => ({
    label: `${String(item.month).padStart(2, '0')}/${item.year}`,
    value: item.amount,
    formattedValue: formatCompactCurrency(item.amount),
    tooltipText: `Tháng ${item.month}/${item.year}: ${formatCurrency(item.amount)}`,
  }));

  return (
    <div>
      {isLoading && <DashboardLoading cardCount={3} />}
      {isError && <FriendlyErrorView error={error} title="Bảng điều khiển Kế toán" />}

      {data && (
        <>
          <div className="kpi-grid kpi-grid-3">
            <KpiCard
              icon={<MaterialIcon name="payments" />}
              iconType="success"
              value={formatCurrency(data.revenue)}
              label="Doanh thu đã thu"
              subtext="Toàn bộ thời gian"
            />
            <KpiCard
              icon={<MaterialIcon name="receipt_long" />}
              iconType="warning"
              value={formatCurrency(data.currentDebt)}
              label="Công nợ hiện tại"
              subtext="Tổng nợ tích lũy"
            />
            <KpiCard
              icon={<MaterialIcon name="error" />}
              iconType={data.overdueDebt > 0 ? 'danger' : 'neutral'}
              value={formatCurrency(data.overdueDebt)}
              label="Công nợ quá hạn"
              subtext="Hóa đơn quá hạn thanh toán"
              badgeText={data.overdueDebt > 0 ? 'Cần đôn đốc' : undefined}
              badgeType="danger"
            />
          </div>
          <div className="dashboard-grid-2">
            <VerticalColumnChart
              title="Doanh thu theo tháng"
              items={revenueMonthlyItems}
              emptyText="Chưa có phát sinh doanh thu."
            />
            <div className="chart-box">
              <div className="chart-header">
                <div>
                  <h3 className="chart-title">Giao dịch gần đây</h3>
                </div>
              </div>

              {data.transactions.length === 0 ? (
                <div className="chart-empty">Không có giao dịch nào phát sinh.</div>
              ) : (
                <div className="table-container max-h-[280px] overflow-y-auto">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Ngày</th>
                        <th>Mã phiếu thu</th>
                        <th>Học viên</th>
                        <th className="text-center">Số tiền</th>
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
                          <td className="text-center font-semibold text-slate-900">
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
        </>
      )}
    </div>
  );
};

export const DashboardPage: React.FC = () => {
  const { user } = useAuth();

  return (
    <div className="dashboard-page">
      <div className="page-header">
        <h1>
          Tổng quan {getRoleLabel(user?.role)}
        </h1>
      </div>

      {user?.role === 'Admin' && <AdminDashboardView />}
      {user?.role === 'Teacher' && <TeacherDashboardView />}
      {user?.role === 'CustomerCare' && <CustomerCareDashboardView />}
      {user?.role === 'Accountant' && <AccountingDashboardView />}
    </div>
  );
};
