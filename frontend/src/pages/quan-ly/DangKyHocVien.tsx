import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/apiClient';
import { Modal } from '../../components/common/Modal';
import { Pagination } from '../../components/common/Pagination';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import type { GuardianResponse, PagedResult } from '../../types/student';

export function DangKyHocVien({ dong }: { dong: () => void }) {
  const boNho = useQueryClient();
  const [hoTen, datHoTen] = useState('');
  const [ngaySinh, datNgaySinh] = useState('');
  const [gioiTinh, datGioiTinh] = useState('');
  const [ghiChu, datGhiChu] = useState('');
  const [taoMoi, datTaoMoi] = useState(false);
  const [tuKhoa, datTuKhoa] = useState('');
  const tuKhoaCham = useDebouncedValue(tuKhoa);
  const [trang, datTrang] = useState(1);
  const [nguoiDaChon, datNguoiDaChon] = useState<GuardianResponse | null>(null);
  const [tenNguoiGiamHo, datTenNguoiGiamHo] = useState('');
  const [dienThoai, datDienThoai] = useState('');
  const [email, datEmail] = useState('');
  const [quanHe, datQuanHe] = useState('');
  const [dangLuu, datDangLuu] = useState(false);
  const [loi, datLoi] = useState('');
  const danhSach = useQuery({ queryKey: ['nguoi-giam-ho-dang-ky', tuKhoaCham, trang], enabled: !taoMoi,
    queryFn: () => apiClient<PagedResult<GuardianResponse>>('/api/guardians', { params: { search: tuKhoaCham, isActive: true, page: trang, pageSize: 20 } }) });
  async function luu(suKien: React.FormEvent) {
    suKien.preventDefault();
    if (!taoMoi && !nguoiDaChon) { datLoi('Vui lòng chọn người giám hộ hoặc nhập người mới.'); return; }
    datDangLuu(true); datLoi('');
    try {
      await apiClient('/api/students/register', { method: 'POST', body: JSON.stringify({
        fullName: hoTen.trim(), dateOfBirth: ngaySinh, gender: gioiTinh ? Number(gioiTinh) : null,
        learningNote: ghiChu.trim() || null, relationship: quanHe.trim(),
        guardianId: taoMoi ? null : nguoiDaChon!.id,
        nguoiGiamHoMoi: taoMoi ? { fullName: tenNguoiGiamHo.trim(), phone: dienThoai.trim(), email: email.trim() || null, isActive: true } : null,
      }) });
      await boNho.invalidateQueries(); dong();
    } catch (loiNhanDuoc) { datLoi((loiNhanDuoc as { message?: string }).message ?? 'Không thể đăng ký học viên.'); }
    finally { datDangLuu(false); }
  }
  return <Modal title="Đăng ký học viên mới" onClose={dong} closeDisabled={dangLuu} wide>
    <form onSubmit={luu}>
      <fieldset disabled={dangLuu} className="space-y-3">
        {loi && <div role="alert" className="alert alert-danger">{loi}</div>}
        <p>Mã học viên được hệ thống tạo tự động: HS0001, HS0002…</p>
        <label>Họ tên học viên<input type="text" required maxLength={100} value={hoTen} onChange={suKien => datHoTen(suKien.target.value)} /></label>
        <div className="modal-form-grid">
          <label>Ngày sinh<input type="date" required value={ngaySinh} onChange={suKien => datNgaySinh(suKien.target.value)} /></label>
          <label>Giới tính<select value={gioiTinh} onChange={suKien => datGioiTinh(suKien.target.value)}><option value="">Chưa cung cấp</option><option value="1">Nam</option><option value="2">Nữ</option><option value="3">Khác</option></select></label>
        </div>
        <label>Lưu ý học tập<textarea maxLength={500} value={ghiChu} onChange={suKien => datGhiChu(suKien.target.value)} /></label>
        <h3 className="border-t pt-3">Người giám hộ</h3>
        <p className="text-sm">Một người giám hộ có thể liên kết với nhiều học viên.</p>
        <div className="flex gap-4 flex-wrap">
          <label><input type="radio" name="loaiNguoiGiamHo" checked={!taoMoi} onChange={() => datTaoMoi(false)} /> Chọn người có sẵn</label>
          <label><input type="radio" name="loaiNguoiGiamHo" checked={taoMoi} onChange={() => datTaoMoi(true)} /> Tạo người mới</label>
        </div>
        {taoMoi ? <>
          <label>Họ tên người giám hộ<input type="text" required maxLength={100} value={tenNguoiGiamHo} onChange={suKien => datTenNguoiGiamHo(suKien.target.value)} /></label>
          <label>Số điện thoại<input type="tel" required maxLength={20} value={dienThoai} onChange={suKien => datDienThoai(suKien.target.value)} /></label>
          <label>Email<input type="email" maxLength={100} value={email} onChange={suKien => datEmail(suKien.target.value)} /></label>
        </> : <>
          <label>Tìm theo tên, điện thoại hoặc email<input type="text" value={tuKhoa} onChange={suKien => { datTuKhoa(suKien.target.value); datTrang(1); }} /></label>
          {nguoiDaChon && <p role="status">Đã chọn: <strong>{nguoiDaChon.fullName} · {nguoiDaChon.phone}</strong></p>}
          {danhSach.isFetching && <p>Đang tải…</p>}
          {danhSach.isError && <div role="alert">Không tải được người giám hộ. <button type="button" onClick={() => void danhSach.refetch()}>Thử lại</button></div>}
          <div className="max-h-40 overflow-y-auto border p-2">
            {danhSach.data?.items.map(nguoi => <label key={nguoi.id} className="flex gap-2 py-2"><input type="radio" name="nguoiGiamHo" checked={nguoiDaChon?.id === nguoi.id} onChange={() => datNguoiDaChon(nguoi)} /><span>{nguoi.fullName} · {nguoi.phone}{nguoi.email ? ` · ${nguoi.email}` : ''}</span></label>)}
            {!danhSach.isFetching && !danhSach.isError && !danhSach.data?.items.length && <p>Không tìm thấy. Bạn có thể chọn “Tạo người mới”.</p>}
          </div>
          <Pagination totalItems={danhSach.data?.totalItems ?? 0} page={trang} onPageChange={datTrang} itemLabel="người giám hộ" />
        </>}
        <label>Quan hệ với học viên<input type="text" required maxLength={50} placeholder="Ví dụ: Bố, Mẹ, Ông, Bà" value={quanHe} onChange={suKien => datQuanHe(suKien.target.value)} /></label>
        <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={dong}>Hủy</button><button className="btn btn-primary">{dangLuu ? 'Đang đăng ký…' : 'Đăng ký học viên'}</button></div>
      </fieldset>
    </form>
  </Modal>;
}
