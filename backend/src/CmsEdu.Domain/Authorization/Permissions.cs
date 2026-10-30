namespace CmsEdu.Domain.Authorization;

public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Students
    {
        public const string View = "students.view";
        public const string Create = "students.create";
        public const string Edit = "students.edit";
        public const string Delete = "students.delete";
    }

    public static class Guardians
    {
        public const string View = "guardians.view";
        public const string Manage = "guardians.manage";
        public const string Delete = "guardians.delete";
    }

    public static class Classes
    {
        public const string View = "classes.view";
        public const string Manage = "classes.manage";
        public const string Delete = "classes.delete";
    }

    public static class Enrollments
    {
        public const string View = "enrollments.view";
        public const string Manage = "enrollments.manage";
        public const string Delete = "enrollments.delete";
        public const string Remarks = "enrollments.remarks";
    }

    public static class Sessions
    {
        public const string View = "sessions.view";
        public const string Manage = "sessions.manage";
        public const string Attendance = "sessions.attendance";
    }

    public static class Invoices
    {
        public const string View = "invoices.view";
        public const string Manage = "invoices.manage";
        public const string Payment = "invoices.payment";
    }

    public static class Curriculum
    {
        public const string View = "curriculum.view";
        public const string Manage = "curriculum.manage";
    }

    public static class Teacher
    {
        public const string Classes = "teacher.classes";
    }

    public static class Assessments
    {
        public const string View = "assessments.view";
        public const string Manage = "assessments.manage";
    }

    public static class ClassAssistant
    {
        public const string View = "assistant.view";
        public const string Chat = "assistant.chat";
    }

    public static class Staff
    {
        public const string View = "staff.view";
        public const string Manage = "staff.manage";
    }

    public static class Roles
    {
        public const string Manage = "roles.manage";
    }

    public record PermissionDefinition(string Code, string Name, string Group, string Description);

    public static readonly IReadOnlyList<PermissionDefinition> All = new List<PermissionDefinition>
    {
        // Học viên
        new(Students.View, "Xem học viên", "Học viên", "Xem danh sách và chi tiết thông tin học viên"),
        new(Students.Create, "Thêm học viên", "Học viên", "Tạo mới hồ sơ học viên"),
        new(Students.Edit, "Chỉnh sửa học viên", "Học viên", "Chỉnh sửa thông tin hồ sơ học viên"),
        new(Students.Delete, "Xóa học viên", "Học viên", "Xóa hồ sơ học viên khỏi hệ thống"),

        // Người giám hộ
        new(Guardians.View, "Xem người giám hộ", "Người giám hộ", "Xem danh sách và chi tiết người giám hộ"),
        new(Guardians.Manage, "Quản lý người giám hộ", "Người giám hộ", "Thêm, sửa và liên kết người giám hộ với học viên"),
        new(Guardians.Delete, "Xóa người giám hộ", "Người giám hộ", "Xóa người giám hộ khỏi hệ thống"),

        // Lớp học
        new(Classes.View, "Xem lớp học", "Lớp học", "Xem danh sách và thông tin chi tiết lớp học"),
        new(Classes.Manage, "Quản lý lớp học", "Lớp học", "Tạo mới, chỉnh sửa thông tin lớp học"),
        new(Classes.Delete, "Xóa lớp học", "Lớp học", "Xóa lớp học khỏi hệ thống"),

        // Ghi danh & Nhận xét
        new(Enrollments.View, "Xem ghi danh", "Ghi danh", "Xem danh sách học viên ghi danh vào lớp"),
        new(Enrollments.Manage, "Quản lý ghi danh", "Ghi danh", "Ghi danh học viên, bảo lưu, chuyển lớp"),
        new(Enrollments.Delete, "Xóa ghi danh", "Ghi danh", "Hủy bỏ ghi danh của học viên"),
        new(Enrollments.Remarks, "Nhận xét học viên", "Ghi danh", "Ghi nhận xét và đánh giá định kỳ cho học viên"),

        // Buổi học & Điểm danh
        new(Sessions.View, "Xem buổi học", "Buổi học & Điểm danh", "Xem danh sách buổi học và lịch học"),
        new(Sessions.Manage, "Quản lý buổi học", "Buổi học & Điểm danh", "Tạo, sửa, hủy và xếp lịch buổi học"),
        new(Sessions.Attendance, "Điểm danh học viên", "Buổi học & Điểm danh", "Thực hiện điểm danh và hoàn tất buổi học"),

        // Học phí & Hóa đơn
        new(Invoices.View, "Xem hóa đơn & công nợ", "Học phí & Thu chi", "Xem danh sách hóa đơn, công nợ học phí"),
        new(Invoices.Manage, "Quản lý hóa đơn", "Học phí & Thu chi", "Tạo và điều chỉnh hóa đơn học phí"),
        new(Invoices.Payment, "Thu tiền & Phiếu thu", "Học phí & Thu chi", "Ghi nhận thanh toán và hủy phiếu thu"),

        // Chương trình học
        new(Curriculum.View, "Xem chương trình học", "Chương trình học", "Xem các khóa học, cấp độ, bài học"),
        new(Curriculum.Manage, "Quản lý chương trình học", "Chương trình học", "Tạo, sửa, xóa khóa học, cấp độ, bài học"),

        // Giảng dạy & Đánh giá AI
        new(Teacher.Classes, "Lớp phụ trách & Lịch dạy", "Giảng dạy", "Xem danh sách lớp phụ trách và lịch giảng dạy cá nhân"),
        new(Assessments.View, "Xem đánh giá AI & Rubric", "Đánh giá học sinh AI", "Xem báo cáo học tập, CLO và Rubric"),
        new(Assessments.Manage, "Chấm điểm & Duyệt nhận xét AI", "Đánh giá học sinh AI", "Tạo Rubric, chấm điểm bài và duyệt nhận xét AI"),

        // Trợ lý học liệu AI
        new(ClassAssistant.View, "Truy cập Trợ lý học liệu", "Trợ lý học liệu AI", "Truy cập không gian Trợ lý học liệu và tra cứu tài liệu"),
        new(ClassAssistant.Chat, "Hỏi đáp AI với học liệu", "Trợ lý học liệu AI", "Gửi câu hỏi và trò chuyện hỏi đáp AI với tài liệu"),

        // Quản lý nhân sự & Phân quyền
        new(Staff.View, "Xem nhân sự", "Quản trị hệ thống", "Xem danh sách nhân viên trung tâm"),
        new(Staff.Manage, "Quản lý nhân sự", "Quản trị hệ thống", "Thêm nhân viên, đổi vai trò, đổi mật khẩu"),
        new(Roles.Manage, "Phân quyền vai trò", "Quản trị hệ thống", "Cấu hình ma trận quyền hạn cho các vai trò")
    };
}
