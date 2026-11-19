import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/apiClient';

export type Criterion = { name: string; clo: string; weight: number; description: string;
  mucDanhGia?: string[]; numericScore?: boolean; fixedCriterion?: boolean };
export type Rubric = { id: number; name: string; criteria: Criterion[] };
export type HocSinhLop = { id: number; fullName: string; studentCode: string };
type HocSinh = HocSinhLop & { attendance: string };
type BaiDaLuu = { id: number; enrollmentId: number; scores: number[]; feedback: string | null;
  feedbackSource: string | null; approvedAt: string | null };
type Bang = { students: HocSinh[]; grades: BaiDaLuu[] };
type ThuocTinh = {
  buoiId: number; danhSachLop: HocSinhLop[]; rubric?: Rubric;
  khoa: boolean; datDangXuLy: (busy: boolean) => void;
};

const goc = '/api/assessments';
const mucDo = ['Chưa đạt', 'Cần cải thiện', 'Đạt', 'Tốt', 'Rất tốt'];
const diemDanh: Record<string, string> = {
  Present: 'Có mặt', Absent: 'Vắng mặt', Unmarked: 'Chưa điểm danh',
  OutsideEnrollment: 'Ngoài thời gian ghi danh', Cancelled: 'Buổi học đã hủy', NoSession: 'Chưa chọn buổi học',
};

export function BangDanhGiaLop({ buoiId, danhSachLop, rubric, khoa, datDangXuLy }: ThuocTinh) {
  const cache = useQueryClient();
  const [boChon, datBoChon] = useState<Set<number>>(new Set());
  const [diemNhap, datDiemNhap] = useState<Record<number, Record<number, number | undefined>>>({});
  const [banSua, datBanSua] = useState<Record<number, string>>({});
  const [loiDong, datLoiDong] = useState<Record<number, string>>({});
  const [loiBang, datLoiBang] = useState('');
  const [tacVu, datTacVu] = useState('');
  const queryKey = ['assessment-matrix', buoiId, rubric?.id ?? 0];
  const { data, isPending, isError, refetch } = useQuery({
    queryKey,
    enabled: !!buoiId,
    queryFn: () => apiClient<Bang>(`${goc}/sessions/${buoiId}/matrix?rubricId=${rubric?.id ?? 0}`),
  });
  const hocSinh = data?.students ?? danhSachLop.map(x => ({ ...x, attendance: 'NoSession' }));
  const bai = new Map(data?.grades.map(x => [x.enrollmentId, x]));
  const coMat = hocSinh.filter(x => x.attendance === 'Present');
  const daChon = coMat.filter(x => !boChon.has(x.id));
  const canLuu = daChon.filter(x => !bai.has(x.id));
  const nhanXet = (x: BaiDaLuu) => banSua[x.id] ?? x.feedback ?? '';
  const banNhapDaChon = daChon.flatMap(x => {
    const daLuu = bai.get(x.id);
    return daLuu && !daLuu.approvedAt ? [daLuu] : [];
  });
  const banNhapCu = (x: BaiDaLuu) => x.feedbackSource?.startsWith('LLM:') &&
    !!x.feedback && x.feedback.trim().split(/\s+/).length < 250;
  const canSinh = banNhapDaChon.filter(x => (!x.feedback && !nhanXet(x).trim()) ||
    (banSua[x.id] == null && banNhapCu(x)));
  const canDuyet = banNhapDaChon.filter(x => nhanXet(x).trim());
  const biKhoa = khoa || !!tacVu;

  async function xuLy(loai: 'luu' | 'sinh' | 'duyet') {
    if (biKhoa || !rubric) return;
    datLoiBang('');
    datLoiDong({});
    const rows = canLuu.map(x => ({
      enrollmentId: x.id,
      scores: rubric.criteria.map((_, i) => diemNhap[x.id]?.[i]),
    }));
    const invalidRows = rows.filter(x => x.scores.some(diem => diem == null || diem < 0 || diem > 10));
    if (loai === 'luu' && invalidRows.length) {
      const message = rubric.criteria.some(criterion => criterion.numericScore)
        ? 'Điền đầy đủ các tiêu chí và điểm sách bài tập từ 0 đến 10.'
        : 'Chọn mức cho từng tiêu chí trước khi lưu.';
      datLoiDong(Object.fromEntries(invalidRows.map(x => [x.enrollmentId, message])));
      return;
    }

    datDangXuLy(true);
    datTacVu({ luu: 'Đang lưu điểm…', sinh: 'Đang tạo nhận xét…', duyet: 'Đang xác nhận…' }[loai]);
    try {
      if (loai === 'luu') {
        await apiClient(`${goc}/sessions/${buoiId}/rubrics/${rubric.id}/grades`, {
          method: 'POST', body: JSON.stringify({ rows }),
        });
      } else if (loai === 'sinh') {
        let viTri = 0;
        let hoanTat = 0;
        let hetHanMuc = false;
        async function taoNhanXet() {
          while (!hetHanMuc && viTri < canSinh.length) {
            const dong = canSinh[viTri++];
            try {
              await apiClient(`${goc}/${dong.id}/feedback`, { method: 'POST' });
            } catch (loi) {
              const { message, statusCode } = loi as { message?: string; statusCode?: number };
              datLoiDong(truoc => ({ ...truoc, [dong.enrollmentId]: message ?? 'Không tạo được nhận xét.' }));
              if (statusCode === 429) {
                hetHanMuc = true;
                datLoiBang('Dịch vụ AI đã đạt giới hạn lượt gọi. Hãy thử lại cho các học sinh còn lại sau.');
              }
            } finally {
              hoanTat++;
              datTacVu(`Đã xử lý ${hoanTat}/${canSinh.length} yêu cầu tạo nhận xét…`);
            }
          }
        }
        await Promise.all(Array.from({ length: Math.min(3, canSinh.length) }, taoNhanXet));
      } else {
        for (const [i, dong] of canDuyet.entries()) {
          datTacVu(`Đang xác nhận ${i + 1}/${canDuyet.length}…`);
          try {
            await apiClient(`${goc}/${dong.id}/approve`, {
              method: 'POST', body: JSON.stringify({ feedback: nhanXet(dong) }),
            });
            datBanSua(truoc => {
              const sau = { ...truoc };
              delete sau[dong.id];
              return sau;
            });
          } catch (loi) {
            datLoiDong(truoc => ({ ...truoc, [dong.enrollmentId]: (loi as { message?: string }).message ?? 'Không xử lý được nhận xét.' }));
          }
        }
      }
    } catch (loi) {
      datLoiBang((loi as { message?: string }).message ?? 'Không lưu được bảng đánh giá.');
    } finally {
      await refetch();
      await cache.invalidateQueries({ queryKey: ['assessment-report'] });
      datTacVu('');
      datDangXuLy(false);
    }
  }

  if (buoiId && isPending) return <p role="status">Đang tải bảng đánh giá…</p>;
  if (isError) return <p role="alert">Không tải được bảng đánh giá. <button onClick={() => void refetch()}>Thử lại</button></p>;

  return <section>
    <h2>Bảng đánh giá theo lớp</h2>
    {!rubric && <p>Chọn rubric để đánh giá học sinh có mặt.</p>}
    {!hocSinh.length && <p>Lớp chưa có học sinh.</p>}
    <div className="bang-danh-gia-lop" tabIndex={0} role="region" aria-label="Bảng tiêu chí theo học sinh">
      <table><thead><tr>
        <th scope="col"><input type="checkbox" aria-label="Chọn tất cả học sinh có mặt"
          checked={coMat.length > 0 && daChon.length === coMat.length} disabled={biKhoa || !rubric || !coMat.length}
          onChange={e => datBoChon(e.target.checked ? new Set() : new Set(coMat.map(x => x.id)))} /> Học sinh</th>
        {rubric?.criteria.map((tieuChi, i) => <th scope="col" key={i}>
          {tieuChi.name}<small>{tieuChi.clo} · {tieuChi.weight}%</small>
          <details><summary>Mô tả mức</summary><p>{tieuChi.description}</p>
            {!tieuChi.numericScore && <ol>{mucDo.map((muc, j) => <li key={muc}>{tieuChi.mucDanhGia?.[j] || muc}</li>)}</ol>}
          </details>
        </th>)}
        <th scope="col">Điểm danh và đánh giá</th>
      </tr></thead><tbody>
        {hocSinh.map(hs => {
          const daLuu = bai.get(hs.id);
          const coTheCham = hs.attendance === 'Present';
          const chon = coTheCham && !boChon.has(hs.id);
          return <tr key={hs.id}>
            <th scope="row"><input type="checkbox" aria-label={`Chọn ${hs.fullName}`} checked={chon}
              disabled={biKhoa || !rubric || !coTheCham} onChange={e => {
                const checked = e.target.checked;
                datBoChon(truoc => {
                  const sau = new Set(truoc);
                  if (checked) sau.delete(hs.id);
                  else sau.add(hs.id);
                  return sau;
                });
              }} /> {hs.fullName}<small>{hs.studentCode}</small></th>
            {rubric?.criteria.map((tieuChi, i) => {
              const diem = daLuu?.scores[i] ?? diemNhap[hs.id]?.[i];
              if (tieuChi.numericScore) return <td key={i}>
                <input type="number" min={0} max={10} step="any" placeholder="0–10" value={diem ?? ''}
                  aria-label={`${hs.fullName} · ${tieuChi.name}`} disabled={biKhoa || !chon || !!daLuu} onChange={e => {
                    const value = e.currentTarget.valueAsNumber;
                    datDiemNhap(truoc => ({ ...truoc, [hs.id]: { ...truoc[hs.id], [i]: Number.isFinite(value) ? value : undefined } }));
                  }} />
              </td>;
              return <td key={i}>
                <div className="assessment-levels" role="group" aria-label={`${hs.fullName} · ${tieuChi.name}`}>
                  {mucDo.map((muc, j) => {
                    const giaTri = (j + 1) * 2;
                    const moTa = tieuChi.mucDanhGia?.[j] || muc;
                    return <label key={muc} title={moTa}>
                      <input type="radio" name={`grade-${hs.id}-${i}`} checked={diem === giaTri}
                        aria-label={`Mức ${j + 1} · ${moTa}`} disabled={biKhoa || !chon || !!daLuu}
                        onChange={() => datDiemNhap(truoc => ({ ...truoc, [hs.id]: { ...truoc[hs.id], [i]: giaTri } }))} />
                      {j + 1}
                    </label>;
                  })}
                </div>
              </td>;
            })}
            <td>{diemDanh[hs.attendance]}{daLuu && <small>{daLuu.approvedAt ? 'Đã xác nhận' : 'Đã lưu điểm'}</small>}
              {!coTheCham && <small>Khóa đánh giá</small>}
              {loiDong[hs.id] && <small role="alert">{loiDong[hs.id]}</small>}
            </td>
          </tr>;
        })}
      </tbody></table>
    </div>
    {rubric && <button type="button" disabled={biKhoa || !canLuu.length} onClick={() => void xuLy('luu')}>Lưu điểm đã chọn</button>}
    {tacVu && <p role="status">{tacVu}</p>}
    {loiBang && <p role="alert">{loiBang}</p>}
    {!!bai.size && <div className="nhan-xet-theo-lop">
      <h3>Nhận xét theo lớp</h3>
      <button type="button" disabled={biKhoa || !canSinh.length || !!canLuu.length}
        onClick={() => void xuLy('sinh')}>Tạo nhận xét</button>{' '}
      <button type="button" disabled={biKhoa || !canDuyet.length}
        onClick={() => void xuLy('duyet')}>Xác nhận và lưu nhận xét đã chọn</button>
      {hocSinh.map(hs => {
        const daLuu = bai.get(hs.id);
        if (!daLuu) return null;
        return <article key={daLuu.id}>
          <label><strong>{hs.fullName} · {daLuu.approvedAt ? 'Đã xác nhận' : banNhapCu(daLuu) ? 'Bản nháp AI dưới 250 từ'
            : daLuu.feedback ? 'Chờ xác nhận' : 'Chưa có nhận xét'}</strong>
            <textarea rows={5} value={nhanXet(daLuu)} maxLength={6000}
              disabled={biKhoa || !!daLuu.approvedAt || hs.attendance !== 'Present'}
              onChange={e => datBanSua(truoc => ({ ...truoc, [daLuu.id]: e.target.value }))} />
          </label>
          {loiDong[hs.id] && <p role="alert">{loiDong[hs.id]}</p>}
        </article>;
      })}
    </div>}
  </section>;
}
