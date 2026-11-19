import { useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { BangDanhGiaLop, type Criterion, type HocSinhLop, type Rubric } from './BangDanhGiaLop';
import { apiClient } from '../../api/apiClient';
import reportPrintStyles from './assessment-report-print.css?inline';

type BuoiHoc = { id: number; sessionDate: string; startTime: string; endTime: string };
type History = { id: number; title: string; date: string; score: number; feedback: string | null; feedbackSource: string | null; approvedAt: string | null };
type Report = { fullName: string; average: number | null; attendanceRate: number | null; progress: number | null; risk: string;
  recommendations: string[]; cloReport: { code: string; attainment: number }[];
  rubricReport: { name: string; clo: string; score: number; weight: number }[]; history: History[] };
const root = '/api/assessments';
export function AssessmentPage() {
  const client = useQueryClient();
  const [classId, setClassId] = useState(0);
  const [studentId, setStudentId] = useState(0);
  const [rubricId, setRubricId] = useState(0);
  const [name, setName] = useState('');
  const [criteria, setCriteria] = useState<Criterion[]>([{ name: '', weight: 100, clo: '', description: '' }]);
  const [buoiId, setBuoiId] = useState(0);
  const [dangCham, setDangCham] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const reportContent = useRef<HTMLDivElement>(null);
  const [printError, setPrintError] = useState('');
  const classes = useQuery({ queryKey: ['assessment-classes'], queryFn: () => apiClient<{ id: number; name: string }[]>(`${root}/classes`) });
  const students = useQuery({ queryKey: ['assessment-students', classId], enabled: !!classId,
    queryFn: () => apiClient<HocSinhLop[]>(`${root}/classes/${classId}/students`) });
  const sessions = useQuery({ queryKey: ['assessment-sessions', classId], enabled: !!classId,
    queryFn: () => apiClient<BuoiHoc[]>(`${root}/classes/${classId}/sessions`) });
  const rubrics = useQuery({ queryKey: ['assessment-rubrics', classId], enabled: !!classId, queryFn: () => apiClient<Rubric[]>(`${root}/classes/${classId}/rubrics`) });
  const report = useQuery({ queryKey: ['assessment-report', studentId], enabled: !!studentId, queryFn: () => apiClient<Report>(`${root}/enrollments/${studentId}/report`) });

  async function luuVaChonRubric(ten: string, tieuChi: Criterion[]) {
    const saved = await apiClient<Rubric>(`${root}/rubrics`, {
      method: 'POST', body: JSON.stringify({ classId, name: ten, criteria: tieuChi }),
    });
    const queryKey = ['assessment-rubrics', classId];
    await client.cancelQueries({ queryKey });
    client.setQueryData<Rubric[]>(queryKey, items => [...(items ?? []).filter(x => x.id !== saved.id), saved]);
    setRubricId(saved.id);
    void client.invalidateQueries({ queryKey });
    return saved;
  }

  async function luuRubricThuCong() {
    setBusy(true);
    setMessage('');
    try {
      const saved = await luuVaChonRubric(name, criteria);
      setMessage(`Đã lưu và chọn rubric ${saved.name}.`);
    } catch (loi) {
      setMessage((loi as { message?: string }).message ?? 'Không thể lưu rubric.');
    } finally {
      setBusy(false);
    }
  }
  async function nhapTepClo(tep: File) {
    setBusy(true);
    setMessage('');
    try {
      const duLieu = new FormData();
      duLieu.append('tep', tep);
      const ketQua = await apiClient<{ criteria: Criterion[] }>(
        `${root}/classes/${classId}/rubrics/import`, { method: 'POST', body: duLieu });
      const ten = tep.name.replace(/\.xlsx$/i, '').trim().slice(0, 200) || 'Rubric từ Excel';
      const saved = await luuVaChonRubric(ten, ketQua.criteria);
      setMessage(`Đã nhập ${tep.name} và đưa ${saved.criteria.length} tiêu chí vào bảng đánh giá.`);
    } catch (loi) {
      setMessage((loi as { message?: string }).message ?? 'Không thể đọc tệp CLO.');
    } finally {
      setBusy(false);
    }
  }
  function xuatBaoCaoPdf() {
    if (!reportContent.current || !report.data) return;
    setPrintError('');
    const printWindow = window.open('', '_blank');
    if (!printWindow) {
      setPrintError('Hãy cho phép cửa sổ bật lên để xuất báo cáo PDF.');
      return;
    }

    printWindow.opener = null;
    const printDocument = printWindow.document;
    printDocument.documentElement.lang = 'vi';
    printDocument.title = `Báo cáo học tập - ${report.data.fullName}`;
    const styles = printDocument.createElement('style');
    styles.textContent = reportPrintStyles;
    printDocument.head.append(styles);
    printDocument.body.append(printDocument.importNode(reportContent.current, true));
    printWindow.addEventListener('afterprint', () => printWindow.close(), { once: true });
    printWindow.focus();
    printWindow.print();
  }

  const khoa = busy || dangCham;
  const rubric = rubrics.data?.find(x => x.id === rubricId) ?? (rubricId === 0 && rubrics.data?.length === 1 ? rubrics.data[0] : undefined);

  return <div className="assessment-page">
    <h1>Đánh giá và nhận xét theo lớp</h1>
    <label>Lớp phụ trách<select value={classId} disabled={khoa} onChange={e => {
      setClassId(+e.target.value);
      setStudentId(0);
      setRubricId(0);
      setBuoiId(0);
      setName('');
      setCriteria([{ name: '', weight: 100, clo: '', description: '' }]);
      setMessage('');
    }}><option value={0}>Chọn lớp</option>{classes.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
    {[classes, students, sessions, rubrics, report].some(x => x.isError) && <p role="alert">Không thể tải dữ liệu. Vui lòng tải lại trang.</p>}
    {message && <p role="status">{message}</p>}
    {!!classId && <>
      <label>Nhập tệp rubric Excel<input type="file" accept=".xlsx" disabled={khoa} onChange={e => {
        const tep = e.currentTarget.files?.[0];
        e.currentTarget.value = '';
        if (tep) void nhapTepClo(tep);
      }} /></label>
      <details key={classId}><summary>Tạo rubric thủ công</summary><form onSubmit={e => {
        e.preventDefault();
        void luuRubricThuCong();
      }}>
      <fieldset disabled={khoa}>
      <label>Tên Rubric<input required maxLength={200} value={name} onChange={e => setName(e.target.value)} /></label>
      {criteria.map((c, i) => <fieldset key={i}><legend>Tiêu chí {i + 1}</legend>{(['name', 'clo', 'description'] as const).map(key => <label key={key}>{ { name: 'Tên tiêu chí', clo: 'Mã CLO', description: 'Mô tả / nội dung cần ôn' }[key]}<input required maxLength={key === 'description' ? 1000 : key === 'clo' ? 50 : 200} value={c[key]} onChange={e => setCriteria(criteria.map((v, j) => j === i ? { ...v, [key]: e.target.value } : v))} /></label>)}<label>Trọng số (%)<input type="number" min="0.01" max="100" step="0.01" required value={c.weight} onChange={e => setCriteria(criteria.map((v, j) => j === i ? { ...v, weight: +e.target.value } : v))} /></label><button type="button" disabled={criteria.length === 1} onClick={() => setCriteria(criteria.filter((_, j) => j !== i))}>Xóa tiêu chí</button></fieldset>)}
      <p>Tổng trọng số: {criteria.reduce((sum, c) => sum + c.weight, 0)}%</p><button type="button" disabled={criteria.length >= 50} onClick={() => setCriteria([...criteria, { name: '', weight: 0, clo: '', description: '' }])}>Thêm tiêu chí</button> <button disabled={busy}>Lưu Rubric</button>
      </fieldset>
      </form></details>
    </>}
    {!!classId && <>
      <label>Buổi học<select value={buoiId} disabled={khoa || sessions.isPending} onChange={e => setBuoiId(+e.target.value)}>
        <option value={0}>Chọn buổi học đã điểm danh</option>
        {sessions.data?.map(x => <option key={x.id} value={x.id}>
          {x.sessionDate.split('-').reverse().join('/')} · {x.startTime.slice(0, 5)}–{x.endTime.slice(0, 5)}
        </option>)}
      </select></label>
      {sessions.data?.length === 0 && <p>Lớp chưa có buổi học để đánh giá.</p>}
      <label>Rubric<select value={rubric?.id ?? 0} disabled={khoa} onChange={e => setRubricId(+e.target.value)}>
        <option value={0}>Chọn rubric</option>
        {rubrics.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
      </select></label>
      {rubrics.data?.length === 0 && <p>Nhập tệp CLO hoặc tạo rubric để chấm điểm.</p>}
      {students.isPending ? <p role="status">Đang tải danh sách lớp…</p> :
        <BangDanhGiaLop key={`${classId}-${buoiId}-${rubric?.id ?? 0}`} buoiId={buoiId}
          danhSachLop={students.data ?? []} rubric={rubric} khoa={busy} datDangXuLy={setDangCham} />}
      <label>Xem báo cáo cá nhân<select value={studentId} disabled={khoa} onChange={e => {
        setPrintError('');
        setStudentId(+e.target.value);
      }}>
        <option value={0}>Chọn học sinh</option>
        {students.data?.map(x => <option key={x.id} value={x.id}>{x.fullName}</option>)}
      </select></label>
    </>}
    {!!studentId && <>
    {report.isFetching && <p>Đang tải báo cáo…</p>}
    {report.data && <section><div ref={reportContent} className="assessment-report">
      <h2>Báo cáo học tập — {report.data.fullName}</h2>
      <p>Điểm trung bình: {report.data.average ?? 'Chưa có'} / 10 · Chuyên cần: {report.data.attendanceRate == null ? 'Chưa có' : `${report.data.attendanceRate}%`} · Thay đổi đầu/cuối: {report.data.progress ?? 'Cần ít nhất 2 bài'} · Cảnh báo: { { High: 'Cao', Medium: 'Trung bình', Low: 'Thấp', Unknown: 'Chưa đủ dữ liệu' }[report.data.risk] }</p>
      <h3>Báo cáo CLO / năng lực</h3><table><thead><tr><th>CLO</th><th>Mức đạt</th><th>Đánh giá</th></tr></thead><tbody>{report.data.cloReport.map(c => <tr key={c.code}><td>{c.code}</td><td>{c.attainment}%</td><td>{c.attainment >= 80 ? 'Điểm mạnh' : c.attainment < 50 ? 'Cần hỗ trợ' : 'Đang phát triển'}</td></tr>)}</tbody></table>
      <h3>Nội dung cần ôn</h3>{report.data.recommendations.length ? <ul>{report.data.recommendations.map(x => <li key={x}>{x}</li>)}</ul> : <p>Chưa có đề xuất từ điểm tiêu chí.</p>}
      <h3>Báo cáo Rubric</h3><table><thead><tr><th>Tiêu chí</th><th>CLO</th><th>Trọng số</th><th>Điểm</th></tr></thead><tbody>{report.data.rubricReport.map((x, i) => <tr key={i}><td>{x.name}</td><td>{x.clo}</td><td>{x.weight}%</td><td>{x.score}/10</td></tr>)}</tbody></table>
      <h3>Lịch sử và nhận xét</h3>
      {!report.data.history.length && <p>Chưa có bài đánh giá.</p>}
      {report.data.history.map(x => <article key={x.id}>
        <h4>{x.date} · {x.title} · {x.score}/10</h4>
        <p>{x.approvedAt ? 'Đã xác nhận' : 'Chờ xác nhận'}</p>
        {x.feedback && <p>{x.feedback}</p>}
      </article>)}
      </div>
      <button type="button" onClick={xuatBaoCaoPdf}>Xuất báo cáo PDF</button>
      {printError && <p role="alert">{printError}</p>}
    </section>}</>}
  </div>;
}


