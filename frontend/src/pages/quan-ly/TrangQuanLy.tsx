import { MaterialIcon } from '../../components/common/MaterialIcon';
import { DangKyHocVien } from './DangKyHocVien';
import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/apiClient';
import { useAuth } from '../../context/AuthContext';
import { Pagination, PAGE_SIZE } from '../../components/common/Pagination';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { BieuMauDuLieu, type BanGhi, type LenhDuLieu, type TruongNhap } from './BieuMauDuLieu';

type TrangDuLieu = { items: BanGhi[]; totalItems: number };
type PhanHe = 'hoc-vien' | 'nguoi-giam-ho' | 'lop-hoc' | 'ghi-danh' | 'lien-ket' | 'nhan-xet';
const truong = (ten: string, nhan: string, loai = 'text', khongBatBuoc = false): TruongNhap => ({ ten, nhan, loai, khongBatBuoc });
const chon = (ten: string, nhan: string, tuyChon: [string | number, string][], loai = 'number'): TruongNhap => ({ ten, nhan, tuyChon, loai });
const coKhong: [string, string][] = [['true', 'Có'], ['false', 'Không']];
const trangThaiLop: [number, string][] = [[1, 'Chuẩn bị'], [2, 'Đang học'], [3, 'Hoàn thành'], [4, 'Đã hủy']];
const trangThaiGhiDanh: [number, string][] = [[1, 'Đang học'], [2, 'Bảo lưu'], [3, 'Hoàn thành'], [4, 'Nghỉ học']];
const cauHinh = {
  'hoc-vien': { tieuDe: 'Danh sách học viên', api: '/api/students', cot: [['studentCode', 'Mã học viên'], ['fullName', 'Họ tên'], ['dateOfBirth', 'Ngày sinh'], ['learningNote', 'Lưu ý học tập']] },
  'nguoi-giam-ho': { tieuDe: 'Người giám hộ', api: '/api/guardians', cot: [['fullName', 'Họ tên'], ['phone', 'Điện thoại'], ['email', 'Email'], ['isActive', 'Hoạt động']] },
  'lop-hoc': { tieuDe: 'Quản lý lớp học', api: '/api/classes', cot: [['maLop', 'Mã lớp'], ['tenLop', 'Tên lớp'], ['siSoHienTai', 'Sĩ số'], ['siSoToiDa', 'Tối đa'], ['ngayBatDau', 'Ngày bắt đầu'], ['trangThai', 'Trạng thái']] },
  'ghi-danh': { tieuDe: 'Ghi danh học viên', api: '/api/enrollments', cot: [['hocVienId', 'Học viên'], ['lopId', 'Lớp học'], ['ngayBatDau', 'Ngày bắt đầu'], ['trangThai', 'Trạng thái'], ['lyDoBaoLuu', 'Lý do bảo lưu'], ['lyDoKetThuc', 'Lý do kết thúc']] },
  'lien-ket': { tieuDe: 'Người giám hộ của học viên', api: '', cot: [['fullName', 'Họ tên'], ['phone', 'Điện thoại'], ['relationship', 'Quan hệ'], ['isPrimary', 'Liên hệ chính']] },
  'nhan-xet': { tieuDe: 'Nhận xét học viên', api: '', cot: [['noiDung', 'Nội dung'], ['maBuoiHoc', 'Mã buổi học'], ['ngayTao', 'Ngày tạo'], ['ngaySua', 'Ngày sửa']] },
};

// Đọc đủ các trang để danh mục lựa chọn không bị mất dữ liệu sau 20 bản ghi.
async function layDanhMuc(duongDan: string, thamSo: Record<string, unknown> = {}): Promise<BanGhi[]> {
  const danhSach: BanGhi[] = [];
  for (let trang = 1; ; trang++) {
    const ketQua = await apiClient<TrangDuLieu | BanGhi[]>(duongDan, { params: { ...thamSo, page: trang, pageSize: 100 } });
    if (Array.isArray(ketQua)) return ketQua;
    danhSach.push(...ketQua.items);
    if (!ketQua.items.length || danhSach.length >= ketQua.totalItems) return danhSach;
  }
}

export function TrangQuanLy({ phanHe }: { phanHe: PhanHe }) {
  const { id } = useParams();
  const [thamSo] = useSearchParams();
  const { user: nguoiDung } = useAuth();
  const boNho = useQueryClient();
  const [tuKhoa, datTuKhoa] = useState('');
  const tuKhoaCham = useDebouncedValue(tuKhoa, 300);
  const [trang, datTrang] = useState(1);
  const [luuTru, datLuuTru] = useState(false);
  const [moDangKy, datMoDangKy] = useState(false);
  const [lenh, datLenh] = useState<LenhDuLieu | null>(null);
  const quanTri = nguoiDung?.role === 'Admin';
  const chamSoc = nguoiDung?.role === 'CustomerCare';
  const giaoVien = nguoiDung?.role === 'Teacher';
  const thongTin = cauHinh[phanHe];
  const duongDan = phanHe === 'lien-ket' ? `/api/students/${id}/guardians` : phanHe === 'nhan-xet' ? `/api/enrollments/${id}/remarks` : thongTin.api;
  const phanTrangMayChu = ['hoc-vien', 'nguoi-giam-ho', 'nhan-xet'].includes(phanHe);
  const duLieu = useQuery({ queryKey: ['quan-ly', phanHe, id, trang, tuKhoaCham, luuTru, thamSo.toString()], queryFn: () => apiClient<TrangDuLieu | BanGhi[]>(duongDan, { params: { page: trang, pageSize: PAGE_SIZE, search: tuKhoaCham, ...(phanHe === 'hoc-vien' ? { isArchived: luuTru } : {}), ...(phanHe === 'ghi-danh' ? { hocVienId: thamSo.get('hocVienId'), lopId: thamSo.get('lopId') } : {}) } }) });
  const danhMuc = useQuery({ queryKey: ['quan-ly', 'danh-muc', phanHe], enabled: ['ghi-danh', 'lop-hoc', 'lien-ket'].includes(phanHe), queryFn: async () => {
    const ketQua: Record<string, BanGhi[]> = {};
    if (phanHe === 'ghi-danh') { ketQua.hocVien = await layDanhMuc('/api/students'); ketQua.lopHoc = await layDanhMuc('/api/classes'); }
    if (phanHe === 'lien-ket' && (quanTri || chamSoc)) ketQua.nguoiGiamHo = await layDanhMuc('/api/guardians', { isActive: true });
    if (phanHe === 'lop-hoc' && quanTri) {
      ketQua.giaoVien = await layDanhMuc('/api/staff', { role: 'Teacher', status: 1 });
      const khoaHoc = await layDanhMuc('/api/courses', { isActive: true });
      ketQua.capDo = (await Promise.all(khoaHoc.map(khoa => layDanhMuc('/api/levels', { courseId: khoa.id, isActive: true })))).flat();
    }
    return ketQua;
  } });
  const luaChon = (ten: string, nhan: string, ma = 'id'): [string | number, string][] => (danhMuc.data?.[ten] ?? []).map(dong => [dong[ma] as string | number, String(dong[nhan] ?? 'Chưa có tên')]);
  const cacTruong: Record<PhanHe, TruongNhap[]> = {
    'hoc-vien': [{ ...truong('studentCode', 'Mã học viên', 'ma-tu-dong'), toiDa: 50 }, truong('fullName', 'Họ tên'), truong('dateOfBirth', 'Ngày sinh', 'date'), { ...chon('gender', 'Giới tính', [[1, 'Nam'], [2, 'Nữ'], [3, 'Khác']]), khongBatBuoc: true }, { ...truong('learningNote', 'Lưu ý học tập', 'textarea', true), toiDa: 500 }],
    'nguoi-giam-ho': [truong('fullName', 'Họ tên'), { ...truong('phone', 'Điện thoại', 'tel'), toiDa: 20 }, truong('email', 'Email', 'email', true), chon('isActive', 'Đang hoạt động', coKhong, 'boolean')],
    'lop-hoc': [truong('maLop', 'Mã lớp', 'ma-tu-dong'), truong('tenLop', 'Tên lớp'), chon('capDoId', 'Cấp độ', luaChon('capDo', 'name')), chon('giaoVienId', 'Giáo viên', luaChon('giaoVien', 'fullName'), 'text'), truong('siSoToiDa', 'Sĩ số tối đa', 'number'), truong('ngayBatDau', 'Ngày bắt đầu', 'date'), truong('ngayKetThuc', 'Ngày kết thúc', 'date', true), chon('thu', 'Ngày học', [[0, 'Chủ nhật'], [1, 'Thứ hai'], [2, 'Thứ ba'], [3, 'Thứ tư'], [4, 'Thứ năm'], [5, 'Thứ sáu'], [6, 'Thứ bảy']]), truong('gioBatDau', 'Giờ bắt đầu', 'time'), truong('gioKetThuc', 'Giờ kết thúc', 'time'), chon('trangThai', 'Trạng thái', trangThaiLop)],
    'ghi-danh': [chon('hocVienId', 'Học viên', luaChon('hocVien', 'fullName')), chon('lopId', 'Lớp học', luaChon('lopHoc', 'tenLop')), truong('ngayBatDau', 'Ngày bắt đầu', 'date')],
    'lien-ket': [chon('guardianId', 'Người giám hộ', luaChon('nguoiGiamHo', 'fullName')), truong('relationship', 'Quan hệ với học viên'), chon('isPrimary', 'Liên hệ chính', coKhong, 'boolean')],
    'nhan-xet': [truong('maBuoiHoc', 'Mã buổi học (tùy chọn)', 'number', true), { ...truong('noiDung', 'Nội dung nhận xét', 'textarea'), toiDa: 1000 }],
  };
  const duocTao = phanHe === 'lop-hoc' ? quanTri : phanHe === 'nhan-xet' ? quanTri || giaoVien : quanTri || chamSoc;
  const duocSua = duocTao && phanHe !== 'ghi-danh';
  const tatCa = Array.isArray(duLieu.data) ? duLieu.data.filter(dong => !tuKhoaCham || Object.values(dong).some(giaTri => String(giaTri ?? '').toLocaleLowerCase('vi').includes(tuKhoaCham.toLocaleLowerCase('vi')))) : duLieu.data?.items ?? [];
  const tongSo = Array.isArray(duLieu.data) ? tatCa.length : duLieu.data?.totalItems ?? 0;
  const cacDong = phanTrangMayChu ? tatCa : tatCa.slice((trang - 1) * PAGE_SIZE, trang * PAGE_SIZE);
  function hienThi(dong: BanGhi, ten: string) {
    const giaTri = dong[ten];
    if (ten === 'trangThai') return (phanHe === 'lop-hoc' ? trangThaiLop : trangThaiGhiDanh).find(([ma]) => ma === giaTri)?.[1] ?? giaTri;
    if (ten === 'hocVienId' || ten === 'lopId') { const nhom = ten === 'hocVienId' ? 'hocVien' : 'lopHoc'; const doiTuong = danhMuc.data?.[nhom]?.find(muc => muc.id === giaTri); return doiTuong?.[ten === 'hocVienId' ? 'fullName' : 'tenLop'] ?? 'Chưa có thông tin'; }
    if (typeof giaTri === 'boolean') return giaTri ? 'Có' : 'Không';
    if (giaTri && (ten.startsWith('ngay') || ten === 'dateOfBirth')) return new Date(String(giaTri)).toLocaleDateString('vi-VN');
    return giaTri ?? '—';
  }
  function thaoTac(tieuDe: string, duoi: string, dong: BanGhi, truongNhap?: TruongNhap[], phuongThuc: LenhDuLieu['phuongThuc'] = 'POST') {
    datLenh({ tieuDe, duongDan: phanHe === 'nhan-xet' ? `/api/remarks/${dong.id}` : `${duongDan}/${dong.id ?? dong.guardianId}${duoi}`, phuongThuc, truong: truongNhap, duLieu: dong });
  }
  return <section className="khung-quan-ly">
    <div className="tieu-de-khung"><h1>{thongTin.tieuDe}</h1></div>
    <div className="noi-dung-khung">
      <div className="thanh-cong-cu">
        {phanHe !== 'nhan-xet' && <input aria-label="Tìm kiếm" type="text" placeholder="Nhập từ khóa tìm kiếm…" value={tuKhoa} onChange={suKien => { datTuKhoa(suKien.target.value); datTrang(1); }} />}
        {phanHe === 'hoc-vien' && <label className="flex items-center gap-2 m-0"><input type="checkbox" checked={luuTru} onChange={suKien => { datLuuTru(suKien.target.checked); datTrang(1); }} />Đã lưu trữ</label>}
        {duocTao && <button className="btn btn-primary" disabled={danhMuc.isFetching || danhMuc.isError} onClick={() => phanHe === 'hoc-vien' ? datMoDangKy(true) : datLenh({ tieuDe: 'Thêm mới', duongDan, phuongThuc: 'POST', truong: cacTruong[phanHe], duLieu: { isActive: true, isPrimary: false, trangThai: 1, ...(thamSo.get('hocVienId') ? { hocVienId: Number(thamSo.get('hocVienId')) } : {}) } })}><MaterialIcon name="add" /> Thêm mới</button>}
        <button className="btn btn-secondary" onClick={() => { void duLieu.refetch(); void danhMuc.refetch(); }}>Tải lại</button>
      </div>
      {(duLieu.isError || danhMuc.isError) && <div role="alert" className="alert alert-danger">{(duLieu.error ?? danhMuc.error as Error)?.message ?? 'Không tải được dữ liệu.'}</div>}
      {duLieu.isFetching && <p role="status">Đang tải dữ liệu…</p>}
      <div className="table-container"><table className="data-table"><thead><tr>{thongTin.cot.map(([ten, nhan]) => <th key={ten}>{nhan}</th>)}<th>Thao tác</th></tr></thead><tbody>
        {cacDong.map(dong => <tr key={String(dong.id ?? dong.guardianId)}>{thongTin.cot.map(([ten]) => <td key={ten}>{hienThi(dong, ten)}</td>)}<td><div className="cac-thao-tac">
          {duocSua && (phanHe !== 'nhan-xet' || quanTri || dong.nguoiTao === nguoiDung?.userId) && <button className="btn btn-secondary" onClick={() => thaoTac('Cập nhật', '', dong, phanHe === 'lien-ket' ? cacTruong[phanHe].slice(1) : phanHe === 'nhan-xet' ? [{ ...truong('noiDung', 'Nội dung nhận xét', 'textarea'), toiDa: 1000 }] : cacTruong[phanHe], 'PUT')}>Sửa</button>}
          {phanHe === 'hoc-vien' && <><Link className="btn btn-secondary" to={`/hoc-vien/${dong.id}/nguoi-giam-ho`}>Người giám hộ</Link>{!giaoVien && <Link className="btn btn-secondary" to={`/ghi-danh?hocVienId=${dong.id}`}>Ghi danh</Link>}{quanTri && <button className="btn btn-secondary" onClick={() => thaoTac(luuTru ? 'Khôi phục học viên' : 'Lưu trữ học viên', luuTru ? '/restore' : '/archive', dong)}>{luuTru ? 'Khôi phục' : 'Lưu trữ'}</button>}</>}
          {phanHe === 'lop-hoc' && !giaoVien && <Link className="btn btn-secondary" to={`/ghi-danh?lopId=${dong.id}`}>Ghi danh</Link>}
          {quanTri && ['lop-hoc', 'nguoi-giam-ho'].includes(phanHe) && <button className="btn btn-danger" onClick={() => thaoTac('Xóa bản ghi', '', dong, undefined, 'DELETE')}>Xóa</button>}
          {phanHe === 'lien-ket' && duocSua && <><button className="btn btn-secondary" disabled={!!dong.isPrimary} onClick={() => thaoTac('Đặt liên hệ chính', '/primary', dong, undefined, 'PUT')}>Đặt chính</button><button className="btn btn-danger" onClick={() => thaoTac('Gỡ liên kết người giám hộ', '', dong, undefined, 'DELETE')}>Gỡ liên kết</button></>}
          {phanHe === 'ghi-danh' && <>
            {(quanTri || chamSoc) && <Link className="btn btn-secondary" to={`/ghi-danh/${dong.id}/nhan-xet`}>Nhận xét</Link>}
            {duocTao && dong.trangThai === 1 && <button className="btn btn-warning" onClick={() => thaoTac('Bảo lưu ghi danh', '/pause', dong, [truong('lyDo', 'Lý do', 'textarea'), truong('ngayDuKienTroLai', 'Ngày dự kiến trở lại', 'date')])}>Bảo lưu</button>}
            {duocTao && dong.trangThai === 2 && <button className="btn btn-secondary" onClick={() => thaoTac('Trở lại học', '/resume', dong)}>Trở lại</button>}
            {duocTao && [1, 2].includes(Number(dong.trangThai)) && <>{quanTri && <button className="btn btn-success" onClick={() => thaoTac('Hoàn thành ghi danh', '/complete', dong, [truong('lyDo', 'Lý do', 'textarea'), truong('ngayKetThuc', 'Ngày kết thúc', 'date')])}>Hoàn thành</button>}<button className="btn btn-danger" onClick={() => thaoTac('Nghỉ học', '/withdraw', dong, [truong('lyDo', 'Lý do', 'textarea'), truong('ngayKetThuc', 'Ngày kết thúc', 'date')])}>Nghỉ học</button></>}
          </>}
        </div></td></tr>)}
        {!duLieu.isFetching && !duLieu.isError && cacDong.length === 0 && <tr><td colSpan={thongTin.cot.length + 1} className="text-center">Không có dữ liệu phù hợp.</td></tr>}
      </tbody></table></div>
      <Pagination totalItems={tongSo} page={trang} onPageChange={datTrang} itemLabel="bản ghi" />
      {moDangKy && <DangKyHocVien dong={() => datMoDangKy(false)} />}
      {lenh && <BieuMauDuLieu lenh={lenh} dong={() => datLenh(null)} daLuu={async () => { await boNho.invalidateQueries(); }} />}
    </div>
  </section>;
}

