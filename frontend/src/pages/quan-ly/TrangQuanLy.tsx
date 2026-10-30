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
const coKhong: [string, string][] = [['true', 'CÃ³'], ['false', 'KhÃ´ng']];
const trangThaiLop: [number, string][] = [[1, 'Chuáº©n bá»‹'], [2, 'Äang há»c'], [3, 'HoÃ n thÃ nh'], [4, 'ÄÃ£ há»§y']];
const trangThaiGhiDanh: [number, string][] = [[1, 'Äang há»c'], [2, 'Báº£o lÆ°u'], [3, 'HoÃ n thÃ nh'], [4, 'Nghá»‰ há»c']];
const cauHinh = {
  'hoc-vien': { tieuDe: 'Danh sÃ¡ch há»c viÃªn', api: '/api/students', cot: [['studentCode', 'MÃ£ há»c viÃªn'], ['fullName', 'Há» tÃªn'], ['dateOfBirth', 'NgÃ y sinh'], ['learningNote', 'LÆ°u Ã½ há»c táº­p'], ['courseMonths', 'Sá»‘ thÃ¡ng Ä‘Äƒng kÃ½'], ['remainingSessions', 'Buá»•i cÃ²n láº¡i']] },
  'nguoi-giam-ho': { tieuDe: 'NgÆ°á»i giÃ¡m há»™', api: '/api/guardians', cot: [['fullName', 'Há» tÃªn'], ['phone', 'Äiá»‡n thoáº¡i'], ['email', 'Email'], ['isActive', 'Hoáº¡t Ä‘á»™ng']] },
  'lop-hoc': { tieuDe: 'Quáº£n lÃ½ lá»›p há»c', api: '/api/classes', cot: [['maLop', 'MÃ£ lá»›p'], ['tenLop', 'TÃªn lá»›p'], ['siSoHienTai', 'SÄ© sá»‘'], ['siSoToiDa', 'Tá»‘i Ä‘a'], ['ngayBatDau', 'NgÃ y báº¯t Ä‘áº§u'], ['trangThai', 'Tráº¡ng thÃ¡i']] },
  'ghi-danh': { tieuDe: 'Ghi danh há»c viÃªn', api: '/api/enrollments', cot: [['hocVienId', 'Há»c viÃªn'], ['lopId', 'Lá»›p há»c'], ['ngayBatDau', 'NgÃ y báº¯t Ä‘áº§u'], ['trangThai', 'Tráº¡ng thÃ¡i'], ['lyDoBaoLuu', 'LÃ½ do báº£o lÆ°u'], ['lyDoKetThuc', 'LÃ½ do káº¿t thÃºc']] },
  'lien-ket': { tieuDe: 'NgÆ°á»i giÃ¡m há»™ cá»§a há»c viÃªn', api: '', cot: [['fullName', 'Há» tÃªn'], ['phone', 'Äiá»‡n thoáº¡i'], ['relationship', 'Quan há»‡'], ['isPrimary', 'LiÃªn há»‡ chÃ­nh']] },
  'nhan-xet': { tieuDe: 'Nháº­n xÃ©t há»c viÃªn', api: '', cot: [['noiDung', 'Ná»™i dung'], ['maBuoiHoc', 'MÃ£ buá»•i há»c'], ['ngayTao', 'NgÃ y táº¡o'], ['ngaySua', 'NgÃ y sá»­a']] },
};

// Äá»c Ä‘á»§ cÃ¡c trang Ä‘á»ƒ danh má»¥c lá»±a chá»n khÃ´ng bá»‹ máº¥t dá»¯ liá»‡u sau 20 báº£n ghi.
async function layDanhMuc(duongDan: string, thamSo: Record<string, unknown> = {}): Promise<BanGhi[]> {
  try {
  const danhSach: BanGhi[] = [];
  for (let trang = 1; ; trang++) {
    const ketQua = await apiClient<TrangDuLieu | BanGhi[]>(duongDan, { params: { ...thamSo, page: trang, pageSize: 100 } });
    if (Array.isArray(ketQua)) return ketQua;
    danhSach.push(...ketQua.items);
    if (!ketQua.items.length || danhSach.length >= ketQua.totalItems) return danhSach;
    }
    return danhSach;
  } catch {
    return [];
  }
}

export function TrangQuanLy({ phanHe }: { phanHe: PhanHe }) {
  const { id } = useParams();
  const [thamSo] = useSearchParams();
  const { user: nguoiDung, hasPermission } = useAuth();
  const boNho = useQueryClient();
  const [tuKhoa, datTuKhoa] = useState('');
  const tuKhoaCham = useDebouncedValue(tuKhoa, 300);
  const [trang, datTrang] = useState(1);
  const [luuTru, datLuuTru] = useState(false);
  const [moDangKy, datMoDangKy] = useState(false);
  const [lenh, datLenh] = useState<LenhDuLieu | null>(null);
  const quanTri = nguoiDung?.role === 'Admin';
  const chamSoc = nguoiDung?.role === 'CustomerCare';

  const thongTin = cauHinh[phanHe];
  const duongDan = phanHe === 'lien-ket' ? `/api/students/${id}/guardians` : phanHe === 'nhan-xet' ? `/api/enrollments/${id}/remarks` : thongTin.api;
  const phanTrangMayChu = ['hoc-vien', 'nguoi-giam-ho', 'nhan-xet'].includes(phanHe);
  const duLieu = useQuery({ queryKey: ['quan-ly', phanHe, id, trang, tuKhoaCham, luuTru, thamSo.toString()], queryFn: () => apiClient<TrangDuLieu | BanGhi[]>(duongDan, { params: { page: trang, pageSize: PAGE_SIZE, search: tuKhoaCham, ...(phanHe === 'hoc-vien' ? { isArchived: luuTru } : {}), ...(phanHe === 'ghi-danh' ? { hocVienId: thamSo.get('hocVienId'), lopId: thamSo.get('lopId') } : {}) } }) });
  const danhMuc = useQuery({ queryKey: ['quan-ly', 'danh-muc', phanHe], enabled: ['ghi-danh', 'lop-hoc', 'lien-ket'].includes(phanHe), queryFn: async () => {
    const ketQua: Record<string, BanGhi[]> = {};
    if (phanHe === 'ghi-danh') { ketQua.hocVien = await layDanhMuc('/api/students'); ketQua.lopHoc = await layDanhMuc('/api/classes'); }
    if (phanHe === 'lien-ket' && (quanTri || chamSoc)) ketQua.nguoiGiamHo = await layDanhMuc('/api/guardians');
    if (phanHe === 'lop-hoc' && quanTri) {
      ketQua.giaoVien = await layDanhMuc('/api/staff', { role: 'Teacher', status: 1 });
      const khoaHoc = await layDanhMuc('/api/courses');
      ketQua.capDo = (await Promise.all(khoaHoc.map(khoa => layDanhMuc('/api/levels', { courseId: khoa.id })))).flat();
    }
    return ketQua;
  } });
  const luaChon = (ten: string, nhan: string, ma = 'id'): [string | number, string][] => (danhMuc.data?.[ten] ?? []).map(dong => [dong[ma] as string | number, String(dong[nhan] ?? 'ChÆ°a cÃ³ tÃªn')]);
  const cacTruong: Record<PhanHe, TruongNhap[]> = {
    'hoc-vien': [{ ...truong('studentCode', 'MÃ£ há»c viÃªn', 'ma-tu-dong'), toiDa: 50 }, truong('fullName', 'Há» tÃªn'), truong('dateOfBirth', 'NgÃ y sinh', 'date'), { ...chon('gender', 'Giá»›i tÃ­nh', [[1, 'Nam'], [2, 'Ná»¯'], [3, 'KhÃ¡c']]), khongBatBuoc: true }, { ...truong('learningNote', 'LÆ°u Ã½ há»c táº­p', 'textarea', true), toiDa: 500 }],
    'nguoi-giam-ho': [truong('fullName', 'Há» tÃªn'), { ...truong('phone', 'Äiá»‡n thoáº¡i', 'tel'), toiDa: 20 }, truong('email', 'Email', 'email', true), chon('isActive', 'Äang hoáº¡t Ä‘á»™ng', coKhong, 'boolean')],
    'lop-hoc': [truong('maLop', 'MÃ£ lá»›p', 'ma-tu-dong'), truong('tenLop', 'TÃªn lá»›p'), chon('capDoId', 'Cáº¥p Ä‘á»™', luaChon('capDo', 'name')), chon('giaoVienId', 'GiÃ¡o viÃªn', luaChon('giaoVien', 'fullName'), 'text'), truong('siSoToiDa', 'SÄ© sá»‘ tá»‘i Ä‘a', 'number'), truong('ngayBatDau', 'NgÃ y báº¯t Ä‘áº§u', 'date'), truong('ngayKetThuc', 'NgÃ y káº¿t thÃºc', 'date', true), chon('thu', 'NgÃ y há»c', [[0, 'Chá»§ nháº­t'], [1, 'Thá»© hai'], [2, 'Thá»© ba'], [3, 'Thá»© tÆ°'], [4, 'Thá»© nÄƒm'], [5, 'Thá»© sÃ¡u'], [6, 'Thá»© báº£y']]), truong('gioBatDau', 'Giá» báº¯t Ä‘áº§u', 'time'), truong('gioKetThuc', 'Giá» káº¿t thÃºc', 'time'), chon('trangThai', 'Tráº¡ng thÃ¡i', trangThaiLop)],
    'ghi-danh': [chon('hocVienId', 'Há»c viÃªn', luaChon('hocVien', 'fullName')), chon('lopId', 'Lá»›p há»c', luaChon('lopHoc', 'tenLop')), truong('ngayBatDau', 'NgÃ y báº¯t Ä‘áº§u', 'date')],
    'lien-ket': [chon('guardianId', 'NgÆ°á»i giÃ¡m há»™', luaChon('nguoiGiamHo', 'fullName')), truong('relationship', 'Quan há»‡ vá»›i há»c viÃªn'), chon('isPrimary', 'LiÃªn há»‡ chÃ­nh', coKhong, 'boolean')],
    'nhan-xet': [truong('maBuoiHoc', 'MÃ£ buá»•i há»c (tÃ¹y chá»n)', 'number', true), { ...truong('noiDung', 'Ná»™i dung nháº­n xÃ©t', 'textarea'), toiDa: 1000 }],
  };
  const duocTao = (() => {
    switch (phanHe) {
      case 'hoc-vien': return hasPermission('students.create');
      case 'lop-hoc': return hasPermission('classes.manage');
      case 'nguoi-giam-ho': return hasPermission('guardians.manage');
      case 'ghi-danh': return hasPermission('enrollments.manage');
      case 'nhan-xet': return hasPermission('enrollments.remarks');
      default: return quanTri || chamSoc;
    }
  })();
  const duocSua = (() => {
    switch (phanHe) {
      case 'hoc-vien': return hasPermission('students.edit');
      case 'lop-hoc': return hasPermission('classes.manage');
      case 'nguoi-giam-ho': return hasPermission('guardians.manage');
      case 'ghi-danh': return false;
      case 'nhan-xet': return hasPermission('enrollments.remarks');
      case 'lien-ket': return hasPermission('guardians.manage');
      default: return quanTri || chamSoc;
    }
  })();
  const duocXoa = (() => {
    switch (phanHe) {
      case 'hoc-vien': return hasPermission('students.delete');
      case 'lop-hoc': return hasPermission('classes.delete');
      case 'nguoi-giam-ho': return hasPermission('guardians.delete');
      case 'ghi-danh': return hasPermission('enrollments.delete');
      default: return quanTri;
    }
  })();
  const tatCa = Array.isArray(duLieu.data) ? duLieu.data.filter(dong => !tuKhoaCham || Object.values(dong).some(giaTri => String(giaTri ?? '').toLocaleLowerCase('vi').includes(tuKhoaCham.toLocaleLowerCase('vi')))) : duLieu.data?.items ?? [];
  const tongSo = Array.isArray(duLieu.data) ? tatCa.length : duLieu.data?.totalItems ?? 0;
  const cacDong = phanTrangMayChu ? tatCa : tatCa.slice((trang - 1) * PAGE_SIZE, trang * PAGE_SIZE);
  function hienThi(dong: BanGhi, ten: string) {
    const giaTri = dong[ten];
    if (ten === 'trangThai') return (phanHe === 'lop-hoc' ? trangThaiLop : trangThaiGhiDanh).find(([ma]) => ma === giaTri)?.[1] ?? giaTri;
    if (ten === 'hocVienId' || ten === 'lopId') { const nhom = ten === 'hocVienId' ? 'hocVien' : 'lopHoc'; const doiTuong = danhMuc.data?.[nhom]?.find(muc => muc.id === giaTri); return doiTuong?.[ten === 'hocVienId' ? 'fullName' : 'tenLop'] ?? 'ChÆ°a cÃ³ thÃ´ng tin'; }
    if (typeof giaTri === 'boolean') return giaTri ? 'CÃ³' : 'KhÃ´ng';
    if (giaTri && (ten.startsWith('ngay') || ten === 'dateOfBirth')) return new Date(String(giaTri)).toLocaleDateString('vi-VN');
    return giaTri ?? 'â€”';
  }
  function thaoTac(tieuDe: string, duoi: string, dong: BanGhi, truongNhap?: TruongNhap[], phuongThuc: LenhDuLieu['phuongThuc'] = 'POST') {
    datLenh({ tieuDe, duongDan: phanHe === 'nhan-xet' ? `/api/remarks/${dong.id}` : `${duongDan}/${dong.id ?? dong.guardianId}${duoi}`, phuongThuc, truong: truongNhap, duLieu: dong });
  }
  return <section className="khung-quan-ly">
    <div className="tieu-de-khung"><h1>{thongTin.tieuDe}</h1></div>
    <div className="noi-dung-khung">
      <div className="thanh-cong-cu">
        {phanHe !== 'nhan-xet' && <input aria-label="TÃ¬m kiáº¿m" type="text" placeholder="Nháº­p tá»« khÃ³a tÃ¬m kiáº¿mâ€¦" value={tuKhoa} onChange={suKien => { datTuKhoa(suKien.target.value); datTrang(1); }} />}
        {phanHe === 'hoc-vien' && <label className="flex items-center gap-2 m-0"><input type="checkbox" checked={luuTru} onChange={suKien => { datLuuTru(suKien.target.checked); datTrang(1); }} />ÄÃ£ lÆ°u trá»¯</label>}
        {duocTao && <button className="btn btn-primary" disabled={danhMuc.isFetching || danhMuc.isError} onClick={() => phanHe === 'hoc-vien' ? datMoDangKy(true) : datLenh({ tieuDe: 'ThÃªm má»›i', duongDan, phuongThuc: 'POST', truong: cacTruong[phanHe], duLieu: { isActive: true, isPrimary: false, trangThai: 1, ...(thamSo.get('hocVienId') ? { hocVienId: Number(thamSo.get('hocVienId')) } : {}) } })}><MaterialIcon name="add" /> ThÃªm má»›i</button>}
        <button className="btn btn-secondary" onClick={() => { void duLieu.refetch(); void danhMuc.refetch(); }}>Táº£i láº¡i</button>
      </div>
      {(duLieu.isError || danhMuc.isError) && <div role="alert" className="alert alert-danger">{(duLieu.error ?? danhMuc.error as Error)?.message ?? 'KhÃ´ng táº£i Ä‘Æ°á»£c dá»¯ liá»‡u.'}</div>}
      {duLieu.isFetching && <p role="status">Äang táº£i dá»¯ liá»‡uâ€¦</p>}
      <div className="table-container"><table className="data-table"><thead><tr>{thongTin.cot.map(([ten, nhan]) => <th key={ten}>{nhan}</th>)}<th>Thao tÃ¡c</th></tr></thead><tbody>
        {cacDong.map(dong => <tr key={String(dong.id ?? dong.guardianId)}>{thongTin.cot.map(([ten]) => <td key={ten}>{hienThi(dong, ten)}</td>)}<td><div className="cac-thao-tac">
          {duocSua && (phanHe !== 'nhan-xet' || quanTri || dong.nguoiTao === nguoiDung?.userId) && <button className="btn btn-secondary" onClick={() => thaoTac('Cáº­p nháº­t', '', dong, phanHe === 'lien-ket' ? cacTruong[phanHe].slice(1) : phanHe === 'nhan-xet' ? [{ ...truong('noiDung', 'Ná»™i dung nháº­n xÃ©t', 'textarea'), toiDa: 1000 }] : cacTruong[phanHe], 'PUT')}>Sá»­a</button>}
          {phanHe === 'hoc-vien' && <>{duocSua && !luuTru && <button className="btn btn-success" onClick={() => datLenh({ tieuDe: 'Gia háº¡n khÃ³a há»c Â· 1 thÃ¡ng = 4 buá»•i', duongDan: `/api/students/${dong.id}/renew`, phuongThuc: 'POST', truong: [truong('courseMonths', 'Sá»‘ thÃ¡ng gia háº¡n', 'number')], duLieu: { courseMonths: 1 } })}>Gia háº¡n</button>}{hasPermission('guardians.view') && <Link className="btn btn-secondary" to={`/hoc-vien/${dong.id}/nguoi-giam-ho`}>NgÆ°á»i giÃ¡m há»™</Link>}{hasPermission('enrollments.view') && <Link className="btn btn-secondary" to={`/ghi-danh?hocVienId=${dong.id}`}>Ghi danh</Link>}{duocXoa && <button className="btn btn-secondary" onClick={() => thaoTac(luuTru ? 'KhÃ´i phá»¥c há»c viÃªn' : 'LÆ°u trá»¯ há»c viÃªn', luuTru ? '/restore' : '/archive', dong)}>{luuTru ? 'KhÃ´i phá»¥c' : 'LÆ°u trá»¯'}</button>}</>}
          {phanHe === 'lop-hoc' && hasPermission('enrollments.view') && <Link className="btn btn-secondary" to={`/ghi-danh?lopId=${dong.id}`}>Ghi danh</Link>}
          {duocXoa && ['lop-hoc', 'nguoi-giam-ho'].includes(phanHe) && <button className="btn btn-danger" onClick={() => thaoTac('XÃ³a báº£n ghi', '', dong, undefined, 'DELETE')}>XÃ³a</button>}
          {phanHe === 'lien-ket' && duocSua && <><button className="btn btn-secondary" disabled={!!dong.isPrimary} onClick={() => thaoTac('Äáº·t liÃªn há»‡ chÃ­nh', '/primary', dong, undefined, 'PUT')}>Äáº·t chÃ­nh</button><button className="btn btn-danger" onClick={() => thaoTac('Gá»¡ liÃªn káº¿t ngÆ°á»i giÃ¡m há»™', '', dong, undefined, 'DELETE')}>Gá»¡ liÃªn káº¿t</button></>}
          {phanHe === 'ghi-danh' && <>
            {(quanTri || chamSoc) && <Link className="btn btn-secondary" to={`/ghi-danh/${dong.id}/nhan-xet`}>Nháº­n xÃ©t</Link>}
            {duocTao && dong.trangThai === 1 && <button className="btn btn-warning" onClick={() => thaoTac('Báº£o lÆ°u ghi danh', '/pause', dong, [truong('lyDo', 'LÃ½ do', 'textarea'), truong('ngayDuKienTroLai', 'NgÃ y dá»± kiáº¿n trá»Ÿ láº¡i', 'date')])}>Báº£o lÆ°u</button>}
            {duocTao && dong.trangThai === 2 && <button className="btn btn-secondary" onClick={() => thaoTac('Trá»Ÿ láº¡i há»c', '/resume', dong)}>Trá»Ÿ láº¡i</button>}
            {duocTao && [1, 2].includes(Number(dong.trangThai)) && <>{quanTri && <button className="btn btn-success" onClick={() => thaoTac('HoÃ n thÃ nh ghi danh', '/complete', dong, [truong('lyDo', 'LÃ½ do', 'textarea'), truong('ngayKetThuc', 'NgÃ y káº¿t thÃºc', 'date')])}>HoÃ n thÃ nh</button>}<button className="btn btn-danger" onClick={() => thaoTac('Nghá»‰ há»c', '/withdraw', dong, [truong('lyDo', 'LÃ½ do', 'textarea'), truong('ngayKetThuc', 'NgÃ y káº¿t thÃºc', 'date')])}>Nghá»‰ há»c</button></>}
          </>}
        </div></td></tr>)}
        {!duLieu.isFetching && !duLieu.isError && cacDong.length === 0 && <tr><td colSpan={thongTin.cot.length + 1} className="text-center">KhÃ´ng cÃ³ dá»¯ liá»‡u phÃ¹ há»£p.</td></tr>}
      </tbody></table></div>
      <Pagination totalItems={tongSo} page={trang} onPageChange={datTrang} itemLabel="báº£n ghi" />
      {moDangKy && <DangKyHocVien dong={() => datMoDangKy(false)} />}
      {lenh && <BieuMauDuLieu lenh={lenh} dong={() => datLenh(null)} daLuu={async () => { await boNho.invalidateQueries(); }} />}
    </div>
  </section>;
}

