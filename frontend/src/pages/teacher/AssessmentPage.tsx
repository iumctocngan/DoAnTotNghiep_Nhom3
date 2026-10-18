import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../api/apiClient';

type Criterion = { name: string; weight: number; clo: string; description: string };
type Rubric = { id: number; name: string; criteria: Criterion[] };
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
  const [name, setname] = useState('');
  const [criteria, setCriteria] = useState<Criterion[]>([{ name: '', weight: 100, clo: '', description: '' }]);
  const [title, setTitle] = useState('');
  const [date, setDate] = useState(new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()));
  const [scores, setScores] = useState<number[]>([]);
  const [drafts, setDrafts] = useState<Record<number, string>>({});
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const classes = useQuery({ queryKey: ['assessment-classes'], queryFn: () => apiClient<{ id: number; name: string }[]>(`${root}/classes`) });
  const students = useQuery({ queryKey: ['assessment-students', classId], enabled: !!classId, queryFn: () => apiClient<{ id: number; fullName: string }[]>(`${root}/classes/${classId}/students`) });
  const rubrics = useQuery({ queryKey: ['assessment-rubrics', classId], enabled: !!classId, queryFn: () => apiClient<Rubric[]>(`${root}/classes/${classId}/rubrics`) });
  const report = useQuery({ queryKey: ['assessment-report', studentId], enabled: !!studentId, queryFn: () => apiClient<Report>(`${root}/enrollments/${studentId}/report`) });
  const rubric = rubrics.data?.find(x => x.id === rubricId);
  async function run(action: () => Promise<unknown>) {
    setBusy(true); setMessage('');
    try { await action(); await client.invalidateQueries({ queryKey: ['assessment-report', studentId] }); await client.invalidateQueries({ queryKey: ['assessment-rubrics', classId] }); setMessage('Đã lưu thành công.'); }
    catch (e) { setMessage((e as { message?: string }).message ?? 'Không thể lưu.'); }
    finally { setBusy(false); }
  }
  const post = (path: string, body: unknown) => apiClient(`${root}${path}`, { method: 'POST', body: JSON.stringify(body) });
  return <div className="assessment-page">
    <h1>Đánh giá học sinh bằng AI</h1><p>Chấm điểm · Rubric · Chuẩn đầu ra CLO · Theo dõi tiến bộ</p>
    <label>Lớp phụ trách<select value={classId} disabled={busy} onChange={e => { setClassId(+e.target.value); setStudentId(0); setRubricId(0); setScores([]); setDrafts({}); }}><option value={0}>Chọn lớp</option>{classes.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
    <label>Học viên<select value={studentId} disabled={busy || !classId} onChange={e => { setStudentId(+e.target.value); setDrafts({}); }}><option value={0}>Chọn học viên</option>{students.data?.map(x => <option key={x.id} value={x.id}>{x.fullName}</option>)}</select></label>
    {[classes, students, rubrics, report].some(x => x.isError) && <p role="alert">Không thể tải dữ liệu. Vui lòng tải lại trang.</p>}
    {message && <p role="status">{message}</p>}
    {!!classId && <details><summary>Tạo Rubric / tiếp nhận tiêu chí từ Module 2</summary><form onSubmit={e => { e.preventDefault(); void run(() => post('/rubrics', { classId, name, criteria })); }}>
      <label>Tên Rubric<input required maxLength={200} value={name} onChange={e => setname(e.target.value)} /></label>
      {criteria.map((c, i) => <fieldset key={i}><legend>Tiêu chí {i + 1}</legend>{(['name', 'clo', 'description'] as const).map(key => <label key={key}>{ { name: 'Tên tiêu chí', clo: 'Mã CLO', description: 'Mô tả / nội dung cần ôn' }[key]}<input required maxLength={key === 'description' ? 1000 : key === 'clo' ? 50 : 200} value={c[key]} onChange={e => setCriteria(criteria.map((v, j) => j === i ? { ...v, [key]: e.target.value } : v))} /></label>)}<label>Trọng số (%)<input type="number" min="0.01" max="100" step="0.01" required value={c.weight} onChange={e => setCriteria(criteria.map((v, j) => j === i ? { ...v, weight: +e.target.value } : v))} /></label><button type="button" disabled={criteria.length === 1} onClick={() => setCriteria(criteria.filter((_, j) => j !== i))}>Xóa tiêu chí</button></fieldset>)}
      <p>Tổng trọng số: {criteria.reduce((sum, c) => sum + c.weight, 0)}%</p><button type="button" disabled={criteria.length >= 50} onClick={() => setCriteria([...criteria, { name: '', weight: 0, clo: '', description: '' }])}>Thêm tiêu chí</button> <button disabled={busy}>Lưu Rubric</button>
    </form></details>}
    {!!studentId && <><section><h2>Chấm điểm theo Rubric</h2><form onSubmit={e => { e.preventDefault(); void run(() => post('', { enrollmentId: studentId, rubricId, title, date, scores })); }}>
      <label>Bài đánh giá<input required maxLength={200} value={title} onChange={e => setTitle(e.target.value)} /></label><label>Ngày đánh giá<input type="date" required value={date} onChange={e => setDate(e.target.value)} /></label>
      <label>Rubric<select required value={rubricId || ''} onChange={e => { const id = +e.target.value; setRubricId(id); setScores(rubrics.data?.find(x => x.id === id)?.criteria.map(() => 0) ?? []); }}><option value="">Chọn Rubric</option>{rubrics.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      {rubric?.criteria.map((c, i) => <label key={i}>{c.name} ({c.weight}% · {c.clo})<input required type="number" min="0" max="10" step="0.01" value={scores[i] ?? 0} onChange={e => setScores(scores.map((v, j) => j === i ? +e.target.value : v))} /></label>)}<button disabled={busy || !rubricId}>Lưu điểm</button>
    </form></section>
    {report.isFetching && <p>Đang tải báo cáo…</p>}
    {report.data && <section><h2>Báo cáo học tập — {report.data.fullName}</h2>
      <p>Điểm trung bình: {report.data.average ?? 'Chưa có'} / 10 · Chuyên cần: {report.data.attendanceRate == null ? 'Chưa có' : `${report.data.attendanceRate}%`} · Thay đổi đầu/cuối: {report.data.progress ?? 'Cần ít nhất 2 bài'} · Cảnh báo: { { High: 'Cao', Medium: 'Trung bình', Low: 'Thấp', Unknown: 'Chưa đủ dữ liệu' }[report.data.risk] }</p>
      <h3>Báo cáo CLO / năng lực</h3><table><thead><tr><th>CLO</th><th>Mức đạt</th><th>Đánh giá</th></tr></thead><tbody>{report.data.cloReport.map(c => <tr key={c.code}><td>{c.code}</td><td>{c.attainment}%</td><td>{c.attainment >= 80 ? 'Điểm mạnh' : c.attainment < 50 ? 'Cần hỗ trợ' : 'Đang phát triển'}</td></tr>)}</tbody></table>
      <h3>Nội dung cần ôn</h3>{report.data.recommendations.length ? <ul>{report.data.recommendations.map(x => <li key={x}>{x}</li>)}</ul> : <p>Chưa có đề xuất từ điểm tiêu chí.</p>}
      <h3>Báo cáo Rubric</h3><table><thead><tr><th>Tiêu chí</th><th>CLO</th><th>Trọng số</th><th>Điểm</th></tr></thead><tbody>{report.data.rubricReport.map((x, i) => <tr key={i}><td>{x.name}</td><td>{x.clo}</td><td>{x.weight}%</td><td>{x.score}/10</td></tr>)}</tbody></table>
      <h3>Lịch sử và nhận xét</h3>{!report.data.history.length && <p>Chưa có bài đánh giá.</p>}{report.data.history.map(x => <article key={x.id}><h4>{x.date} · {x.title} · {x.score}/10</h4><p>{x.approvedAt ? 'Đã duyệt' : 'Bản nháp — chờ giáo viên duyệt'} · {x.feedbackSource ?? 'Chưa sinh nhận xét'}</p><textarea aria-label={`Nhận xét ${x.title}`} maxLength={4000} rows={4} disabled={!!x.approvedAt || busy} value={drafts[x.id] ?? x.feedback ?? ''} onChange={e => setDrafts({ ...drafts, [x.id]: e.target.value })} />{!x.approvedAt && <><button disabled={busy} onClick={() => void run(async () => { const result = await apiClient<{ feedback: string }>(`${root}/${x.id}/feedback`, { method: 'POST' }); setDrafts(d => ({ ...d, [x.id]: result.feedback })); })}>Sinh nhận xét AI</button> <button disabled={busy || !(drafts[x.id] ?? x.feedback)?.trim()} onClick={() => void run(() => post(`/${x.id}/approve`, { feedback: drafts[x.id] ?? x.feedback }))}>Duyệt nhận xét</button></>}</article>)}
      <button type="button" onClick={() => { const url = URL.createObjectURL(new Blob([JSON.stringify(report.data, null, 2)], { type: 'application/json' })); const a = document.createElement('a'); a.href = url; a.download = `bao-cao-${studentId}.json`; a.click(); URL.revokeObjectURL(url); }}>Xuất báo cáo JSON</button>
    </section>}</>}
  </div>;
}


