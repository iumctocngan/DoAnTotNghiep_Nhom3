import React, { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { invoiceApi } from '../../api/invoiceApi';
import { useAuth } from '../../context/AuthContext';
import { formatCurrency, formatDateDisplay, formatDateTimeDisplay } from '../../utils/date';
import { CreateInvoiceModal } from './CreateInvoiceModal';
import { CreatePaymentModal } from './CreatePaymentModal';
import { CancelInvoiceModal } from './CancelInvoiceModal';
import { CancelPaymentModal } from './CancelPaymentModal';
import type { InvoiceResponse, InvoiceStatus, PaymentResponse } from '../../types/invoice';
import type { ApiError } from '../../types/auth';

export const InvoiceListPage: React.FC = () => {
  const { user } = useAuth();

  const [activeTab, setActiveTab] = useState<'invoices' | 'payments' | 'debtors'>('invoices');

  // Filter state for Invoices
  const [statusFilter, setStatusFilter] = useState<InvoiceStatus | ''>('');
  const [invoicePage, setInvoicePage] = useState(1);
  const pageSize = 20;

  // Filter state for Payments
  const [paymentPage, setPaymentPage] = useState(1);

  // Filter state for Debtors
  const [debtorsPage, setDebtorsPage] = useState(1);

  // Modals
  const [isCreateInvoiceOpen, setIsCreateInvoiceOpen] = useState(false);
  const [selectedInvoiceForPayment, setSelectedInvoiceForPayment] = useState<InvoiceResponse | null>(null);
  const [selectedInvoiceForCancel, setSelectedInvoiceForCancel] = useState<InvoiceResponse | null>(null);
  const [selectedPaymentForCancel, setSelectedPaymentForCancel] = useState<PaymentResponse | null>(null);

  // TanStack Query for Invoices
  const {
    data: pagedInvoices,
    isLoading: isInvoicesLoading,
    isError: isInvoicesError,
    error: invoicesError,
  } = useQuery({
    queryKey: ['invoices', { status: statusFilter, page: invoicePage, pageSize }],
    queryFn: () =>
      invoiceApi.getInvoices({
        status: statusFilter ? (Number(statusFilter) as InvoiceStatus) : undefined,
        page: invoicePage,
        pageSize,
      }),
    enabled: activeTab === 'invoices',
    placeholderData: keepPreviousData,
  });

  // TanStack Query for Payments
  const {
    data: pagedPayments,
    isLoading: isPaymentsLoading,
    isError: isPaymentsError,
    error: paymentsError,
  } = useQuery({
    queryKey: ['payments', { page: paymentPage, pageSize }],
    queryFn: () =>
      invoiceApi.getPayments({
        page: paymentPage,
        pageSize,
      }),
    enabled: activeTab === 'payments',
    placeholderData: keepPreviousData,
  });

  // TanStack Query for Debtors
  const {
    data: pagedDebtors,
    isLoading: isDebtorsLoading,
    isError: isDebtorsError,
    error: debtorsError,
  } = useQuery({
    queryKey: ['debtors', { page: debtorsPage, pageSize }],
    queryFn: () =>
      invoiceApi.getDebtors({
        page: debtorsPage,
        pageSize,
      }),
    enabled: activeTab === 'debtors',
    placeholderData: keepPreviousData,
  });

  const canManageInvoice = user?.role === 'Admin' || user?.role === 'Accountant';
  const canViewPayments = user?.role === 'Admin' || user?.role === 'Accountant';
  const totalInvoicePages = pagedInvoices ? Math.ceil(pagedInvoices.totalItems / pageSize) : 1;
  const totalPaymentPages = pagedPayments ? Math.ceil(pagedPayments.totalItems / pageSize) : 1;
  const totalDebtorPages = pagedDebtors ? Math.ceil(pagedDebtors.totalItems / pageSize) : 1;

  const renderInvoiceBadge = (status: InvoiceStatus) => {
    switch (status) {
      case 1:
        return <span className="badge badge-admin">Đã phát hành</span>;
      case 2:
        return <span className="badge badge-inactive">Đã hủy</span>;
      case 3:
        return <span className="badge badge-teacher">Nháp</span>;
      case 4:
        return <span className="badge badge-accountant">Một phần</span>;
      case 5:
        return <span className="badge badge-active">Đã thu đủ</span>;
      case 6:
        return <span className="badge badge-danger">Quá hạn</span>;
      default:
        return <span className="badge">—</span>;
    }
  };

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-bold text-slate-900">Quản lý Học phí & Thu tiền</h1>
          <p className="text-sm text-slate-600">
            Theo dõi hóa đơn học phí, ghi nhận thanh toán phiếu thu và quản lý công nợ học sinh.
          </p>
        </div>

        {canManageInvoice && (
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => setIsCreateInvoiceOpen(true)}
          >
            + Lập hóa đơn mới
          </button>
        )}
      </div>

      {/* Tabs */}
      <div className="flex border-b border-slate-200 gap-6 text-sm font-semibold">
        {canViewPayments && <button
          type="button"
          className={`pb-3 border-b-2 transition-colors cursor-pointer ${
            activeTab === 'invoices'
              ? 'border-sky-600 text-sky-600'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
          onClick={() => setActiveTab('invoices')}
        >
          📄 Danh sách Hóa đơn
        </button>}
        <button
          type="button"
          className={`pb-3 border-b-2 transition-colors cursor-pointer ${
            activeTab === 'payments'
              ? 'border-sky-600 text-sky-600'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
          onClick={() => setActiveTab('payments')}
        >
          💳 Phiếu thu / Thanh toán
        </button>
        <button
          type="button"
          className={`pb-3 border-b-2 transition-colors cursor-pointer ${
            activeTab === 'debtors'
              ? 'border-sky-600 text-sky-600'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
          onClick={() => setActiveTab('debtors')}
        >
          ⚠️ Học sinh còn nợ (Công nợ)
        </button>
      </div>

      {/* TAB 1: DANH SÁCH HÓA ĐƠN */}
      {activeTab === 'invoices' && (
        <div className="space-y-4">
          <div className="card p-4">
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div>
                <label htmlFor="invStatus" className="text-xs uppercase text-slate-500 font-semibold mb-1">
                  Trạng thái hóa đơn
                </label>
                <select
                  id="invStatus"
                  value={statusFilter}
                  onChange={(e) => {
                    setStatusFilter(e.target.value ? (Number(e.target.value) as InvoiceStatus) : '');
                    setInvoicePage(1);
                  }}
                >
                  <option value="">-- Tất cả trạng thái --</option>
                  <option value="1">Đã phát hành (Chưa nộp)</option>
                  <option value="4">Một phần (Đã nộp 1 phần)</option>
                  <option value="5">Đã thu đủ</option>
                  <option value="6">Quá hạn thanh toán</option>
                  <option value="2">Đã hủy</option>
                  <option value="3">Nháp</option>
                </select>
              </div>
            </div>
          </div>

          <div className="card !p-0 overflow-hidden">
            {isInvoicesLoading ? (
              <div className="p-8 text-center text-slate-500">
                <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-sky-600 border-t-transparent mb-3" />
                <p>Đang tải danh sách hóa đơn...</p>
              </div>
            ) : isInvoicesError ? (
              <div className="p-6 text-center text-red-600">
                <p>Đã xảy ra lỗi: {(invoicesError as unknown as ApiError)?.message || 'Vui lòng thử lại.'}</p>
              </div>
            ) : !pagedInvoices || pagedInvoices.items.length === 0 ? (
              <div className="p-8 text-center text-slate-500">
                <p className="font-medium text-slate-700 mb-1">Không có hóa đơn nào</p>
                <p className="text-sm">Chưa có hóa đơn phù hợp với điều kiện tìm kiếm.</p>
              </div>
            ) : (
              <div className="table-container">
                <table className="table-custom">
                  <thead>
                    <tr>
                      <th>Số HĐ</th>
                      <th>Học viên</th>
                      <th>Kỳ học</th>
                      <th>Hạn nộp</th>
                      <th>Phải nộp</th>
                      <th>Đã nộp</th>
                      <th>Còn nợ</th>
                      <th>Trạng thái</th>
                      <th className="text-right">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pagedInvoices.items.map((inv) => (
                      <tr key={inv.id}>
                        <td className="font-mono font-bold text-slate-900">{inv.soHoaDon}</td>
                        <td>
                          <div className="font-semibold text-slate-900">{inv.tenHocVien}</div>
                          <div className="text-xs font-mono text-slate-500">{inv.maHocVien}</div>
                        </td>
                        <td className="text-xs">
                          {formatDateDisplay(inv.ngayBatDauKy)} → {formatDateDisplay(inv.ngayKetThucKy)}
                        </td>
                        <td className="text-xs font-medium">{formatDateDisplay(inv.ngayDenHan)}</td>
                        <td className="font-semibold">{formatCurrency(inv.soTienPhaiTra)}</td>
                        <td className="text-emerald-700 font-semibold">{formatCurrency(inv.tongDaXacNhan)}</td>
                        <td className={inv.conNo > 0 ? 'text-red-700 font-bold' : 'text-slate-600 font-semibold'}>
                          {formatCurrency(inv.conNo)}
                        </td>
                        <td>{renderInvoiceBadge(inv.trangThai)}</td>
                        <td className="text-right whitespace-nowrap">
                          <div className="inline-flex items-center gap-1.5">
                            {canManageInvoice && inv.trangThai !== 2 && inv.conNo > 0 && (
                              <button
                                type="button"
                                className="btn btn-secondary text-xs !py-1 !px-2.5 text-emerald-700 font-semibold border-emerald-300 hover:bg-emerald-50"
                                onClick={() => setSelectedInvoiceForPayment(inv)}
                              >
                                💵 Thu tiền
                              </button>
                            )}

                            {canManageInvoice && inv.trangThai !== 2 && inv.tongDaXacNhan === 0 && (
                              <button
                                type="button"
                                className="btn btn-secondary text-xs !py-1 !px-2 text-red-600 hover:text-red-800"
                                onClick={() => setSelectedInvoiceForCancel(inv)}
                              >
                                Hủy HĐ
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {/* Phân trang */}
            {pagedInvoices && pagedInvoices.totalItems > 0 && (
              <div className="flex flex-wrap items-center justify-between p-4 border-t border-slate-200 text-sm gap-2">
                <span className="text-slate-600">
                  Hiển thị <strong>{(invoicePage - 1) * pageSize + 1}</strong> -{' '}
                  <strong>{Math.min(invoicePage * pageSize, pagedInvoices.totalItems)}</strong> trên tổng số{' '}
                  <strong>{pagedInvoices.totalItems}</strong> hóa đơn
                </span>
                <div className="inline-flex items-center gap-2">
                  <button
                    type="button"
                    className="btn btn-secondary text-xs !py-1 !px-3"
                    disabled={invoicePage <= 1}
                    onClick={() => setInvoicePage((p) => Math.max(p - 1, 1))}
                  >
                    Trang trước
                  </button>
                  <span className="text-slate-700 font-medium px-2">
                    Trang {invoicePage} / {totalInvoicePages}
                  </span>
                  <button
                    type="button"
                    className="btn btn-secondary text-xs !py-1 !px-3"
                    disabled={invoicePage >= totalInvoicePages}
                    onClick={() => setInvoicePage((p) => Math.min(p + 1, totalInvoicePages))}
                  >
                    Trang sau
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 2: QUẢN LÝ PHIẾU THU (PAYMENTS) */}
      {canViewPayments && activeTab === 'payments' && (
        <div className="card !p-0 overflow-hidden">
          {isPaymentsLoading ? (
            <div className="p-8 text-center text-slate-500">
              <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-sky-600 border-t-transparent mb-3" />
              <p>Đang tải danh sách phiếu thu...</p>
            </div>
          ) : isPaymentsError ? (
            <div className="p-6 text-center text-red-600">
              <p>Đã xảy ra lỗi: {(paymentsError as unknown as ApiError)?.message || 'Vui lòng thử lại.'}</p>
            </div>
          ) : !pagedPayments || pagedPayments.items.length === 0 ? (
            <div className="p-8 text-center text-slate-500">
              <p className="font-medium text-slate-700 mb-1">Chưa có giao dịch thanh toán nào</p>
              <p className="text-sm">Các phiếu thu học phí sẽ hiển thị tại đây khi được ghi nhận.</p>
            </div>
          ) : (
            <div className="table-container">
              <table className="table-custom">
                <thead>
                  <tr>
                    <th>Số phiếu thu</th>
                    <th>Hóa đơn</th>
                    <th>Học viên</th>
                    <th>Số tiền thu</th>
                    <th>Hình thức</th>
                    <th>Thời gian thu</th>
                    <th>Trạng thái</th>
                    <th>Người lập</th>
                    <th className="text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedPayments.items.map((p) => (
                    <tr key={p.id}>
                      <td className="font-mono font-bold text-slate-900">{p.receiptNumber}</td>
                      <td className="font-mono text-slate-700">{p.invoiceNumber}</td>
                      <td className="font-medium text-slate-900">{p.tenHocVien}</td>
                      <td className="font-bold text-emerald-700">{formatCurrency(p.amount)}</td>
                      <td>
                        {p.method === 1 ? (
                          <span className="badge badge-teacher">💵 Tiền mặt</span>
                        ) : (
                          <span className="badge badge-admin">🏦 Chuyển khoản</span>
                        )}
                      </td>
                      <td className="text-xs text-slate-700">{formatDateTimeDisplay(p.paidAt)}</td>
                      <td>
                        {p.status === 1 ? (
                          <span className="badge badge-active">Đã xác nhận</span>
                        ) : (
                          <span className="badge badge-danger">Đã hủy</span>
                        )}
                      </td>
                      <td className="text-xs text-slate-500">{p.createdBy}</td>
                      <td className="text-right whitespace-nowrap">
                        {canManageInvoice && p.status === 1 && (
                          <button
                            type="button"
                            className="btn btn-secondary text-xs !py-1 !px-2 text-red-600 hover:text-red-800"
                            onClick={() => setSelectedPaymentForCancel(p)}
                          >
                            Hủy phiếu thu
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Phân trang Payments */}
          {pagedPayments && pagedPayments.totalItems > 0 && (
            <div className="flex flex-wrap items-center justify-between p-4 border-t border-slate-200 text-sm gap-2">
              <span className="text-slate-600">
                Hiển thị <strong>{(paymentPage - 1) * pageSize + 1}</strong> -{' '}
                <strong>{Math.min(paymentPage * pageSize, pagedPayments.totalItems)}</strong> trên tổng số{' '}
                <strong>{pagedPayments.totalItems}</strong> phiếu thu
              </span>
              <div className="inline-flex items-center gap-2">
                <button
                  type="button"
                  className="btn btn-secondary text-xs !py-1 !px-3"
                  disabled={paymentPage <= 1}
                  onClick={() => setPaymentPage((p) => Math.max(p - 1, 1))}
                >
                  Trang trước
                </button>
                <span className="text-slate-700 font-medium px-2">
                  Trang {paymentPage} / {totalPaymentPages}
                </span>
                <button
                  type="button"
                  className="btn btn-secondary text-xs !py-1 !px-3"
                  disabled={paymentPage >= totalPaymentPages}
                  onClick={() => setPaymentPage((p) => Math.min(p + 1, totalPaymentPages))}
                >
                  Trang sau
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* TAB 3: DANH SÁCH HỌC SINH CÒN NỢ (DEBTORS) */}
      {activeTab === 'debtors' && (
        <div className="card !p-0 overflow-hidden">
          {isDebtorsLoading ? (
            <div className="p-8 text-center text-slate-500">
              <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-sky-600 border-t-transparent mb-3" />
              <p>Đang tải danh sách công nợ học sinh...</p>
            </div>
          ) : isDebtorsError ? (
            <div className="p-6 text-center text-red-600">
              <p>Đã xảy ra lỗi: {(debtorsError as unknown as ApiError)?.message || 'Vui lòng thử lại.'}</p>
            </div>
          ) : !pagedDebtors || pagedDebtors.items.length === 0 ? (
            <div className="p-8 text-center text-slate-500">
              <p className="font-medium text-emerald-700 mb-1">🎉 Tuyệt vời! Không có học sinh nào nợ học phí.</p>
              <p className="text-sm">Toàn bộ học viên đã thanh toán đủ học phí.</p>
            </div>
          ) : (
            <div className="table-container">
              <table className="table-custom">
                <thead>
                  <tr>
                    <th>Mã HV</th>
                    <th>Họ và tên</th>
                    <th>Số tiền còn nợ</th>
                    <th>Số hóa đơn chưa trả</th>
                    <th>Cảnh báo quá hạn</th>
                    <th className="text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedDebtors.items.map((d) => (
                    <tr key={d.studentId}>
                      <td className="font-mono font-medium">{d.maHocVien}</td>
                      <td className="font-semibold text-slate-900">{d.tenHocVien}</td>
                      <td className="font-bold text-red-700 text-base">{formatCurrency(d.conNo)}</td>
                      <td className="font-medium">{d.soHoaDonChuaTra} hóa đơn</td>
                      <td>
                        {d.coHoaDonQuaHan ? (
                          <span className="badge badge-danger">⚠️ Có hóa đơn quá hạn</span>
                        ) : (
                          <span className="badge badge-admin">Chưa đến hạn</span>
                        )}
                      </td>
                      <td className="text-right whitespace-nowrap">
                        <Link
                          to={`/students/${d.studentId}`}
                          className="btn btn-secondary text-xs !py-1 !px-2.5"
                        >
                          Chi tiết học viên →
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Phân trang Debtors */}
          {pagedDebtors && pagedDebtors.totalItems > 0 && (
            <div className="flex flex-wrap items-center justify-between p-4 border-t border-slate-200 text-sm gap-2">
              <span className="text-slate-600">
                Hiển thị <strong>{(debtorsPage - 1) * pageSize + 1}</strong> -{' '}
                <strong>{Math.min(debtorsPage * pageSize, pagedDebtors.totalItems)}</strong> trên tổng số{' '}
                <strong>{pagedDebtors.totalItems}</strong> học sinh còn nợ
              </span>
              <div className="inline-flex items-center gap-2">
                <button
                  type="button"
                  className="btn btn-secondary text-xs !py-1 !px-3"
                  disabled={debtorsPage <= 1}
                  onClick={() => setDebtorsPage((p) => Math.max(p - 1, 1))}
                >
                  Trang trước
                </button>
                <span className="text-slate-700 font-medium px-2">
                  Trang {debtorsPage} / {totalDebtorPages}
                </span>
                <button
                  type="button"
                  className="btn btn-secondary text-xs !py-1 !px-3"
                  disabled={debtorsPage >= totalDebtorPages}
                  onClick={() => setDebtorsPage((p) => Math.min(p + 1, totalDebtorPages))}
                >
                  Trang sau
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Modals */}
      <CreateInvoiceModal
        isOpen={isCreateInvoiceOpen}
        onClose={() => setIsCreateInvoiceOpen(false)}
      />

      <CreatePaymentModal
        invoice={selectedInvoiceForPayment}
        isOpen={selectedInvoiceForPayment !== null}
        onClose={() => setSelectedInvoiceForPayment(null)}
      />

      <CancelInvoiceModal
        invoice={selectedInvoiceForCancel}
        isOpen={selectedInvoiceForCancel !== null}
        onClose={() => setSelectedInvoiceForCancel(null)}
      />

      <CancelPaymentModal
        payment={selectedPaymentForCancel}
        isOpen={selectedPaymentForCancel !== null}
        onClose={() => setSelectedPaymentForCancel(null)}
      />
    </div>
  );
};
