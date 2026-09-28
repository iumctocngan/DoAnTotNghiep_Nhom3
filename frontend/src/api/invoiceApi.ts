import { apiClient } from './apiClient';
import type {
  CancelInvoiceRequest,
  CancelPaymentRequest,
  CreateInvoiceRequest,
  CreatePaymentRequest,
  DebtorResponse,
  InvoiceFilterParams,
  InvoiceResponse,
  PagedResult,
  PaymentFilterParams,
  PaymentResponse,
  StudentDebtResponse,
} from '../types/invoice';

export const invoiceApi = {
  getInvoices: (params?: InvoiceFilterParams) =>
    apiClient<PagedResult<InvoiceResponse>>('/api/invoices', {
      params: params as Record<string, unknown>,
    }),

  getInvoiceById: (id: number) =>
    apiClient<InvoiceResponse>(`/api/invoices/${id}`),

  createInvoice: (body: CreateInvoiceRequest) =>
    apiClient<InvoiceResponse>('/api/invoices', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  cancelInvoice: (id: number, body: CancelInvoiceRequest) =>
    apiClient<void>(`/api/invoices/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  getStudentDebt: (studentId: number) =>
    apiClient<StudentDebtResponse>(`/api/invoices/students/${studentId}/debt`),

  getDebtors: (params?: { page?: number; pageSize?: number }) =>
    apiClient<PagedResult<DebtorResponse>>('/api/invoices/debtors', {
      params: params as Record<string, unknown>,
    }),

  getPayments: (params?: PaymentFilterParams) =>
    apiClient<PagedResult<PaymentResponse>>('/api/payments', {
      params: params as Record<string, unknown>,
    }),

  getPaymentById: (id: number) =>
    apiClient<PaymentResponse>(`/api/payments/${id}`),

  createPayment: (invoiceId: number, body: CreatePaymentRequest) =>
    apiClient<PaymentResponse>(`/api/invoices/${invoiceId}/payments`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  cancelPayment: (paymentId: number, body: CancelPaymentRequest) =>
    apiClient<void>(`/api/payments/${paymentId}/cancel`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
};
