import { useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { BangDanhGiaLop, type Criterion, type HocSinhLop, type Rubric } from './BangDanhGiaLop';
import { apiClient } from '../../api/apiClient';
import reportPrintStyles from './assessment-report-print.css?inline';

type BuoiHoc = { id: number; sessionDate: string; startTime: string; endTime: string; lessonName?: string };
type History = { id: number; title: string; date: string; sessionId: number | null; score: number;
  feedback: string | null; approvedAt: string | null; lesson: { name: string; objective: string | null } | null;
  criteria: (Criterion & { score: number })[] };
type Report = { fullName: string; className: string; risk: string; history: History[] };
const root = '/api/assessments';
const ngayVietNam = (date: string) => date.split('-').reverse().join('/');
const mucDanhGia = (score: number) => score >= 9 ? 'Rất tốt' : score >= 8 ? 'Tốt' : score >= 6.5 ? 'Đạt' : score >= 5 ? 'Cần cải thiện' : 'Chưa đạt';
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
  const [reportAssessmentId, setReportAssessmentId] = useState(0);
  const classes = useQuery({ queryKey: ['assessment-classes'], queryFn: () => apiClient<{ id: number; name: string }[]>(`${root}/classes`) });
  const students = useQuery({ queryKey: ['assessment-students', classId], enabled: !!classId,
    queryFn: () => apiClient<HocSinhLop[]>(`${root}/classes/${classId}/students`) });
  const sessions = useQuery({ queryKey: ['assessment-sessions', classId], enabled: !!classId,
    queryFn: () => apiClient<BuoiHoc[]>(`${root}/classes/${classId}/sessions`) });
  const rubrics = useQuery({ queryKey: ['assessment-rubrics', classId], enabled: !!classId, queryFn: () => apiClient<Rubric[]>(`${root}/classes/${classId}/rubrics`) });
  const lessonRubric = useQuery({ queryKey: ['assessment-lesson-rubric', classId, buoiId], enabled: !!buoiId,
    queryFn: () => apiClient<Rubric | null>(`${root}/sessions/${buoiId}/lesson-rubric`, { method: 'POST' }) });
  const report = useQuery({ queryKey: ['assessment-report', studentId], enabled: !!studentId, queryFn: () => apiClient<Report>(`${root}/enrollments/${studentId}/report`) });
  const history = report.data?.history ?? [];
  const selectedAssessment = history.find(item => item.id === reportAssessmentId) ??
    history.findLast(item => item.sessionId === buoiId) ?? history.at(-1);

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
    printDocument.title = `Báo cáo học tập - ${report.data.fullName} - ${selectedAssessment?.date ?? ''}`;
    const styles = printDocument.createElement('style');
    styles.textContent = reportPrintStyles;
    printDocument.head.append(styles);
    printDocument.body.append(printDocument.importNode(reportContent.current, true));
    printWindow.addEventListener('afterprint', () => printWindow.close(), { once: true });
    printWindow.focus();
    printWindow.print();
  }

  const khoa = busy || dangCham;
  const rubric = rubrics.data?.find(x => x.id === rubricId) ?? lessonRubric.data ??
    ((!buoiId || lessonRubric.isSuccess) && rubrics.data?.length === 1 ? rubrics.data[0] : undefined);
  const improvements = selectedAssessment?.criteria.filter(c => c.score < 8) ?? [];

  return <div className="assessment-page">
    <h1>Đánh giá và nhận xét theo lớp</h1>
    <label>Lớp phụ trách<select value={classId} disabled={khoa} onChange={e => {
      setClassId(+e.target.value);
      setStudentId(0);
      setReportAssessmentId(0);
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
      <label>Buổi học<select value={buoiId} disabled={khoa || sessions.isPending} onChange={e => {
        setBuoiId(+e.target.value);
        setRubricId(0);
        setReportAssessmentId(0);
      }}>
        <option value={0}>Chọn buổi học đã điểm danh</option>
        {sessions.data?.map(x => <option key={x.id} value={x.id}>
          {ngayVietNam(x.sessionDate)} · {x.startTime.slice(0, 5)}–{x.endTime.slice(0, 5)}{x.lessonName ? ` · ${x.lessonName}` : ''}
        </option>)}
      </select></label>
      {sessions.data?.length === 0 && <p>Lớp chưa có buổi học để đánh giá.</p>}
      <label>Rubric<select value={rubric?.id ?? 0} disabled={khoa || (!!buoiId && lessonRubric.isPending)} onChange={e => setRubricId(+e.target.value)}>
        <option value={0}>Chọn rubric</option>
        {lessonRubric.data && <option value={lessonRubric.data.id}>{lessonRubric.data.name} · CLO theo bài học</option>}
        {rubrics.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
      </select></label>
      {!!buoiId && lessonRubric.isPending && <p role="status">Đang lấy CLO của bài học…</p>}
      {lessonRubric.isError && <p role="alert">Không tải được CLO theo bài học. <button type="button" onClick={() => void lessonRubric.refetch()}>Thử lại</button></p>}
      {!!buoiId && lessonRubric.isSuccess && !lessonRubric.data &&
        <p>Buổi này chưa có CLO đã lưu. Kiểm tra bài học gắn với buổi, hoặc chọn rubric có sẵn.</p>}
      {students.isPending ? <p role="status">Đang tải danh sách lớp…</p> :
        <BangDanhGiaLop key={`${classId}-${buoiId}-${rubric?.id ?? 0}`} buoiId={buoiId}
          danhSachLop={students.data ?? []} rubric={rubric} khoa={busy} datDangXuLy={setDangCham} />}
      <label>Xem báo cáo cá nhân<select value={studentId} disabled={khoa} onChange={e => {
        setPrintError('');
        setReportAssessmentId(0);
        setStudentId(+e.target.value);
      }}>
        <option value={0}>Chọn học sinh</option>
        {students.data?.map(x => <option key={x.id} value={x.id}>{x.fullName}</option>)}
      </select></label>
    </>}
    {!!studentId && <>
    {report.isFetching && <p>Đang tải báo cáo…</p>}
    {report.data && !selectedAssessment && <p>Học sinh chưa có bài đánh giá.</p>}
    {report.data && selectedAssessment && <section>
      <label>Buổi đánh giá<select value={selectedAssessment.id} onChange={e => setReportAssessmentId(+e.target.value)}>
        {history.map(item => <option key={item.id} value={item.id}>{ngayVietNam(item.date)} · {item.title}</option>)}
      </select></label>
      <div ref={reportContent} className="assessment-report">
      <h2>{report.data.fullName} – {report.data.className} – {ngayVietNam(selectedAssessment.date)}</h2>
      <p>Điểm trung bình: {selectedAssessment.score}/10 · Mức độ đánh giá: {mucDanhGia(selectedAssessment.score)}
        {report.data.risk === 'High' ? ' · Mức độ cảnh báo: Cao' : report.data.risk === 'Medium' ? ' · Mức độ cảnh báo: Trung bình' : ''}</p>
      <h3>Nội dung đã học</h3>
      <p><strong>{selectedAssessment.lesson?.name ?? selectedAssessment.title}</strong></p>
      {selectedAssessment.lesson?.objective && <p>{selectedAssessment.lesson.objective}</p>}
      <ul>{selectedAssessment.criteria.filter(c => !c.fixedCriterion).map((c, i) => <li key={i}>{c.clo} · {c.description}</li>)}</ul>
      <h3>Bảng đánh giá</h3>
      <table><thead><tr><th>Tiêu chí</th><th>CLO</th><th>Trọng số</th><th>Điểm</th></tr></thead><tbody>
        {selectedAssessment.criteria.map((c, i) => <tr key={i}><td>{c.name}</td><td>{c.clo}</td><td>{c.weight}%</td><td>{c.score}/10</td></tr>)}
      </tbody></table>
      <h3>Nhận xét</h3>
      {selectedAssessment.feedback ? <>
        <p>{selectedAssessment.approvedAt ? 'Đã xác nhận' : 'Chờ xác nhận'}</p>
        <p style={{ whiteSpace: 'pre-line' }}>{selectedAssessment.feedback}</p>
      </> : <p>Chưa có nhận xét.</p>}
      <h3>Điểm cần cải thiện và luyện tập</h3>
      {improvements.length ? <ul>{improvements.map((c, i) => {
        const nextLevel = c.mucDanhGia?.[Math.floor(c.score / 2)];
        const practice = c.numericScore ? 'Làm lại các câu sai trong sách và ghi rõ cách giải.' : nextLevel || c.description;
        return <li key={i}><strong>{c.name} ({c.score}/10)</strong> · {practice}</li>;
      })}</ul> : <p>Không có tiêu chí dưới 8/10.</p>}
      </div>
      <button type="button" onClick={xuatBaoCaoPdf}>Xuất báo cáo PDF</button>
      {printError && <p role="alert">{printError}</p>}
    </section>}</>}
  </div>;
}


