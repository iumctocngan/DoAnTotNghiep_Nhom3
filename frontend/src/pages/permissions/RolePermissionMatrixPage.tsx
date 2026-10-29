import React, { useState, useEffect, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { permissionApi } from '../../api/permissionApi';
import { MaterialIcon } from '../../components/common/MaterialIcon';
import { useToast } from '../../components/common/ToastProvider';
import { getRoleLabel } from '../../utils/role';
import type { UserRole } from '../../types/auth';

const EDITABLE_ROLES: UserRole[] = ['Teacher', 'Accountant', 'CustomerCare'];

export const RolePermissionMatrixPage: React.FC = () => {
  const queryClient = useQueryClient();
  const showToast = useToast();

  const [activeFilterGroup, setActiveFilterGroup] = useState<string>('ALL');
  const [searchKeyword, setSearchKeyword] = useState<string>('');
  const [matrixState, setMatrixState] = useState<Record<string, string[]>>({});
  const [initialMatrix, setInitialMatrix] = useState<Record<string, string[]>>({});

  const { data: matrixData, isLoading, error } = useQuery({
    queryKey: ['permissions', 'matrix'],
    queryFn: permissionApi.getMatrix,
  });

  useEffect(() => {
    if (matrixData?.rolePermissions) {
      const copy: Record<string, string[]> = {};
      EDITABLE_ROLES.forEach((r) => {
        copy[r] = [...(matrixData.rolePermissions[r] ?? [])];
      });
      setMatrixState(copy);
      setInitialMatrix(copy);
    }
  }, [matrixData]);

  // Kiểm tra xem có thay đổi nào so với ban đầu không
  const changedRoles = useMemo(() => {
    return EDITABLE_ROLES.filter((role) => {
      const current = matrixState[role] ?? [];
      const init = initialMatrix[role] ?? [];
      if (current.length !== init.length) return true;
      const setA = new Set(current);
      return !init.every((item) => setA.has(item));
    });
  }, [matrixState, initialMatrix]);

  const hasChanges = changedRoles.length > 0;

  const saveMutation = useMutation({
    mutationFn: async () => {
      await Promise.all(
        changedRoles.map((role) =>
          permissionApi.updateRolePermissions(role, matrixState[role] ?? [])
        )
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['permissions', 'matrix'] });
      showToast('Đã lưu thành công ma trận phân quyền hệ thống.', 'success');
      setInitialMatrix({ ...matrixState });
    },
    onError: (err) => {
      showToast(err instanceof Error ? err.message : 'Lỗi khi lưu phân quyền.', 'error');
    },
  });

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center p-12 text-slate-500">
        <div className="w-8 h-8 border-4 border-sky-600 border-t-transparent rounded-full animate-spin mb-3" />
        <p>Đang tải ma trận phân quyền...</p>
      </div>
    );
  }

  if (error || !matrixData) {
    return (
      <div className="p-6 bg-red-50 text-red-700 rounded-lg border border-red-200">
        <p className="font-semibold">Không thể tải thông tin phân quyền.</p>
        <p className="text-sm">Vui lòng kiểm tra quyền Admin hoặc kết nối mạng.</p>
      </div>
    );
  }

  // Danh sách các nhóm Module
  const allGroups = Array.from(new Set(matrixData.permissions.map((p) => p.group)));

  // Lọc quyền theo Group và Từ khóa tìm kiếm
  const filteredPermissions = matrixData.permissions.filter((p) => {
    const matchGroup = activeFilterGroup === 'ALL' || p.group === activeFilterGroup;
    const matchSearch =
      !searchKeyword.trim() ||
      p.name.toLowerCase().includes(searchKeyword.trim().toLowerCase()) ||
      p.description.toLowerCase().includes(searchKeyword.trim().toLowerCase());
    return matchGroup && matchSearch;
  });

  // Gom nhóm danh sách quyền đã lọc
  const groupedPermissions = filteredPermissions.reduce<Record<string, typeof matrixData.permissions>>((acc, item) => {
    if (!acc[item.group]) {
      acc[item.group] = [];
    }
    acc[item.group].push(item);
    return acc;
  }, {});

  // Toggle 1 quyền cho 1 vai trò
  const togglePermission = (role: string, permCode: string) => {
    setMatrixState((prev) => {
      const currentList = prev[role] ?? [];
      const nextList = currentList.includes(permCode)
        ? currentList.filter((c) => c !== permCode)
        : [...currentList, permCode];
      return { ...prev, [role]: nextList };
    });
  };

  // Toggle toàn bộ quyền trong 1 nhóm cho 1 vai trò
  const toggleGroupForRole = (role: string, groupCodes: string[]) => {
    setMatrixState((prev) => {
      const currentList = prev[role] ?? [];
      const allSelected = groupCodes.every((code) => currentList.includes(code));
      let nextList: string[];
      if (allSelected) {
        nextList = currentList.filter((code) => !groupCodes.includes(code));
      } else {
        nextList = Array.from(new Set([...currentList, ...groupCodes]));
      }
      return { ...prev, [role]: nextList };
    });
  };

  // Toggle tất cả quyền của 1 vai trò
  const toggleAllForRole = (role: string) => {
    const allCodes = matrixData.permissions.map((p) => p.code);
    setMatrixState((prev) => {
      const currentList = prev[role] ?? [];
      const isAll = allCodes.every((c) => currentList.includes(c));
      return { ...prev, [role]: isAll ? [] : allCodes };
    });
  };

  const handleReset = () => {
    setMatrixState({ ...initialMatrix });
  };

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Ma trận Phân quyền Vai trò</h1>
          <p className="text-sm text-slate-500 mt-1">
            Tích chọn quyền hạn truy cập chức năng cho từng vai trò trong hệ thống.
          </p>
        </div>

        {/* Nút lưu */}
        <div className="flex items-center gap-3">
          {hasChanges && (
            <button
              type="button"
              onClick={handleReset}
              className="btn btn-secondary text-sm"
              disabled={saveMutation.isPending}
            >
              Hủy thay đổi
            </button>
          )}
          <button
            type="button"
            onClick={() => saveMutation.mutate()}
            disabled={!hasChanges || saveMutation.isPending}
            className="btn btn-primary text-sm flex items-center gap-2 shadow"
          >
            {saveMutation.isPending ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                <span>Đang lưu...</span>
              </>
            ) : (
              <>
                <MaterialIcon name="check" />
                <span>Lưu ma trận quyền {hasChanges && `(${changedRoles.length})`}</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Thanh bộ lọc & Tìm kiếm nhanh */}
      <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-3">
        {/* Filter Pills */}
        <div className="flex items-center gap-1.5 overflow-x-auto pb-1 md:pb-0 scrollbar-thin">
          <button
            type="button"
            onClick={() => setActiveFilterGroup('ALL')}
            className={`px-3 py-1.5 rounded-full text-xs font-semibold whitespace-nowrap transition-colors ${
              activeFilterGroup === 'ALL'
                ? 'bg-sky-600 text-white'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Tất cả ({matrixData.permissions.length})
          </button>
          {allGroups.map((group) => {
            const count = matrixData.permissions.filter((p) => p.group === group).length;
            const isActive = activeFilterGroup === group;
            return (
              <button
                key={group}
                type="button"
                onClick={() => setActiveFilterGroup(group)}
                className={`px-3 py-1.5 rounded-full text-xs font-semibold whitespace-nowrap transition-colors ${
                  isActive
                    ? 'bg-sky-600 text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}
              >
                {group} ({count})
              </button>
            );
          })}
        </div>

        {/* Ô tìm kiếm */}
        <div className="w-full md:w-64 shrink-0">
          <input
            type="text"
            value={searchKeyword}
            onChange={(e) => setSearchKeyword(e.target.value)}
            placeholder="Tìm tên chức năng..."
            className="h-9 text-xs"
          />
        </div>
      </div>

      {/* Bảng Ma trận Role x Permission */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[760px]">
            {/* Header cột */}
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200 text-xs font-bold text-slate-700">
                <th className="py-3.5 px-4 w-[40%]">Chức năng / Nghiệp vụ</th>
                {EDITABLE_ROLES.map((role) => {
                  const rolePerms = matrixState[role] ?? [];
                  const total = matrixData.permissions.length;
                  const count = rolePerms.length;
                  const isAll = count === total && total > 0;

                  return (
                    <th key={role} className="py-3.5 px-3 text-center w-[16%]">
                      <div className="flex flex-col items-center gap-1">
                        <span className="text-sm font-bold text-slate-900">{getRoleLabel(role)}</span>
                        <div className="flex items-center gap-1.5">
                          <input
                            type="checkbox"
                            checked={isAll}
                            onChange={() => toggleAllForRole(role)}
                            title={`Chọn / bỏ chọn tất cả quyền cho ${getRoleLabel(role)}`}
                            className="w-4 h-4 text-sky-600 rounded border-slate-300 focus:ring-sky-500 cursor-pointer"
                          />
                          <span className="text-[11px] font-normal text-slate-500">
                            ({count}/{total})
                          </span>
                        </div>
                      </div>
                    </th>
                  );
                })}
                <th className="py-3.5 px-3 text-center w-[12%]">
                  <div className="flex flex-col items-center gap-1">
                    <span className="text-sm font-bold text-slate-900">Quản trị viên</span>
                    <span className="text-[11px] font-medium text-amber-700 bg-amber-50 px-2 py-0.5 rounded">
                      Toàn quyền
                    </span>
                  </div>
                </th>
              </tr>
            </thead>

            {/* Thân bảng */}
            <tbody className="divide-y divide-slate-100 text-sm">
              {Object.keys(groupedPermissions).length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-slate-500">
                    Không tìm thấy chức năng phù hợp.
                  </td>
                </tr>
              ) : (
                Object.entries(groupedPermissions).map(([groupName, perms]) => {
                  const groupCodes = perms.map((p) => p.code);

                  return (
                    <React.Fragment key={groupName}>
                      {/* Dòng tiêu đề Nhóm (Module Subheader) */}
                      <tr className="bg-slate-100/70 border-y border-slate-200">
                        <td className="py-2.5 px-4 font-bold text-slate-800 text-xs tracking-wide uppercase">
                          {groupName}
                        </td>
                        {EDITABLE_ROLES.map((role) => {
                          const currentRolePerms = matrixState[role] ?? [];
                          const selectedInGroup = groupCodes.filter((c) => currentRolePerms.includes(c)).length;
                          const isAllInGroup = selectedInGroup === groupCodes.length && groupCodes.length > 0;

                          return (
                            <td key={role} className="py-2.5 px-3 text-center">
                              <label className="inline-flex items-center justify-center gap-1.5 cursor-pointer mb-0">
                                <input
                                  type="checkbox"
                                  checked={isAllInGroup}
                                  onChange={() => toggleGroupForRole(role, groupCodes)}
                                  title={`Chọn tất cả trong nhóm ${groupName} cho ${getRoleLabel(role)}`}
                                  className="w-3.5 h-3.5 text-sky-600 rounded border-slate-300 focus:ring-sky-500 cursor-pointer"
                                />
                                <span className="text-[11px] text-slate-500 select-none">
                                  {selectedInGroup}/{groupCodes.length}
                                </span>
                              </label>
                            </td>
                          );
                        })}
                        <td className="py-2.5 px-3 text-center text-xs text-slate-400">
                          {perms.length}/{perms.length}
                        </td>
                      </tr>

                      {/* Các dòng quyền cụ thể trong Nhóm */}
                      {perms.map((perm) => {
                        return (
                          <tr key={perm.code} className="hover:bg-slate-50/70 transition-colors">
                            <td className="py-2.5 px-4">
                              <div className="font-medium text-slate-800">{perm.name}</div>
                              <div className="text-xs text-slate-500">{perm.description}</div>
                            </td>

                            {EDITABLE_ROLES.map((role) => {
                              const isChecked = (matrixState[role] ?? []).includes(perm.code);

                              return (
                                <td key={role} className="py-2.5 px-3 text-center">
                                  <input
                                    type="checkbox"
                                    checked={isChecked}
                                    onChange={() => togglePermission(role, perm.code)}
                                    className="w-4 h-4 text-sky-600 rounded border-slate-300 focus:ring-sky-500 cursor-pointer"
                                  />
                                </td>
                              );
                            })}

                            {/* Cột Admin: Luôn tích và cố định */}
                            <td className="py-2.5 px-3 text-center">
                              <input
                                type="checkbox"
                                checked={true}
                                disabled={true}
                                className="w-4 h-4 text-slate-400 rounded border-slate-300 cursor-not-allowed opacity-60"
                              />
                            </td>
                          </tr>
                        );
                      })}
                    </React.Fragment>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default RolePermissionMatrixPage;
