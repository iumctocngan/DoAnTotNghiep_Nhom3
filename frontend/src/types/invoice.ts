import type { PagedResult } from './staff';

export type InvoiceStatus = 1 | 2 | 3 | 4 | 5 | 6;
// 1: Issued (Đã phát hành), 2: Cancelled (Đã hủy), 3: Draft (Nháp), 4: Partial (Thanh toán một phần), 5: Paid (Đã thanh toán), 6: Overdue (Quá hạn)

export type PaymentMethod = 1 | 2; // 1: Cash (Tiền mặt), 2: BankTransfer (Chuyển khoản)
export type PaymentStatus = 1 | 2; // 1: Confirmed (Đã xác nhận), 2: Cancelled (Đã hủy)

export interface InvoiceResponse {
  id: number;
  soHoaDon: string;
  enrollmentId: number;
  studentId: number;
  tenHocVien: string;
  maHocVien: string;
  ngayBatDauKy: string; // YYYY-MM-DD
  ngayKetThucKy: string; // YYYY-MM-DD
  soTienPhaiTra: number;
  ngayDenHan: string; // YYYY-MM-DD
  trangThai: InvoiceStatus;
  tongDaXacNhan: number;
  conNo: number;
  ghiChu?: string | null;
  nguoiTao: string;
  ngayTao: string;
  nguoiHuy?: string | null;
  ngayHuy?: string | null;
  lyDoHuy?: string | null;
}

export interface CreateInvoiceRequest {
  enrollmentId: number;
  periodStart: string; // YYYY-MM-DD
  amountDue: number;
  dueDate: string; // YYYY-MM-DD
  note?: string | null;
}

export interface CancelInvoiceRequest {
  lyDoHuy: string;
}

export interface StudentDebtResponse {
  studentId: number;
  tenHocVien: string;
  maHocVien: string;
  tongPhaiTra: number;
  tongDaXacNhan: number;
  conNo: number;
  coHoaDonQuaHan: boolean;
  danhSachHoaDon: InvoiceResponse[];
}

export interface DebtorResponse {
  studentId: number;
  tenHocVien: string;
  maHocVien: string;
  conNo: number;
  coHoaDonQuaHan: boolean;
  soHoaDonChuaTra: number;
}

export interface InvoiceFilterParams {
  enrollmentId?: number;
  studentId?: number;
  status?: InvoiceStatus;
  page?: number;
  pageSize?: number;
}

export interface PaymentResponse {
  id: number;
  paymentNumber: string;
  receiptNumber: string;
  invoiceId: number;
  invoiceNumber: string;
  studentId: number;
  tenHocVien: string;
  amount: number;
  paidAt: string;
  method: PaymentMethod;
  status: PaymentStatus;
  note?: string | null;
  createdBy: string;
  cancelledBy?: string | null;
  cancelledAt?: string | null;
  cancelReason?: string | null;
}

export interface CreatePaymentRequest {
  amount: number;
  paidAt: string; // ISO DateTime
  method: PaymentMethod;
  note?: string | null;
}

export interface CancelPaymentRequest {
  lyDoHuy: string;
}

export interface PaymentFilterParams {
  invoiceId?: number;
  studentId?: number;
  status?: PaymentStatus;
  method?: PaymentMethod;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export type { PagedResult };
