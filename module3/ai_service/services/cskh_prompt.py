"""Prompt và schema đầu ra cho chatbot CSKH.

LLM chỉ được dùng để diễn đạt lại context đã truy xuất. Mọi dữ liệu số và
quyết định nghiệp vụ vẫn được lấy trực tiếp từ SQL Server.
Hỗ trợ nén ngữ cảnh (Context Compaction) để tối ưu hóa lượng token đầu vào.
"""
import json

from ai_service.schemas.cskh_schemas import StudentContext


OUT_OF_DOMAIN_MESSAGE = (
    "Tôi chỉ hỗ trợ tư vấn CSKH liên quan đến tình hình học tập, chuyên cần, "
    "học phí và nguy cơ tái phí của học viên CMS EDU."
)

_CSKH_TERMS = (
    "học", "điểm", "nhận xét", "chuyên cần", "vắng", "lớp", "giáo viên",
    "học phí", "công nợ", "thanh toán", "tái phí", "gia hạn", "rủi ro",
    "phụ huynh", "học viên", "cms edu",
)


def is_cskh_question(question: str) -> bool:
    """Chặn câu hỏi lạc đề trước khi chuyển sang LLM."""
    normalized = question.casefold()
    return any(term in normalized for term in _CSKH_TERMS)

SYSTEM_PROMPT = """Bạn là chuyên viên Tư vấn Giáo dục và Chăm sóc Khách hàng cấp cao tại CMS EDU.

Bạn CHỈ được sử dụng dữ liệu trong khối STUDENT_CONTEXT. Không dùng kiến thức bên ngoài,
không suy diễn về gia đình, không bịa điểm, lớp, số buổi, số tiền hoặc nguyên nhân rủi ro.
Không được tự cộng, trừ, nhân, chia hoặc suy ra số liệu mới. Các số liệu trong context là
kết quả đã được SQL Server tính sẵn. Nếu một trường là null hoặc được nêu trong
missing_data_notes, hãy ghi đúng "Chưa có dữ liệu" và đề nghị CSKH xác minh với người phụ trách.
Chỉ phân tích nguyên nhân rủi ro từ renewal_prediction.top_drivers; không quy nguyên nhân
tài chính nếu các driver không nêu dữ liệu tài chính.

Trả về DUY NHẤT JSON hợp lệ, không markdown, theo đúng 4 khóa:
{
  "summary": "2-3 câu ngắn",
  "risk_analysis": "phân tích chỉ từ Top SHAP Drivers hoặc Chưa có dữ liệu",
  "action_plan": "hành động chuẩn bị trước khi liên hệ",
  "suggested_call_script": ["Chào hỏi", "Thông báo tình trạng", "Thăm dò khó khăn", "Đề xuất lộ trình", "Cam kết hỗ trợ"]
}
Kịch bản cần trực diện, đồng cảm và không hứa hẹn kết quả ngoài dữ liệu.
"""


def build_prompt(context: StudentContext, question: str, compact: bool = True) -> str:
    """Tạo prompt bất biến từ snapshot context.
    Khi compact=True (mặc định), áp dụng Tầng 2 Context Compaction để tiết kiệm 20% - 35% Input Token.
    """
    if compact:
        from ai_service.services.cskh_cache import compact_student_context
        context_dict = compact_student_context(context)
    else:
        context_dict = context.model_dump(mode="json")

    context_json = json.dumps(context_dict, ensure_ascii=False, separators=(",", ":"))
    return f"{SYSTEM_PROMPT}\nSTUDENT_CONTEXT:\n{context_json}\n\nCâu hỏi của CSKH: {question.strip()}"
