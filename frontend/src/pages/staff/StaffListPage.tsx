import React, { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { staffApi } from '../../api/staffApi';
import { useAuth } from '../../context/AuthContext';
import type { ApiError } from '../../types/auth';
import type { EmploymentStatus, StaffResponse } from '../../types/staff';
import { getRoleLabel } from '../../utils/role';
import { CreateStaffModal } from './CreateStaffModal';
import { EditStaffModal } from './EditStaffModal';
import { ChangeRoleModal } from './ChangeRoleModal';
import { ResetStaffPasswordModal } from './ResetStaffPasswordModal';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';

export const StaffListPage: React.FC = () => {
  const { user: currentUser } = useAuth();
  const queryClient = useQueryClient();

  // Filter & Pagination state
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search);
  const [role, setRole] = useState<string>('');
  const [status, setStatus] = useState<EmploymentStatus | undefined>(undefined);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Modal states
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [editingStaff, setEditingStaff] = useState<StaffResponse | null>(null);
  const [changingRoleStaff, setChangingRoleStaff] = useState<StaffResponse | null>(null);
  const [resettingPasswordStaff, setResettingPasswordStaff] = useState<StaffResponse | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<{
    message: string;
    confirmText: string;
    isDangerous: boolean;
    action: () => void;
  } | null>(null);

  // TanStack Query for Staff List
  const {
    data: pagedData,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: ['staff', { search: debouncedSearch, role, status, page, pageSize }],
    queryFn: () => staffApi.getStaff({ search: debouncedSearch, role, status, page, pageSize }),
    placeholderData: keepPreviousData,
  });

  // Activate mutation
  const activateMutation = useMutation({
    mutationFn: (id: string) => staffApi.activateStaff(id),
    onSuccess: () => {
      setActionError(null);
      queryClient.invalidateQueries({ queryKey: ['staff'] });
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setActionError(apiErr.message || 'Kích hoạt tài khoản thất bại.');
    },
  });

  // Deactivate mutation
  const deactivateMutation = useMutation({
    mutationFn: (id: string) => staffApi.deactivateStaff(id),
    onSuccess: () => {
      setActionError(null);
      queryClient.invalidateQueries({ queryKey: ['staff'] });
    },
    onError: (err: unknown) => {
      const apiErr = err as ApiError;
      setActionError(apiErr.message || 'Ngừng hoạt động tài khoản thất bại.');
    },
  });

  const handleToggleStatus = (staff: StaffResponse) => {
    setActionError(null);
    if (staff.id === currentUser?.userId) {
      setActionError('Bạn không thể tự vô hiệu hóa tài khoản của chính mình.');
      return;
    }

    if (staff.status === 1) {
      setConfirmation({
        message: `Bạn có chắc chắn muốn ngừng hoạt động tài khoản của nhân viên "${staff.fullName}" (${staff.employeeCode})? Mọi phiên làm việc của nhân viên này sẽ bị thu hồi.`,
        confirmText: 'Ngừng hoạt động',
        isDangerous: true,
        action: () => deactivateMutation.mutate(staff.id),
      });
    } else {
      setConfirmation({
        message: `Bạn có chắc chắn muốn kích hoạt lại tài khoản của nhân viên "${staff.fullName}" (${staff.employeeCode})?`,
        confirmText: 'Kích hoạt lại',
        isDangerous: false,
        action: () => activateMutation.mutate(staff.id),
      });
    }
  };

  const getRoleBadgeClass = (roleName: string) => {
    switch (roleName?.toLowerCase()) {
      case 'admin':
        return 'badge badge-admin';
      case 'teacher':
        return 'badge badge-teacher';
      case 'accountant':
        return 'badge badge-accountant';
      case 'customercare':
        return 'badge badge-customercare';
      default:
        return 'badge';
    }
  };

  const totalPages = pagedData ? Math.ceil(pagedData.totalItems / pageSize) : 1;

  return (
    <div>
      {/* Tiêu đề trang & Nút thêm mới */}
      <div className="flex justify-between items-center mb-5 flex-wrap gap-4">
        <div>
          <h1 className="text-xl font-semibold text-slate-800 mb-1">Quản lý nhân sự</h1>
          <p className="text-sm text-slate-500">
            Quản lý tài khoản, vai trò, bảo mật và trạng thái làm việc của cán bộ nhân viên CMS EDU.
          </p>
        </div>

        <button
          type="button"
          className="btn btn-primary"
          onClick={() => setIsCreateOpen(true)}
        >
          + Thêm nhân viên
        </button>
      </div>

      {actionError && <div className="alert alert-danger">{actionError}</div>}
      {isError && (
        <div className="alert alert-danger">
          {error?.message || 'Không thể tải danh sách nhân sự từ máy chủ.'}
        </div>
      )}

      {/* Thanh bộ lọc và tìm kiếm */}
      <div className="card grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 p-4 mb-4">
        <div>
          <label htmlFor="searchFilter" className="text-xs font-semibold text-slate-700 mb-1">Tìm kiếm:</label>
          <input
            id="searchFilter"
            type="text"
            placeholder="Mã NV, Họ tên, Email..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <div>
          <label htmlFor="roleFilter" className="text-xs font-semibold text-slate-700 mb-1">Vai trò:</label>
          <select
            id="roleFilter"
            value={role}
            onChange={(e) => {
              setRole(e.target.value);
              setPage(1);
            }}
          >
            <option value="">Tất cả vai trò</option>
            <option value="Admin">Quản trị viên</option>
            <option value="Teacher">Giáo viên</option>
            <option value="Accountant">Kế toán</option>
            <option value="CustomerCare">Chăm sóc khách hàng</option>
          </select>
        </div>

        <div>
          <label htmlFor="statusFilter" className="text-xs font-semibold text-slate-700 mb-1">Trạng thái:</label>
          <select
            id="statusFilter"
            value={status === undefined ? '' : status}
            onChange={(e) => {
              const val = e.target.value;
              setStatus(val === '' ? undefined : (Number(val) as EmploymentStatus));
              setPage(1);
            }}
          >
            <option value="">Tất cả trạng thái</option>
            <option value="1">Đang hoạt động</option>
            <option value="2">Ngừng hoạt động</option>
          </select>
        </div>
      </div>

      {/* Bảng dữ liệu */}
      <div className="table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Mã NV</th>
              <th>Họ và tên</th>
              <th>Email</th>
              <th>Số điện thoại</th>
              <th>Vai trò</th>
              <th>Trạng thái</th>
              <th className="text-right">Hành động</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td colSpan={7} className="text-center py-8 text-slate-400">
                  Đang tải danh sách nhân viên...
                </td>
              </tr>
            ) : pagedData?.items && pagedData.items.length > 0 ? (
              pagedData.items.map((staff) => (
                <tr key={staff.id}>
                  <td className="font-semibold font-mono">
                    {staff.employeeCode}
                  </td>
                  <td className="font-medium">{staff.fullName}</td>
                  <td className="text-slate-500">{staff.email}</td>
                  <td>{staff.phoneNumber || '—'}</td>
                  <td>
                    <span className={getRoleBadgeClass(staff.role)}>{getRoleLabel(staff.role)}</span>
                  </td>
                  <td>
                    {staff.status === 1 ? (
                      <span className="badge badge-active">Đang hoạt động</span>
                    ) : (
                      <span className="badge badge-inactive">Ngừng hoạt động</span>
                    )}
                  </td>
                  <td>
                    <div className="staff-actions">
                    <button
                      type="button"
                      className="btn btn-secondary px-2 py-1 text-xs"
                      onClick={() => setEditingStaff(staff)}
                      title="Sửa thông tin"
                    >
                      Sửa
                    </button>

                    <button
                      type="button"
                      className="btn btn-secondary px-2 py-1 text-xs"
                      onClick={() => setChangingRoleStaff(staff)}
                      title="Đổi vai trò"
                      disabled={staff.id === currentUser?.userId}
                    >
                      Đổi vai trò
                    </button>

                    <button
                      type="button"
                      className="btn btn-secondary px-2 py-1 text-xs"
                      onClick={() => setResettingPasswordStaff(staff)}
                      title="Đặt lại mật khẩu"
                    >
                      Đặt lại MK
                    </button>

                    {staff.status === 1 ? (
                      <button
                        type="button"
                        className="btn btn-danger px-2 py-1 text-xs"
                        onClick={() => handleToggleStatus(staff)}
                        disabled={staff.id === currentUser?.userId || deactivateMutation.isPending}
                        title="Ngừng hoạt động tài khoản"
                      >
                        Ngừng
                      </button>
                    ) : (
                      <button
                        type="button"
                        className="btn btn-primary px-2 py-1 text-xs"
                        onClick={() => handleToggleStatus(staff)}
                        disabled={activateMutation.isPending}
                        title="Kích hoạt lại tài khoản"
                      >
                        Mở khóa
                      </button>
                    )}
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={7} className="text-center py-8 text-slate-500">
                  Không tìm thấy nhân viên nào phù hợp với điều kiện tìm kiếm.
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
            <strong>{pagedData.totalItems}</strong> nhân viên
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

      {/* Các modals */}
      <CreateStaffModal
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        onSuccess={() => {
          queryClient.invalidateQueries({ queryKey: ['staff'] });
        }}
      />

      <EditStaffModal
        staff={editingStaff}
        isOpen={!!editingStaff}
        onClose={() => setEditingStaff(null)}
        onSuccess={() => {
          queryClient.invalidateQueries({ queryKey: ['staff'] });
        }}
      />

      <ChangeRoleModal
        staff={changingRoleStaff}
        isOpen={!!changingRoleStaff}
        onClose={() => setChangingRoleStaff(null)}
        onSuccess={() => {
          queryClient.invalidateQueries({ queryKey: ['staff'] });
        }}
      />

      <ResetStaffPasswordModal
        staff={resettingPasswordStaff}
        isOpen={!!resettingPasswordStaff}
        onClose={() => setResettingPasswordStaff(null)}
        onSuccess={() => {
          queryClient.invalidateQueries({ queryKey: ['staff'] });
        }}
      />

      {confirmation && (
        <ConfirmDialog
          title="Xác nhận thay đổi trạng thái"
          message={confirmation.message}
          confirmText={confirmation.confirmText}
          isDangerous={confirmation.isDangerous}
          onCancel={() => setConfirmation(null)}
          onConfirm={() => {
            confirmation.action();
            setConfirmation(null);
          }}
        />
      )}
    </div>
  );
};
