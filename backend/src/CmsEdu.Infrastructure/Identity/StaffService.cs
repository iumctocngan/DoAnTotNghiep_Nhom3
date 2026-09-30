using System.Data;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Staff;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Identity;

public class StaffService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IAuthenticationService authenticationService,
    ICurrentUser currentUser) : IStaffService
{
    public async Task<PagedResult<StaffResponse>> GetStaffAsync(
        string? search,
        string? role,
        EmploymentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(page, pageSize);
        if (role is not null && !UserRole.AllRoles.Contains(role))
            throw new ValidationException("Vai trò không hợp lệ.");
        if (status is not null && !Enum.IsDefined(status.Value))
            throw new ValidationException("Trạng thái không hợp lệ.");

        var query = from user in dbContext.Users.AsNoTracking()
                    join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                    join identityRole in dbContext.Roles.AsNoTracking() on userRole.RoleId equals identityRole.Id
                    select new { User = user, Role = identityRole.Name! };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(staff =>
                staff.User.EmployeeCode.Contains(keyword) ||
                staff.User.FullName.Contains(keyword) ||
                staff.User.Email!.Contains(keyword));
        }

        if (role is not null)
            query = query.Where(staff => staff.Role == role);
        if (status is not null)
            query = query.Where(staff => staff.User.EmploymentStatus == status);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(staff => staff.User.EmployeeCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(staff => new StaffResponse(
                staff.User.Id,
                staff.User.EmployeeCode,
                staff.User.FullName,
                staff.User.Email!,
                staff.User.PhoneNumber,
                staff.Role,
                staff.User.EmploymentStatus))
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffResponse>(items, page, pageSize, totalItems);
    }

    public async Task<StaffResponse> GetStaffByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await (from user in dbContext.Users.AsNoTracking()
                      join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                      join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                      where user.Id == id
                      select new StaffResponse(
                          user.Id,
                          user.EmployeeCode,
                          user.FullName,
                          user.Email!,
                          user.PhoneNumber,
                          role.Name!,
                          user.EmploymentStatus))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nhân viên.");
    }

    public async Task<StaffResponse> CreateStaffAsync(
        CreateStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeCode = request.EmployeeCode?.Trim().ToUpperInvariant();
        var fullName = request.FullName?.Trim();
        var email = request.Email?.Trim();
        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        if (string.IsNullOrWhiteSpace(employeeCode) || employeeCode.Length > 50)
            throw new ValidationException("Mã nhân viên là bắt buộc và không được vượt quá 50 ký tự.");
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100)
            throw new ValidationException("Họ và tên là bắt buộc và không được vượt quá 100 ký tự.");
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("Email là bắt buộc.");
        if (phoneNumber?.Length > 20)
            throw new ValidationException("Số điện thoại không được vượt quá 20 ký tự.");
        if (!UserRole.AllRoles.Contains(request.Role))
            throw new ValidationException("Vai trò không hợp lệ.");
        if (string.IsNullOrEmpty(request.TemporaryPassword))
            throw new ValidationException("Mật khẩu tạm thời là bắt buộc.");

        if (await dbContext.Users.AnyAsync(user => user.EmployeeCode == employeeCode, cancellationToken))
            throw new ConflictException("Mã nhân viên đã tồn tại.");
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("Email đã tồn tại.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            EmployeeCode = employeeCode,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            EmploymentStatus = EmploymentStatus.Active
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var createResult = await userManager.CreateAsync(user, request.TemporaryPassword);
            EnsureSucceeded(createResult);

            var roleResult = await userManager.AddToRoleAsync(user, request.Role);
            EnsureSucceeded(roleResult);

            AddAudit(user.Id, "Staff.Create", $"Created staff account {employeeCode} with role {request.Role}.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Mã nhân viên hoặc email đã tồn tại.");
        }

        return new StaffResponse(
            user.Id,
            user.EmployeeCode,
            user.FullName,
            user.Email!,
            user.PhoneNumber,
            request.Role,
            user.EmploymentStatus);
    }

    public async Task<StaffResponse> UpdateStaffAsync(
        string id,
        UpdateStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        var fullName = request.FullName?.Trim();
        var email = request.Email?.Trim();
        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100)
            throw new ValidationException("Họ và tên là bắt buộc và không được vượt quá 100 ký tự.");
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("Email là bắt buộc.");
        if (phoneNumber?.Length > 20)
            throw new ValidationException("Số điện thoại không được vượt quá 20 ký tự.");

        var user = await userManager.FindByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nhân viên.");
        var emailOwner = await userManager.FindByEmailAsync(email);
        if (emailOwner is not null && emailOwner.Id != id)
            throw new ConflictException("Email đã tồn tại.");

        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        user.FullName = fullName;
        user.Email = email;
        user.UserName = email;
        user.EmailConfirmed = true;
        user.PhoneNumber = phoneNumber;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            EnsureSucceeded(await userManager.UpdateAsync(user));
            if (emailChanged)
                await authenticationService.RevokeAllSessionsAsync(
                    id, null, "Email nhân viên đã thay đổi.", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Email đã tồn tại.");
        }

        return await GetStaffByIdAsync(id, cancellationToken);
    }

    public async Task ChangeRoleAsync(
        string id,
        ChangeStaffRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!UserRole.AllRoles.Contains(request.Role))
            throw new ValidationException("Vai trò không hợp lệ.");
        if (id == currentUser.UserId)
            throw new ForbiddenAccessException("Quản trị viên không được phép tự thay đổi vai trò của chính mình.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var user = await userManager.FindByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nhân viên.");
        if (user.EmploymentStatus != EmploymentStatus.Inactive)
            throw new ConflictException("Vui lòng ngừng hoạt động tài khoản nhân viên trước khi thay đổi vai trò.");

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !UserRole.AllRoles.Contains(roles[0]))
            throw new ConflictException("Tài khoản nhân viên phải có đúng một vai trò hợp lệ.");

        var currentRole = roles[0];
        if (currentRole == request.Role)
            return;
        if (currentRole == UserRole.Teacher && await dbContext.Classes.AnyAsync(
                item => item.MainTeacherUserId == id && item.Status == ClassStatus.Active,
                cancellationToken))
            throw new ConflictException("Giảng viên vẫn đang phụ trách lớp học đang hoạt động.");

        EnsureSucceeded(await userManager.RemoveFromRoleAsync(user, currentRole));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, request.Role));

        AddAudit(user.Id, "Staff.RoleChanged", $"Changed staff role from {currentRole} to {request.Role}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await authenticationService.RevokeAllSessionsAsync(
            id, null, "Vai trò nhân viên đã thay đổi.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(
        string id,
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.NewPassword))
            throw new ValidationException("Mật khẩu mới là bắt buộc.");

        var user = await userManager.FindByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nhân viên.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        EnsureSucceeded(await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword));
        await authenticationService.RevokeAllSessionsAsync(
            id, null, "Đặt lại mật khẩu nhân viên.", cancellationToken);

        AddAudit(user.Id, "Staff.PasswordReset", $"Reset password for staff account {user.EmployeeCode}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ActivateAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var user = await userManager.FindByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nhân viên.");
        if (user.EmploymentStatus == EmploymentStatus.Active)
            return;

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !UserRole.AllRoles.Contains(roles[0]))
            throw new ConflictException("Tài khoản nhân viên phải có đúng một vai trò hợp lệ.");

        user.EmploymentStatus = EmploymentStatus.Active;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        AddAudit(user.Id, "Staff.Activated", $"Activated staff account {user.EmployeeCode}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeactivateAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (id == currentUser.UserId)
            throw new ForbiddenAccessException("Quản trị viên không được phép tự ngừng hoạt động tài khoản của chính mình.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var user = await userManager.FindByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nhân viên.");
        if (user.EmploymentStatus == EmploymentStatus.Inactive)
            return;

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !UserRole.AllRoles.Contains(roles[0]))
            throw new ConflictException("Tài khoản nhân viên phải có đúng một vai trò hợp lệ.");

        var role = roles[0];
        if (role == UserRole.Admin && !await HasAnotherActiveAdminAsync(id, cancellationToken))
            throw new ConflictException("Hệ thống phải duy trì ít nhất một Quản trị viên đang hoạt động.");
        if (role == UserRole.Teacher && await dbContext.Classes.AnyAsync(
                item => item.MainTeacherUserId == id && item.Status == ClassStatus.Active,
                cancellationToken))
            throw new ConflictException("Giảng viên vẫn đang phụ trách lớp học đang hoạt động.");

        user.EmploymentStatus = EmploymentStatus.Inactive;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        await authenticationService.RevokeAllSessionsAsync(
            id, null, "Tài khoản nhân viên đã bị ngừng hoạt động.", cancellationToken);
        AddAudit(user.Id, "Staff.Deactivated", $"Deactivated staff account {user.EmployeeCode}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<bool> HasAnotherActiveAdminAsync(string excludedUserId, CancellationToken cancellationToken)
    {
        return (from user in dbContext.Users
                join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                where user.Id != excludedUserId &&
                      user.EmploymentStatus == EmploymentStatus.Active &&
                      role.Name == UserRole.Admin
                select user.Id).AnyAsync(cancellationToken);
    }

    private void AddAudit(string userId, string action, string description)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            EntityType = "Staff",
            EntityId = userId,
            Description = description,
            OccurredAt = DateTime.UtcNow
        });
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            throw new ValidationException("Trang phải từ 1 trở lên và kích thước trang phải từ 1 đến 100.");
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
            return;

        if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            throw new ConflictException("Email đã tồn tại.");
        if (result.Errors.Any(error => error.Code == "ConcurrencyFailure"))
            throw new ConflictException("Tài khoản nhân viên đã bị thay đổi bởi một yêu cầu khác.");

        throw new ValidationException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }
}
