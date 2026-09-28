import type { PagedResult } from './staff';

export type ClassStatus = 1 | 2 | 3 | 4;
export type EnrollmentStatus = 1 | 2 | 3 | 4;

export interface ClassResponse {
  id: number;
  maLop: string;
  tenLop: string;
  capDoId: number;
  giaoVienId: string;
  siSoToiDa: number;
  siSoHienTai: number;
  ngayBatDau: string;
  ngayKetThuc?: string | null;
  thu: number;
  gioBatDau: string;
  gioKetThuc: string;
  trangThai: ClassStatus;
}

export interface CreateClassRequest {
  maLop: string;
  tenLop: string;
  capDoId: number;
  giaoVienId: string;
  siSoToiDa: number;
  ngayBatDau: string;
  ngayKetThuc?: string | null;
  thu: number;
  gioBatDau: string;
  gioKetThuc: string;
  trangThai: ClassStatus;
}

export interface UpdateClassRequest extends CreateClassRequest {}

export interface EnrollmentResponse {
  id: number;
  hocVienId: number;
  lopId: number;
  ngayBatDau: string;
  ngayKetThuc?: string | null;
  trangThai: EnrollmentStatus;
  lyDoBaoLuu?: string | null;
  ngayDuKienTroLai?: string | null;
  lyDoKetThuc?: string | null;
}

export interface CreateEnrollmentRequest {
  hocVienId: number;
  lopId: number;
  ngayBatDau: string;
}

export interface PauseEnrollmentRequest {
  lyDo: string;
  ngayDuKienTroLai: string;
}

export interface EndEnrollmentRequest {
  lyDo: string;
  ngayKetThuc: string;
}

export interface StudentRemarkResponse {
  id: number;
  maGhiDanh: number;
  maBuoiHoc?: number | null;
  noiDung: string;
  nguoiTao: string;
  ngayTao: string;
  nguoiSua?: string | null;
  ngaySua?: string | null;
}

export interface CreateRemarkRequest {
  maBuoiHoc?: number | null;
  noiDung: string;
}

export interface UpdateRemarkRequest {
  noiDung: string;
}

export type { PagedResult };
