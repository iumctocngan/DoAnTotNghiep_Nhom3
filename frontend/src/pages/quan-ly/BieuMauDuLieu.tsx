import { useState } from 'react';
import { Modal } from '../../components/common/Modal';
import { apiClient } from '../../api/apiClient';

export type BanGhi = Record<string, string | number | boolean | null>;
export type TruongNhap = { ten: string; nhan: string; loai?: string; tuyChon?: [string | number, string][]; khongBatBuoc?: boolean; toiDa?: number };
export type LenhDuLieu = { tieuDe: string; duongDan: string; phuongThuc: 'POST' | 'PUT' | 'DELETE'; truong?: TruongNhap[]; duLieu?: BanGhi };

export function BieuMauDuLieu({ lenh, dong, daLuu }: { lenh: LenhDuLieu; dong: () => void; daLuu: () => Promise<void> }) {
  const [giaTri, datGiaTri] = useState<BanGhi>(lenh.duLieu ?? {});
  const [dangLuu, datDangLuu] = useState(false);
  const [loi, datLoi] = useState('');
  async function luu(suKien: React.FormEvent) {
    suKien.preventDefault(); datDangLuu(true); datLoi('');
    const duLieu: BanGhi = {};
    for (const truong of lenh.truong ?? []) {
      const giaTriNhap = giaTri[truong.ten];
      duLieu[truong.ten] = giaTriNhap === '' || giaTriNhap === undefined ? null
        : truong.loai === 'number' ? Number(giaTriNhap)
        : truong.loai === 'boolean' ? String(giaTriNhap) === 'true'
        : truong.loai === 'time' && String(giaTriNhap).length === 5 ? `${giaTriNhap}:00` : giaTriNhap;
    }
    try {
      await apiClient(lenh.duongDan, { method: lenh.phuongThuc, ...(lenh.truong ? { body: JSON.stringify(duLieu) } : {}) });
      await daLuu(); dong();
    } catch (loiNhanDuoc) { datLoi((loiNhanDuoc as { message?: string }).message ?? 'Không thể lưu dữ liệu.'); }
    finally { datDangLuu(false); }
  }
  return <Modal title={lenh.tieuDe} onClose={dong} closeDisabled={dangLuu}>
    <form onSubmit={luu}>
      {loi && <div role="alert" className="alert alert-danger">{loi}</div>}
      {!lenh.truong?.length && <p>Xác nhận thực hiện thao tác này?</p>}
      {lenh.truong?.map(truong => <label key={truong.ten} className="block mb-3">{truong.nhan}
        {truong.tuyChon ? <select required={!truong.khongBatBuoc} value={String(giaTri[truong.ten] ?? '')} onChange={suKien => datGiaTri({ ...giaTri, [truong.ten]: suKien.target.value })}>
          <option value="">— Chọn —</option>
          {giaTri[truong.ten] != null && giaTri[truong.ten] !== '' && !truong.tuyChon.some(([ma]) => String(ma) === String(giaTri[truong.ten])) && <option value={String(giaTri[truong.ten])}>Giá trị hiện tại: {String(giaTri[truong.ten])}</option>}
          {truong.tuyChon.map(([ma, nhan]) => <option key={String(ma)} value={String(ma)}>{nhan}</option>)}
        </select> : truong.loai === 'textarea' ? <textarea required={!truong.khongBatBuoc} maxLength={truong.toiDa ?? 2000} rows={4} value={String(giaTri[truong.ten] ?? '')} onChange={suKien => datGiaTri({ ...giaTri, [truong.ten]: suKien.target.value })} />
        : <input type={truong.loai ?? 'text'} required={!truong.khongBatBuoc} min={truong.loai === 'number' ? 1 : undefined} maxLength={truong.toiDa ?? 100} value={String(giaTri[truong.ten] ?? '')} onChange={suKien => datGiaTri({ ...giaTri, [truong.ten]: suKien.target.value })} />}
      </label>)}
      <div className="modal-footer"><button type="button" className="btn btn-secondary" disabled={dangLuu} onClick={dong}>Hủy</button><button className="btn btn-primary" disabled={dangLuu}>{dangLuu ? 'Đang lưu…' : 'Xác nhận'}</button></div>
    </form>
  </Modal>;
}
