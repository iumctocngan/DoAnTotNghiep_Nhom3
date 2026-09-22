using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Payments;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

/// <summary>Cung cấp API quản lý khoản thanh toán và phiếu thu.</summary>
[ApiController]
[Route("api/payments")]
[Authorize(Roles = $"{UserRole.Admin},{UserRole.Accountant}")]
public class PaymentController(IDichVuPayment dichVuPayment) : ControllerBase
{
    /// <summary>Lấy danh sách khoản thanh toán theo bộ lọc.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PhanHoiPayment>>> LayDanhSachPayment(
        [FromQuery] int? invoiceId,
        [FromQuery] int? studentId,
        [FromQuery] PaymentStatus? status,
        [FromQuery] PaymentMethod? method,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await dichVuPayment.LayDanhSachPaymentAsync(
            invoiceId, studentId, status, method, fromDate, toDate,
            page, pageSize, cancellationToken));
    }

    /// <summary>Lấy chi tiết khoản thanh toán theo mã.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PhanHoiPayment>> LayChiTietPayment(
        [FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        return Ok(await dichVuPayment.LayChiTietPaymentAsync(id, cancellationToken));
    }

    /// <summary>Ghi nhận khoản thanh toán cho hóa đơn.</summary>
    [HttpPost("/api/invoices/{invoiceId:int}/payments")]
    public async Task<ActionResult<PhanHoiPayment>> TaoPayment(
        [FromRoute] int invoiceId,
        [FromBody] YeuCauTaoPayment request,
        CancellationToken cancellationToken = default)
    {
        var payment = await dichVuPayment.TaoPaymentAsync(invoiceId, request, cancellationToken);
        return CreatedAtAction(nameof(LayChiTietPayment), new { id = payment.Id }, payment);
    }

    /// <summary>Hủy khoản thanh toán, không xóa vật lý dữ liệu.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<PhanHoiPayment>> HuyPayment(
        [FromRoute] int id,
        [FromBody] YeuCauHuyPayment request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await dichVuPayment.HuyPaymentAsync(id, request, cancellationToken));
    }
}
