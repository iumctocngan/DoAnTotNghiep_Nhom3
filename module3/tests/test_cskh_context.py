"""Kiểm thử groundedness cho Context Retriever Sprint 3."""
from datetime import date

import pytest

from ai_service.services.context_retriever import retrieve_student_context
from ai_service.services.cskh_prompt import OUT_OF_DOMAIN_MESSAGE, build_prompt, is_cskh_question


class _Result:
    def __init__(self, row=None, scalars=None):
        self.row = row
        self._scalars = scalars or []

    def mappings(self):
        return self

    def first(self):
        return self.row

    def scalars(self):
        return self

    def all(self):
        return self._scalars


class _Db:
    def __init__(self, results):
        self.results = iter(results)
        self.parameters = []

    def execute(self, _query, parameters):
        self.parameters.append(parameters)
        return next(self.results)


def test_context_uses_exact_student_id_and_keeps_missing_values_explicit():
    db = _Db([
        _Result({"Id": 7, "StudentCode": "HS007", "FullName": "Nguyễn Minh"}),
        _Result({"Id": 11, "ClassName": "M-L2-01", "LevelName": "Level 2", "TeacherName": "Cô Lan"}),
        _Result({"FullName": "Nguyễn An", "Phone": "0900000000"}),
        _Result({"TotalCount": 2, "PresentCount": 1}),
        _Result(scalars=[2, 2, 1]),
        _Result({"CurrentDebt": 150000, "DueDate": date(2026, 10, 1), "PeriodEnd": date(2026, 12, 1)}),
        _Result({"Content": "Cần tập trung hơn"}),
        _Result({"RenewalProbability": 0.4, "RiskLevel": 2, "TopDriversJson": '[{"feature":"attendance"}]', "CreatedAt": None}),
    ])

    context = retrieve_student_context(db, 7)

    assert all(parameters["student_id"] == 7 for parameters in db.parameters if "student_id" in parameters)
    assert context.attendance.attendance_rate_last_60d == 0.5
    assert context.attendance.consecutive_absent_count == 2
    assert context.financial.current_debt == 150000
    assert context.renewal_prediction.risk_level == "MediumRisk"
    assert context.renewal_prediction.top_drivers == [{"feature": "attendance"}]
    assert "Nguyễn Minh" in build_prompt(context, "Tình hình hiện tại?")


def test_context_rejects_unknown_student_without_guessing():
    with pytest.raises(ValueError, match="Không tìm thấy dữ liệu học viên"):
        retrieve_student_context(_Db([_Result()]), 999)


def test_out_of_domain_question_is_rejected_before_llm_call():
    assert not is_cskh_question("Kể một câu chuyện về vũ trụ")
    assert "CMS EDU" in OUT_OF_DOMAIN_MESSAGE
