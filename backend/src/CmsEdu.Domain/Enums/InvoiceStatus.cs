namespace CmsEdu.Domain.Enums;

public enum InvoiceStatus
{
    Issued    = 1, // Đã phát hành, chưa có thanh toán nào được xác nhận
    Cancelled = 2, // Đã hủy (không xóa vật lý)
    Draft     = 3, // Nháp, chưa phát hành chính thức
    Partial   = 4, // Đã thanh toán một phần
    Paid      = 5, // Đã thanh toán đủ
    Overdue   = 6  // Quá hạn, chưa thanh toán đủ
}
