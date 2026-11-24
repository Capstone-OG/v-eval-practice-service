using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Schedules.DTOs;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure.Persistence;

namespace V_Eval_Practice_Service.Infrastructure.Services;

public class ScheduleService : IScheduleService
{
    private readonly PracticeDbContext _context;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        PracticeDbContext context,
        IContentGrpcClient contentGrpcClient,
        ILogger<ScheduleService> logger)
    {
        _context = context;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    // =========================================================================
    // 1. ACADEMIC TERMS (HỌC KỲ / ĐỢT TUYỂN SINH)
    // =========================================================================
    public async Task<Result<IReadOnlyList<AcademicTermDto>>> GetAcademicTermsAsync(
        Guid? campusId = null,
        string? academicYear = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var query = _context.AcademicTerms
            .AsNoTracking()
            .Include(t => t.Classes)
            .Include(t => t.Registrations)
            .AsQueryable();

        if (campusId.HasValue && campusId.Value != Guid.Empty)
        {
            query = query.Where(t => t.CampusId == campusId.Value);
        }

        if (!string.IsNullOrWhiteSpace(academicYear))
        {
            query = query.Where(t => t.AcademicYear == academicYear.Trim());
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(t => t.Status == normalizedStatus);
        }

        var terms = await query
            .OrderByDescending(t => t.AcademicYear)
            .ThenBy(t => t.TermOrder)
            .ToListAsync(ct);

        var dtos = terms.Select(MapToAcademicTermDto).ToList();
        return Result.Success<IReadOnlyList<AcademicTermDto>>(dtos);
    }

    public async Task<Result<AcademicTermDto>> CreateAcademicTermAsync(
        CreateAcademicTermRequestDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<AcademicTermDto>(
                Error.Validation("Schedule.InvalidTerm", "Mã học kỳ (Code) và tên học kỳ (Name) không được để trống."));
        }

        if (request.EndDate <= request.StartDate)
        {
            return Result.Failure<AcademicTermDto>(
                Error.Validation("Schedule.InvalidTermDates", "Ngày kết thúc học kỳ phải lớn hơn ngày bắt đầu."));
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.AcademicTerms.AnyAsync(t => t.Code == normalizedCode, ct);
        if (exists)
        {
            return Result.Failure<AcademicTermDto>(
                Error.Conflict("Schedule.DuplicateTermCode", $"Mã học kỳ '{normalizedCode}' đã tồn tại trên hệ thống."));
        }

        var term = new AcademicTerm
        {
            TermId = Guid.NewGuid(),
            CampusId = request.CampusId,
            Code = normalizedCode,
            Name = request.Name.Trim(),
            AcademicYear = string.IsNullOrWhiteSpace(request.AcademicYear)
                ? $"{request.StartDate.Year}-{request.StartDate.Year + 1}"
                : request.AcademicYear.Trim(),
            TermOrder = request.TermOrder <= 0 ? 1 : request.TermOrder,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "ACTIVE" : request.Status.Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _context.AcademicTerms.AddAsync(term, ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success(MapToAcademicTermDto(term));
    }

    public async Task<Result<AcademicTermDto>> UpdateAcademicTermStatusAsync(
        Guid termId,
        string status,
        CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms
            .Include(t => t.Classes)
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.TermId == termId, ct);

        if (term == null)
        {
            return Result.Failure<AcademicTermDto>(
                Error.NotFound("Schedule.TermNotFound", $"Không tìm thấy học kỳ với ID '{termId}'."));
        }

        term.Status = status.Trim().ToUpperInvariant();
        await _context.SaveChangesAsync(ct);

        return Result.Success(MapToAcademicTermDto(term));
    }

    // =========================================================================
    // 2. COURSES (KHÓA HỌC / CHƯƠNG TRÌNH ĐÀO TẠO)
    // =========================================================================
    public async Task<Result<IReadOnlyList<CourseSummaryDto>>> GetCoursesAsync(
        Guid? campusId = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = _context.Courses.AsNoTracking().AsQueryable();

        if (campusId.HasValue && campusId.Value != Guid.Empty)
        {
            query = query.Where(c => c.CampusId == campusId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var courses = await query.OrderBy(c => c.Code).ToListAsync(ct);
        var courseIds = courses.Select(c => c.CourseId).ToList();

        var classCounts = await _context.Classes
            .AsNoTracking()
            .Where(cl => cl.CourseId.HasValue && courseIds.Contains(cl.CourseId.Value))
            .GroupBy(cl => cl.CourseId!.Value)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct);

        var dtos = courses.Select(c => new CourseSummaryDto(
            CourseId: c.CourseId,
            CampusId: c.CampusId,
            Code: c.Code,
            Name: c.Name,
            PlanType: c.PlanType,
            DurationWeeks: c.DurationWeeks,
            TuitionFee: c.TuitionFee,
            RetakePolicy: c.RetakePolicy,
            RetakeFee: c.RetakeFee,
            MaxClassSize: c.MaxClassSize,
            IsActive: c.IsActive,
            TotalClassesCount: classCounts.GetValueOrDefault(c.CourseId, 0),
            CreatedAt: c.CreatedAt
        )).ToList();

        return Result.Success<IReadOnlyList<CourseSummaryDto>>(dtos);
    }

    public async Task<Result<CourseSummaryDto>> CreateCourseAsync(
        CreateCourseRequestDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CourseSummaryDto>(
                Error.Validation("Schedule.InvalidCourse", "Mã khóa học (Code) và tên khóa học (Name) không được để trống."));
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.Courses.AnyAsync(c => c.Code == normalizedCode, ct);
        if (exists)
        {
            return Result.Failure<CourseSummaryDto>(
                Error.Conflict("Schedule.DuplicateCourseCode", $"Mã khóa học '{normalizedCode}' đã tồn tại."));
        }

        var course = new Course
        {
            CourseId = Guid.NewGuid(),
            CampusId = request.CampusId,
            Code = normalizedCode,
            Name = request.Name.Trim(),
            PlanType = string.IsNullOrWhiteSpace(request.PlanType) ? "STANDARD" : request.PlanType.Trim().ToUpperInvariant(),
            DurationWeeks = request.DurationWeeks <= 0 ? 12 : request.DurationWeeks,
            TuitionFee = request.TuitionFee < 0 ? 0 : request.TuitionFee,
            RetakePolicy = string.IsNullOrWhiteSpace(request.RetakePolicy) ? "FREE_1X" : request.RetakePolicy.Trim().ToUpperInvariant(),
            RetakeFee = request.RetakeFee < 0 ? 0 : request.RetakeFee,
            MaxClassSize = request.MaxClassSize <= 0 ? 20 : request.MaxClassSize,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Courses.AddAsync(course, ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success(new CourseSummaryDto(
            CourseId: course.CourseId,
            CampusId: course.CampusId,
            Code: course.Code,
            Name: course.Name,
            PlanType: course.PlanType,
            DurationWeeks: course.DurationWeeks,
            TuitionFee: course.TuitionFee,
            RetakePolicy: course.RetakePolicy,
            RetakeFee: course.RetakeFee,
            MaxClassSize: course.MaxClassSize,
            IsActive: course.IsActive,
            TotalClassesCount: 0,
            CreatedAt: course.CreatedAt
        ));
    }

    public async Task<Result<CourseSummaryDto>> UpdateCourseAsync(
        Guid courseId,
        UpdateCourseRequestDto request,
        CancellationToken ct = default)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId, ct);
        if (course == null)
        {
            return Result.Failure<CourseSummaryDto>(
                Error.NotFound("Schedule.CourseNotFound", $"Không tìm thấy khóa học với ID '{courseId}'."));
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            if (!string.Equals(course.Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                var duplicate = await _context.Courses.AnyAsync(c => c.Code == normalizedCode && c.CourseId != courseId, ct);
                if (duplicate)
                {
                    return Result.Failure<CourseSummaryDto>(
                        Error.Conflict("Schedule.DuplicateCourseCode", $"Mã khóa học '{normalizedCode}' đã tồn tại."));
                }
                course.Code = normalizedCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            course.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PlanType))
        {
            course.PlanType = request.PlanType.Trim().ToUpperInvariant();
        }

        if (request.DurationWeeks.HasValue && request.DurationWeeks.Value > 0)
        {
            course.DurationWeeks = request.DurationWeeks.Value;
        }

        if (request.TuitionFee.HasValue && request.TuitionFee.Value >= 0)
        {
            course.TuitionFee = request.TuitionFee.Value;
        }

        if (request.MaxClassSize.HasValue && request.MaxClassSize.Value > 0)
        {
            course.MaxClassSize = Math.Min(20, request.MaxClassSize.Value);
        }

        if (request.IsActive.HasValue)
        {
            course.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync(ct);

        var classCount = await _context.Classes.CountAsync(cl => cl.CourseId == courseId, ct);

        return Result.Success(new CourseSummaryDto(
            CourseId: course.CourseId,
            CampusId: course.CampusId,
            Code: course.Code,
            Name: course.Name,
            PlanType: course.PlanType,
            DurationWeeks: course.DurationWeeks,
            TuitionFee: course.TuitionFee,
            RetakePolicy: course.RetakePolicy,
            RetakeFee: course.RetakeFee,
            MaxClassSize: course.MaxClassSize,
            IsActive: course.IsActive,
            TotalClassesCount: classCount,
            CreatedAt: course.CreatedAt
        ));
    }

    public async Task<Result<string>> DeleteCourseAsync(
        Guid courseId,
        CancellationToken ct = default)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId, ct);
        if (course == null)
        {
            return Result.Failure<string>(
                Error.NotFound("Schedule.CourseNotFound", $"Không tìm thấy khóa học với ID '{courseId}'."));
        }

        var hasClasses = await _context.Classes.AnyAsync(cl => cl.CourseId == courseId, ct);
        var hasRegistrations = await _context.CourseRegistrations.AnyAsync(r => r.CourseId == courseId, ct);

        if (hasClasses || hasRegistrations)
        {
            course.IsActive = false;
            await _context.SaveChangesAsync(ct);
            return Result.Success($"Khóa học '{course.Name}' đã có lớp/học viên đăng ký nên đã được chuyển sang trạng thái Ngừng mở (ẩn khỏi Cổng Học phí).");
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync(ct);
        return Result.Success($"Đã xóa khóa học '{course.Name}' thành công.");
    }

    // =========================================================================
    // 3. CLASSES & WEEKLY SCHEDULES (LỚP HỌC THEO KỲ/KHÓA & KHUNG LỊCH TUẦN)
    // =========================================================================
    public async Task<Result<IReadOnlyList<ClassOverviewDto>>> GetClassesAsync(
        Guid? campusId = null,
        Guid? termId = null,
        Guid? courseId = null,
        Guid? teacherId = null,
        CancellationToken ct = default)
    {
        var query = _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Term)
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .Include(c => c.Sessions)
            .AsQueryable();

        if (campusId.HasValue && campusId.Value != Guid.Empty)
        {
            query = query.Where(c => c.CampusId == campusId.Value);
        }

        if (termId.HasValue && termId.Value != Guid.Empty)
        {
            query = query.Where(c => c.TermId == termId.Value);
        }

        if (courseId.HasValue && courseId.Value != Guid.Empty)
        {
            query = query.Where(c => c.CourseId == courseId.Value);
        }

        if (teacherId.HasValue && teacherId.Value != Guid.Empty)
        {
            query = query.Where(c => c.TeacherId == teacherId.Value);
        }

        var classes = await query
            .OrderByDescending(c => c.StartDate)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        var dtos = classes.Select(MapToClassOverviewDto).ToList();
        return Result.Success<IReadOnlyList<ClassOverviewDto>>(dtos);
    }

    public async Task<Result<ClassOverviewDto>> CreateClassWithScheduleAsync(
        CreateClassWithScheduleRequestDto request,
        Guid? assignedBy = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<ClassOverviewDto>(
                Error.Validation("Schedule.InvalidClassName", "Tên lớp học không được để trống."));
        }

        AcademicTerm? term = null;
        if (request.TermId.HasValue && request.TermId.Value != Guid.Empty)
        {
            term = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.TermId == request.TermId.Value, ct);
            if (term == null)
            {
                return Result.Failure<ClassOverviewDto>(
                    Error.NotFound("Schedule.TermNotFound", $"Không tìm thấy học kỳ '{request.TermId}'."));
            }
        }

        Course? course = null;
        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == request.CourseId.Value, ct);
            if (course == null)
            {
                return Result.Failure<ClassOverviewDto>(
                    Error.NotFound("Schedule.CourseNotFound", $"Không tìm thấy khóa học '{request.CourseId}'."));
            }
        }

        var startDate = request.StartDate ?? term?.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var durationWeeks = course?.DurationWeeks ?? 12;
        var endDate = request.EndDate ?? term?.EndDate ?? startDate.AddDays(durationWeeks * 7);

        var newClass = new Class
        {
            ClassId = Guid.NewGuid(),
            CampusId = request.CampusId,
            CourseId = request.CourseId,
            TermId = request.TermId,
            Name = request.Name.Trim(),
            TeacherId = request.TeacherId,
            AssignedBy = request.TeacherId.HasValue ? assignedBy : null,
            AssignedAt = request.TeacherId.HasValue ? DateTime.UtcNow : null,
            ClassType = request.ClassType,
            DomainCode = request.FocusDomain,
            AcademicYear = term?.AcademicYear ?? $"{startDate.Year}-{startDate.Year + 1}",
            MaxCapacity = request.MaxCapacity <= 0 ? (course?.MaxClassSize ?? 20) : request.MaxCapacity,
            StartDate = startDate,
            EndDate = endDate,
            Status = "ACTIVE"
        };

        await _context.Classes.AddAsync(newClass, ct);

        if (request.WeeklySchedules != null && request.WeeklySchedules.Count > 0)
        {
            foreach (var slot in request.WeeklySchedules)
            {
                if (slot.DayOfWeek < 0 || slot.DayOfWeek > 8 || slot.EndTime <= slot.StartTime)
                {
                    return Result.Failure<ClassOverviewDto>(
                        Error.Validation("Schedule.InvalidSlot", $"Khung giờ Thứ ({slot.DayOfWeek}) Ca {slot.SlotNo} không hợp lệ."));
                }
            }

            var conflictResult = await CheckWeeklySchedulesConflictAsync(
                request.CampusId,
                null,
                request.TeacherId,
                request.WeeklySchedules,
                ct);

            if (conflictResult != null && !conflictResult.IsSuccess)
            {
                return Result.Failure<ClassOverviewDto>(conflictResult.Error);
            }

            foreach (var slot in request.WeeklySchedules)
            {
                var schedule = new ClassSchedule
                {
                    ScheduleId = Guid.NewGuid(),
                    ClassId = newClass.ClassId,
                    DayOfWeek = slot.DayOfWeek,
                    SlotNumber = slot.SlotNo <= 0 ? 1 : slot.SlotNo,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Room = string.IsNullOrWhiteSpace(slot.Room) ? string.Empty : slot.Room.Trim(),
                    IsActive = true
                };
                newClass.Schedules.Add(schedule);
                await _context.ClassSchedules.AddAsync(schedule, ct);
            }
        }

        await _context.SaveChangesAsync(ct);

        if (request.AutoGenerateSessions && newClass.Schedules.Count > 0)
        {
            await GenerateClassSessionsAsync(
                newClass.ClassId,
                new GenerateClassSessionsRequestDto(startDate, endDate, OverwriteScheduledSessions: false),
                ct);
        }

        var loaded = await _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Term)
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .Include(c => c.Sessions)
            .FirstAsync(c => c.ClassId == newClass.ClassId, ct);

        return Result.Success(MapToClassOverviewDto(loaded));
    }

    private async Task<Result?> CheckWeeklySchedulesConflictAsync(
        Guid campusId,
        Guid? currentClassId,
        Guid? teacherId,
        IReadOnlyList<ClassScheduleSlotInputDto> newSlots,
        CancellationToken ct)
    {
        if (newSlots == null || newSlots.Count == 0)
        {
            return null;
        }

        // 1. Kiểm tra xung đột nội bộ giữa các slot trong chính danh sách yêu cầu
        for (int i = 0; i < newSlots.Count; i++)
        {
            var s1 = newSlots[i];
            var d1 = NormalizeDayOfWeek(s1.DayOfWeek);
            var room1 = (s1.Room ?? string.Empty).Trim();

            for (int j = i + 1; j < newSlots.Count; j++)
            {
                var s2 = newSlots[j];
                var d2 = NormalizeDayOfWeek(s2.DayOfWeek);
                var room2 = (s2.Room ?? string.Empty).Trim();

                if (d1 == d2 && s1.StartTime < s2.EndTime && s2.StartTime < s1.EndTime)
                {
                    if (!string.IsNullOrEmpty(room1) && !string.IsNullOrEmpty(room2) &&
                        string.Equals(room1, room2, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result.Failure(Error.Conflict(
                            "Schedule.RoomConflict",
                            $"Trùng phòng trong danh sách ca học: Phòng '{room1}' bị xếp 2 lần vào {GetVietnameseDayOfWeekLabel(d1)} ({s1.StartTime:HH\\:mm}-{s1.EndTime:HH\\:mm} và {s2.StartTime:HH\\:mm}-{s2.EndTime:HH\\:mm})."));
                    }

                    return Result.Failure(Error.Conflict(
                        "Schedule.TimeConflict",
                        $"Trùng khung giờ trong danh sách ca học vào {GetVietnameseDayOfWeekLabel(d1)}: Ca {s1.SlotNo} ({s1.StartTime:HH\\:mm}-{s1.EndTime:HH\\:mm}) chồng lấn với Ca {s2.SlotNo} ({s2.StartTime:HH\\:mm}-{s2.EndTime:HH\\:mm})."));
                }
            }
        }

        // 2. Tải tất cả lớp học đang hoạt động tại cùng cơ sở kèm lịch tuần đang active
        var otherClassesQuery = _context.Classes
            .AsNoTracking()
            .Include(c => c.Schedules)
            .Where(c => c.CampusId == campusId && c.Status != "CANCELLED" && c.Status != "DELETED");

        if (currentClassId.HasValue && currentClassId.Value != Guid.Empty)
        {
            otherClassesQuery = otherClassesQuery.Where(c => c.ClassId != currentClassId.Value);
        }

        var otherClasses = await otherClassesQuery.ToListAsync(ct);

        foreach (var newSlot in newSlots)
        {
            var newDow = NormalizeDayOfWeek(newSlot.DayOfWeek);
            var newRoom = (newSlot.Room ?? string.Empty).Trim();

            foreach (var otherClass in otherClasses)
            {
                var activeSchedules = (otherClass.Schedules ?? Enumerable.Empty<ClassSchedule>())
                    .Where(s => s.IsActive && NormalizeDayOfWeek(s.DayOfWeek) == newDow);

                foreach (var existingSchedule in activeSchedules)
                {
                    bool isTimeOverlap = newSlot.StartTime < existingSchedule.EndTime && existingSchedule.StartTime < newSlot.EndTime;
                    if (!isTimeOverlap)
                    {
                        continue;
                    }

                    // A. Kiểm tra xung đột phòng học (Room conflict) cùng cơ sở
                    if (!string.IsNullOrEmpty(newRoom) && !string.IsNullOrEmpty(existingSchedule.Room) &&
                        string.Equals(newRoom, existingSchedule.Room.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return Result.Failure(Error.Conflict(
                            "Schedule.RoomConflict",
                            $"Xung đột phòng học: Phòng '{newRoom}' đã được xếp cho lớp '{otherClass.Name}' vào {GetVietnameseDayOfWeekLabel(newDow)} ({existingSchedule.StartTime:HH\\:mm} - {existingSchedule.EndTime:HH\\:mm}). Vui lòng chọn phòng học hoặc khung giờ khác."));
                    }

                    // B. Kiểm tra xung đột lịch giảng viên (Teacher conflict)
                    if (teacherId.HasValue && teacherId.Value != Guid.Empty &&
                        otherClass.TeacherId.HasValue && otherClass.TeacherId.Value == teacherId.Value)
                    {
                        return Result.Failure(Error.Conflict(
                            "Schedule.TeacherConflict",
                            $"Xung đột lịch giảng dạy: Giảng viên đã có lịch dạy lớp '{otherClass.Name}' vào {GetVietnameseDayOfWeekLabel(newDow)} ({existingSchedule.StartTime:HH\\:mm} - {existingSchedule.EndTime:HH\\:mm}). Vui lòng chọn giảng viên hoặc khung giờ khác."));
                    }
                }
            }
        }

        return null;
    }

    public async Task<Result<IReadOnlyList<ClassScheduleSlotDto>>> SaveClassSchedulesAsync(
        Guid classId,
        SaveClassSchedulesRequestDto request,
        CancellationToken ct = default)
    {
        var cls = await _context.Classes
            .Include(c => c.Schedules)
            .FirstOrDefaultAsync(c => c.ClassId == classId, ct);

        if (cls == null)
        {
            return Result.Failure<IReadOnlyList<ClassScheduleSlotDto>>(
                Error.NotFound("Schedule.ClassNotFound", $"Không tìm thấy lớp học '{classId}'."));
        }

        if (request.Slots == null || request.Slots.Count == 0)
        {
            return Result.Failure<IReadOnlyList<ClassScheduleSlotDto>>(
                Error.Validation("Schedule.EmptySlots", "Danh sách ca học trong tuần không được để trống."));
        }

        foreach (var slot in request.Slots)
        {
            if (slot.DayOfWeek < 0 || slot.DayOfWeek > 8 || slot.EndTime <= slot.StartTime)
            {
                return Result.Failure<IReadOnlyList<ClassScheduleSlotDto>>(
                    Error.Validation("Schedule.InvalidSlot", $"Ca học Thứ ({slot.DayOfWeek}) Ca {slot.SlotNo} có thời gian không hợp lệ."));
            }
        }

        var conflictResult = await CheckWeeklySchedulesConflictAsync(
            cls.CampusId,
            cls.ClassId,
            cls.TeacherId,
            request.Slots,
            ct);

        if (conflictResult != null && !conflictResult.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<ClassScheduleSlotDto>>(conflictResult.Error);
        }

        // Deactivate old schedules or update matching (DayOfWeek, SlotNumber)
        foreach (var existing in cls.Schedules)
        {
            existing.IsActive = false;
        }

        var activeSchedules = new List<ClassSchedule>();
        foreach (var slot in request.Slots)
        {
            var match = cls.Schedules.FirstOrDefault(s => s.DayOfWeek == slot.DayOfWeek && s.SlotNumber == slot.SlotNo);
            if (match != null)
            {
                match.StartTime = slot.StartTime;
                match.EndTime = slot.EndTime;
                match.Room = string.IsNullOrWhiteSpace(slot.Room) ? string.Empty : slot.Room.Trim();
                match.IsActive = true;
                activeSchedules.Add(match);
            }
            else
            {
                var created = new ClassSchedule
                {
                    ScheduleId = Guid.NewGuid(),
                    ClassId = classId,
                    DayOfWeek = slot.DayOfWeek,
                    SlotNumber = slot.SlotNo <= 0 ? 1 : slot.SlotNo,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Room = string.IsNullOrWhiteSpace(slot.Room) ? string.Empty : slot.Room.Trim(),
                    IsActive = true
                };
                await _context.ClassSchedules.AddAsync(created, ct);
                activeSchedules.Add(created);
            }
        }

        await _context.SaveChangesAsync(ct);

        if (request.RegenerateFutureSessions)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var fromDate = cls.StartDate.HasValue && cls.StartDate.Value > today ? cls.StartDate.Value : today;
            var toDate = cls.EndDate ?? fromDate.AddDays(84);
            await GenerateClassSessionsAsync(
                classId,
                new GenerateClassSessionsRequestDto(fromDate, toDate, OverwriteScheduledSessions: true),
                ct);
        }

        var slotDtos = activeSchedules
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.SlotNumber)
            .Select(MapToScheduleSlotDto)
            .ToList();

        return Result.Success<IReadOnlyList<ClassScheduleSlotDto>>(slotDtos);
    }

    // =========================================================================
    // 4. CLASS SESSIONS (AUTO-GENERATE, CONFLICT CHECK, SUBSTITUTE, RESCHEDULE)
    // =========================================================================
    public async Task<Result<GenerateClassSessionsResponseDto>> GenerateClassSessionsAsync(
        Guid classId,
        GenerateClassSessionsRequestDto request,
        CancellationToken ct = default)
    {
        var cls = await _context.Classes
            .Include(c => c.Course)
            .Include(c => c.Term)
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .Include(c => c.Sessions)
            .FirstOrDefaultAsync(c => c.ClassId == classId, ct);

        if (cls == null)
        {
            return Result.Failure<GenerateClassSessionsResponseDto>(
                Error.NotFound("Schedule.ClassNotFound", $"Không tìm thấy lớp học '{classId}'."));
        }

        var activeSlots = cls.Schedules.Where(s => s.IsActive).OrderBy(s => s.DayOfWeek).ThenBy(s => s.SlotNumber).ToList();
        if (activeSlots.Count == 0)
        {
            return Result.Failure<GenerateClassSessionsResponseDto>(
                Error.Validation("Schedule.NoWeeklySchedules", "Lớp học chưa được cấu hình khung lịch tuần (ClassSchedules)."));
        }

        var fromDate = request.FromDate
            ?? cls.StartDate
            ?? cls.Term?.StartDate
            ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var durationWeeks = cls.Course?.DurationWeeks ?? 12;
        var toDate = request.ToDate
            ?? cls.EndDate
            ?? cls.Term?.EndDate
            ?? fromDate.AddDays(durationWeeks * 7);

        if (toDate < fromDate)
        {
            return Result.Failure<GenerateClassSessionsResponseDto>(
                Error.Validation("Schedule.InvalidDateRange", "Ngày kết thúc sinh lịch phải lớn hơn hoặc bằng ngày bắt đầu."));
        }

        if (request.OverwriteScheduledSessions)
        {
            var removableSessions = cls.Sessions
                .Where(s => s.SessionDate >= fromDate && s.SessionDate <= toDate && s.Status == "SCHEDULED")
                .ToList();

            if (removableSessions.Count > 0)
            {
                _context.ClassSessions.RemoveRange(removableSessions);
                await _context.SaveChangesAsync(ct);
                foreach (var rem in removableSessions)
                {
                    cls.Sessions.Remove(rem);
                }
            }
        }

        // Load skill tree for curriculum topic enrichment
        IReadOnlyList<SkillTreeNodeDto> skills = Array.Empty<SkillTreeNodeDto>();
        try
        {
            skills = await _contentGrpcClient.GetSkillsTreeAsync(ct);
            if (!string.IsNullOrWhiteSpace(cls.DomainCode) && skills.Count > 0)
            {
                var domainFiltered = skills
                    .Where(s => string.Equals(s.DomainCode, cls.DomainCode, StringComparison.OrdinalIgnoreCase)
                             || s.DomainName.Contains(cls.DomainCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (domainFiltered.Count > 0)
                {
                    skills = domainFiltered;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy cây kỹ năng từ Content Service khi sinh lịch lớp {ClassId}. Sẽ dùng tiêu đề mặc định.", classId);
        }

        // Load other active sessions in the same date range at the campus to detect Room & Teacher conflicts
        var otherSessionsInRange = await _context.ClassSessions
            .AsNoTracking()
            .Include(s => s.Class)
            .Where(s => s.ClassId != classId
                     && s.SessionDate >= fromDate
                     && s.SessionDate <= toDate
                     && s.Status != "CANCELLED"
                     && s.Status != "RESCHEDULED")
            .ToListAsync(ct);

        var warnings = new List<string>();
        var generatedCount = 0;
        var skippedConflictCount = 0;

        int currentSessionNo = cls.Sessions.Count > 0 ? cls.Sessions.Max(s => s.SessionNo) : 0;

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            int dow = (int)date.DayOfWeek; // 0 = Sunday .. 6 = Saturday
            var matchingSlots = activeSlots
                .Where(s => NormalizeDayOfWeek(s.DayOfWeek) == dow)
                .OrderBy(s => s.SlotNumber)
                .ToList();

            foreach (var slot in matchingSlots)
            {
                // Skip if session already exists at same date and start time for this class
                bool alreadyExists = cls.Sessions.Any(s =>
                    s.SessionDate == date &&
                    s.StartTime == slot.StartTime &&
                    s.Status != "CANCELLED");

                if (alreadyExists)
                {
                    continue;
                }

                // Check room conflict across campus
                var roomConflict = otherSessionsInRange.FirstOrDefault(s =>
                    s.SessionDate == date &&
                    string.Equals(s.Room, slot.Room, StringComparison.OrdinalIgnoreCase) &&
                    s.StartTime < slot.EndTime &&
                    slot.StartTime < s.EndTime);

                if (roomConflict != null)
                {
                    warnings.Add($"[Cảnh báo trùng phòng] Ngày {date:dd/MM/yyyy} ({slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}): Phòng '{slot.Room}' đang trùng với lớp '{roomConflict.Class?.Name ?? roomConflict.ClassId.ToString()}'.");
                }

                // Check teacher conflict
                if (cls.TeacherId.HasValue)
                {
                    var teacherConflict = otherSessionsInRange.FirstOrDefault(s =>
                        s.SessionDate == date &&
                        (s.TeacherId ?? s.Class?.TeacherId) == cls.TeacherId.Value &&
                        s.StartTime < slot.EndTime &&
                        slot.StartTime < s.EndTime);

                    if (teacherConflict != null)
                    {
                        warnings.Add($"[Cảnh báo trùng lịch GV] Ngày {date:dd/MM/yyyy} ({slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}): Giảng viên đang có lịch dạy tại lớp '{teacherConflict.Class?.Name ?? teacherConflict.ClassId.ToString()}'.");
                    }
                }

                currentSessionNo++;
                SkillTreeNodeDto? assignedSkill = skills.Count > 0
                    ? skills[(currentSessionNo - 1) % skills.Count]
                    : null;

                var topicTitle = assignedSkill != null
                    ? $"Buổi {currentSessionNo}: Chuyên đề {assignedSkill.Name}"
                    : $"Buổi {currentSessionNo}: Chuyên đề {cls.Name}";

                var newSession = new ClassSession
                {
                    SessionId = Guid.NewGuid(),
                    ClassId = classId,
                    ScheduleId = slot.ScheduleId,
                    SessionNo = currentSessionNo,
                    SessionDate = date,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Room = slot.Room ?? string.Empty,
                    TeacherId = cls.TeacherId,
                    SkillId = assignedSkill?.SkillId,
                    TopicTitle = topicTitle,
                    Status = "SCHEDULED"
                };

                cls.Sessions.Add(newSession);
                await _context.ClassSessions.AddAsync(newSession, ct);
                generatedCount++;
            }
        }

        await _context.SaveChangesAsync(ct);

        var allSessions = await GetClassSessionsInternalAsync(cls, null, null, skills, ct);

        return Result.Success(new GenerateClassSessionsResponseDto(
            ClassId: cls.ClassId,
            ClassName: cls.Name,
            FromDate: fromDate,
            ToDate: toDate,
            GeneratedSessionsCount: generatedCount,
            SkippedConflictCount: skippedConflictCount,
            Warnings: warnings,
            Sessions: allSessions
        ));
    }

    public async Task<Result<IReadOnlyList<ClassSessionDetailDto>>> GetClassSessionsAsync(
        Guid classId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var cls = await _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Term)
            .Include(c => c.Enrollments)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Attendances)
            .FirstOrDefaultAsync(c => c.ClassId == classId, ct);

        if (cls == null)
        {
            return Result.Failure<IReadOnlyList<ClassSessionDetailDto>>(
                Error.NotFound("Schedule.ClassNotFound", $"Không tìm thấy lớp học '{classId}'."));
        }

        IReadOnlyList<SkillTreeNodeDto> skills = Array.Empty<SkillTreeNodeDto>();
        try
        {
            skills = await _contentGrpcClient.GetSkillsTreeAsync(ct);
        }
        catch
        {
            // Ignore gRPC fallback failure
        }

        var dtos = await GetClassSessionsInternalAsync(cls, fromDate, toDate, skills, ct);
        return Result.Success(dtos);
    }

    public async Task<Result<ClassSessionDetailDto>> UpdateClassSessionAsync(
        Guid sessionId,
        UpdateClassSessionRequestDto request,
        CancellationToken ct = default)
    {
        var session = await _context.ClassSessions
            .Include(s => s.Class)
                .ThenInclude(c => c!.Course)
            .Include(s => s.Class)
                .ThenInclude(c => c!.Term)
            .Include(s => s.Class)
                .ThenInclude(c => c!.Enrollments)
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

        if (session == null || session.Class == null)
        {
            return Result.Failure<ClassSessionDetailDto>(
                Error.NotFound("Schedule.SessionNotFound", $"Không tìm thấy buổi học '{sessionId}'."));
        }

        var targetDate = request.SessionDate ?? session.SessionDate;
        var targetStart = request.StartTime ?? session.StartTime;
        var targetEnd = request.EndTime ?? session.EndTime;
        var targetRoom = !string.IsNullOrWhiteSpace(request.Room) ? request.Room.Trim() : (session.Room ?? string.Empty);
        var targetTeacher = request.ActualTeacherId ?? session.TeacherId ?? session.Class.TeacherId;

        if (targetEnd <= targetStart)
        {
            return Result.Failure<ClassSessionDetailDto>(
                Error.Validation("Schedule.InvalidSessionTime", "Giờ kết thúc buổi học phải lớn hơn giờ bắt đầu."));
        }

        // Check room conflict if room/date/time changed
        var roomConflict = await _context.ClassSessions
            .AsNoTracking()
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s =>
                s.SessionId != sessionId &&
                s.SessionDate == targetDate &&
                s.Room == targetRoom &&
                s.Status != "CANCELLED" &&
                s.Status != "RESCHEDULED" &&
                s.StartTime < targetEnd &&
                targetStart < s.EndTime, ct);

        if (roomConflict != null)
        {
            return Result.Failure<ClassSessionDetailDto>(
                Error.Conflict("Schedule.RoomConflict",
                    $"Phòng '{targetRoom}' vào ngày {targetDate:dd/MM/yyyy} ({targetStart:HH\\:mm}-{targetEnd:HH\\:mm}) đã có lớp '{roomConflict.Class?.Name}' sử dụng."));
        }

        // Check substitute/actual teacher conflict
        if (targetTeacher.HasValue)
        {
            var teacherConflict = await _context.ClassSessions
                .AsNoTracking()
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s =>
                    s.SessionId != sessionId &&
                    s.SessionDate == targetDate &&
                    (s.TeacherId ?? s.Class!.TeacherId) == targetTeacher.Value &&
                    s.Status != "CANCELLED" &&
                    s.Status != "RESCHEDULED" &&
                    s.StartTime < targetEnd &&
                    targetStart < s.EndTime, ct);

            if (teacherConflict != null)
            {
                return Result.Failure<ClassSessionDetailDto>(
                    Error.Conflict("Schedule.TeacherConflict",
                        $"Giảng viên được phân công đã có lịch dạy lớp '{teacherConflict.Class?.Name}' vào khung giờ {targetStart:HH\\:mm}-{targetEnd:HH\\:mm} ngày {targetDate:dd/MM/yyyy}."));
            }
        }

        session.SessionDate = targetDate;
        session.StartTime = targetStart;
        session.EndTime = targetEnd;
        session.Room = targetRoom;
        session.UpdatedAt = DateTime.UtcNow;

        if (request.ActualTeacherId.HasValue)
        {
            session.TeacherId = request.ActualTeacherId.Value;
        }

        if (request.SkillId.HasValue)
        {
            session.SkillId = request.SkillId.Value;
        }

        if (request.TopicTitle != null)
        {
            session.TopicTitle = request.TopicTitle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            session.Status = request.Status.Trim().ToUpperInvariant();
        }

        if (request.Notes != null)
        {
            session.Notes = request.Notes.Trim();
        }

        await _context.SaveChangesAsync(ct);

        return Result.Success(MapToSessionDetailDto(session, session.Class, new Dictionary<Guid, string>()));
    }

    public async Task<Result<ClassSessionDetailDto>> RescheduleClassSessionAsync(
        Guid sessionId,
        RescheduleClassSessionRequestDto request,
        CancellationToken ct = default)
    {
        var originalSession = await _context.ClassSessions
            .Include(s => s.Class)
                .ThenInclude(c => c!.Course)
            .Include(s => s.Class)
                .ThenInclude(c => c!.Term)
            .Include(s => s.Class)
                .ThenInclude(c => c!.Enrollments)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

        if (originalSession == null || originalSession.Class == null)
        {
            return Result.Failure<ClassSessionDetailDto>(
                Error.NotFound("Schedule.SessionNotFound", $"Không tìm thấy buổi học '{sessionId}' cần dời lịch."));
        }

        if (request.NewEndTime <= request.NewStartTime)
        {
            return Result.Failure<ClassSessionDetailDto>(
                Error.Validation("Schedule.InvalidMakeupTime", "Khung giờ học bù không hợp lệ (EndTime phải lớn hơn StartTime)."));
        }

        var normalizedRoom = string.IsNullOrWhiteSpace(request.NewRoom)
            ? (originalSession.Room ?? string.Empty)
            : request.NewRoom.Trim();

        // Check room conflict for the makeup session
        var roomConflict = await _context.ClassSessions
            .AsNoTracking()
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s =>
                s.SessionId != sessionId &&
                s.SessionDate == request.NewSessionDate &&
                s.Room == normalizedRoom &&
                s.Status != "CANCELLED" &&
                s.Status != "RESCHEDULED" &&
                s.StartTime < request.NewEndTime &&
                request.NewStartTime < s.EndTime, ct);

        if (roomConflict != null)
        {
            return Result.Failure<ClassSessionDetailDto>(
                Error.Conflict("Schedule.MakeupRoomConflict",
                    $"Phòng '{normalizedRoom}' vào ngày học bù {request.NewSessionDate:dd/MM/yyyy} ({request.NewStartTime:HH\\:mm}-{request.NewEndTime:HH\\:mm}) đã có lớp '{roomConflict.Class?.Name}' học."));
        }

        // Mark original session as RESCHEDULED
        originalSession.Status = "RESCHEDULED";
        originalSession.Notes = $"[Đã dời lịch sang {request.NewSessionDate:dd/MM/yyyy} {request.NewStartTime:HH\\:mm}-{request.NewEndTime:HH\\:mm} tại {normalizedRoom}] Lý do: {request.Reason}";
        originalSession.UpdatedAt = DateTime.UtcNow;

        // Create makeup session linked to originalSession
        var makeupSession = new ClassSession
        {
            SessionId = Guid.NewGuid(),
            ClassId = originalSession.ClassId,
            ScheduleId = originalSession.ScheduleId,
            SessionNo = originalSession.SessionNo,
            SessionDate = request.NewSessionDate,
            StartTime = request.NewStartTime,
            EndTime = request.NewEndTime,
            Room = normalizedRoom,
            TeacherId = request.SubstituteTeacherId ?? originalSession.TeacherId ?? originalSession.Class.TeacherId,
            SkillId = originalSession.SkillId,
            TopicTitle = string.IsNullOrWhiteSpace(originalSession.TopicTitle)
                ? $"[Học bù Buổi {originalSession.SessionNo}] {originalSession.Class.Name}"
                : $"[Học bù] {originalSession.TopicTitle}",
            RescheduledFromId = originalSession.SessionId,
            Status = "SCHEDULED",
            Notes = $"Buổi học bù cho ngày {originalSession.SessionDate:dd/MM/yyyy}. Lý do: {request.Reason}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.ClassSessions.AddAsync(makeupSession, ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success(MapToSessionDetailDto(makeupSession, originalSession.Class, new Dictionary<Guid, string>()));
    }

    // =========================================================================
    // 5. SESSION ATTENDANCES (ĐIỂM DANH, ĐỒNG BỘ STREAK & CẢNH BÁO SỚM BR-04)
    // =========================================================================
    public async Task<Result<SessionAttendanceSummaryResponseDto>> GetSessionAttendancesAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await _context.ClassSessions
            .AsNoTracking()
            .Include(s => s.Class)
                .ThenInclude(c => c!.Enrollments)
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

        if (session == null || session.Class == null)
        {
            return Result.Failure<SessionAttendanceSummaryResponseDto>(
                Error.NotFound("Schedule.SessionNotFound", $"Không tìm thấy buổi học '{sessionId}'."));
        }

        var enrolledStudentIds = session.Class.Enrollments
            .Select(e => e.StudentId)
            .Distinct()
            .ToList();

        var existingMap = session.Attendances.ToDictionary(a => a.StudentId, a => a);

        var items = new List<SessionAttendanceItemDto>();
        foreach (var studentId in enrolledStudentIds)
        {
            if (existingMap.TryGetValue(studentId, out var att))
            {
                items.Add(new SessionAttendanceItemDto(
                    AttendanceId: att.AttendanceId,
                    SessionId: sessionId,
                    StudentId: studentId,
                    Status: att.Status,
                    CheckInAt: att.CheckInAt,
                    RecordedBy: att.RecordedBy,
                    Note: att.Note
                ));
            }
            else
            {
                items.Add(new SessionAttendanceItemDto(
                    AttendanceId: Guid.Empty,
                    SessionId: sessionId,
                    StudentId: studentId,
                    Status: "UNRECORDED",
                    CheckInAt: null,
                    RecordedBy: null,
                    Note: null
                ));
            }
        }

        // Include any attendance records for students not in the current enrollment list
        foreach (var att in session.Attendances.Where(a => !enrolledStudentIds.Contains(a.StudentId)))
        {
            items.Add(new SessionAttendanceItemDto(
                AttendanceId: att.AttendanceId,
                SessionId: sessionId,
                StudentId: att.StudentId,
                Status: att.Status,
                CheckInAt: att.CheckInAt,
                RecordedBy: att.RecordedBy,
                Note: att.Note
            ));
        }

        return Result.Success(new SessionAttendanceSummaryResponseDto(
            SessionId: session.SessionId,
            ClassId: session.ClassId,
            ClassName: session.Class.Name,
            SessionNo: session.SessionNo,
            SessionDate: session.SessionDate,
            SessionStatus: session.Status,
            TotalEnrolled: enrolledStudentIds.Count,
            PresentCount: items.Count(i => i.Status == "PRESENT"),
            AbsentCount: items.Count(i => i.Status == "ABSENT"),
            LateCount: items.Count(i => i.Status == "LATE"),
            ExcusedCount: items.Count(i => i.Status == "EXCUSED"),
            TriggeredAlertsCount: 0,
            Records: items
        ));
    }

    public async Task<Result<SessionAttendanceSummaryResponseDto>> SubmitSessionAttendancesAsync(
        Guid sessionId,
        SubmitSessionAttendanceRequestDto request,
        Guid? recordedBy = null,
        CancellationToken ct = default)
    {
        var session = await _context.ClassSessions
            .Include(s => s.Class)
                .ThenInclude(c => c!.Enrollments)
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

        if (session == null || session.Class == null)
        {
            return Result.Failure<SessionAttendanceSummaryResponseDto>(
                Error.NotFound("Schedule.SessionNotFound", $"Không tìm thấy buổi học '{sessionId}'."));
        }

        if (request.Attendances == null || request.Attendances.Count == 0)
        {
            return Result.Failure<SessionAttendanceSummaryResponseDto>(
                Error.Validation("Schedule.EmptyAttendances", "Danh sách điểm danh không được để trống."));
        }

        var now = DateTime.UtcNow;
        var sessionDurationMinutes = Math.Max(30, (int)(session.EndTime - session.StartTime).TotalMinutes);
        var activityDateUtc = DateTime.SpecifyKind(session.SessionDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var triggeredAlertsCount = 0;

        foreach (var item in request.Attendances)
        {
            var normalizedStatus = string.IsNullOrWhiteSpace(item.Status)
                ? "PRESENT"
                : item.Status.Trim().ToUpperInvariant();

            if (normalizedStatus != "PRESENT" && normalizedStatus != "ABSENT" &&
                normalizedStatus != "LATE" && normalizedStatus != "EXCUSED")
            {
                normalizedStatus = "PRESENT";
            }

            var existing = session.Attendances.FirstOrDefault(a => a.StudentId == item.StudentId);
            if (existing != null)
            {
                existing.Status = normalizedStatus;
                existing.CheckInAt = (normalizedStatus == "PRESENT" || normalizedStatus == "LATE")
                    ? (existing.CheckInAt ?? now)
                    : null;
                existing.RecordedBy = recordedBy ?? existing.RecordedBy;
                existing.Note = item.Note;
                existing.UpdatedAt = now;
            }
            else
            {
                var created = new SessionAttendance
                {
                    AttendanceId = Guid.NewGuid(),
                    SessionId = sessionId,
                    StudentId = item.StudentId,
                    Status = normalizedStatus,
                    CheckInAt = (normalizedStatus == "PRESENT" || normalizedStatus == "LATE") ? now : null,
                    RecordedBy = recordedBy,
                    Note = item.Note,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                session.Attendances.Add(created);
                await _context.SessionAttendances.AddAsync(created, ct);
            }

            // 1. Sync to StudentDailyActivities when student attends class (PRESENT or LATE)
            if (normalizedStatus == "PRESENT" || normalizedStatus == "LATE")
            {
                var dailyActivity = await _context.StudentDailyActivities
                    .FirstOrDefaultAsync(a => a.StudentId == item.StudentId && a.ActivityDate == activityDateUtc, ct);

                if (dailyActivity == null)
                {
                    dailyActivity = new StudentDailyActivity
                    {
                        Id = Guid.NewGuid(),
                        StudentId = item.StudentId,
                        ActivityDate = activityDateUtc,
                        StudyTimeMinutes = sessionDurationMinutes,
                        VideoMinutesWatched = sessionDurationMinutes,
                        IsStreakCounted = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _context.StudentDailyActivities.AddAsync(dailyActivity, ct);
                }
                else
                {
                    dailyActivity.StudyTimeMinutes = Math.Max(dailyActivity.StudyTimeMinutes, sessionDurationMinutes);
                    dailyActivity.IsStreakCounted = true;
                    dailyActivity.UpdatedAt = now;
                }
            }
            // 2. Check Early Warning BR-04 if student is ABSENT without excuse
            else if (normalizedStatus == "ABSENT")
            {
                var previousAbsencesCount = await _context.SessionAttendances
                    .AsNoTracking()
                    .Include(a => a.Session)
                    .CountAsync(a =>
                        a.StudentId == item.StudentId &&
                        a.SessionId != sessionId &&
                        a.Session != null &&
                        a.Session.ClassId == session.ClassId &&
                        a.Status == "ABSENT", ct);

                var totalClassAbsences = previousAbsencesCount + 1;
                if (totalClassAbsences >= 2)
                {
                    var alertType = totalClassAbsences >= 3 ? "RED" : "YELLOW";
                    var alertMessage = totalClassAbsences >= 3
                        ? $"[Cảnh báo Chuyên cần Cấp độ ĐỎ] Học sinh đã vắng không phép {totalClassAbsences} buổi tại lớp '{session.Class.Name}' (Buổi gần nhất: Buổi {session.SessionNo} ngày {session.SessionDate:dd/MM/yyyy}). Đề nghị Giáo viên và Phụ huynh phối hợp xếp lịch học bù ngay."
                        : $"[Cảnh báo Chuyên cần Cấp độ VÀNG] Học sinh đã vắng không phép {totalClassAbsences} buổi tại lớp '{session.Class.Name}' (Buổi {session.SessionNo} ngày {session.SessionDate:dd/MM/yyyy}).";

                    var alert = new SystemAlert
                    {
                        AlertId = Guid.NewGuid(),
                        StudentId = item.StudentId,
                        AlertType = alertType,
                        Message = alertMessage,
                        IsRead = false,
                        CreatedAt = now
                    };
                    await _context.SystemAlerts.AddAsync(alert, ct);
                    triggeredAlertsCount++;
                }
            }
        }

        if (request.MarkSessionCompleted && session.Status == "SCHEDULED")
        {
            session.Status = "COMPLETED";
            session.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(ct);

        var summaryResult = await GetSessionAttendancesAsync(sessionId, ct);
        if (summaryResult.IsFailure)
        {
            return summaryResult;
        }

        var val = summaryResult.Value;
        return Result.Success(val with { TriggeredAlertsCount = triggeredAlertsCount });
    }

    // =========================================================================
    // 6. STUDENT TIMETABLE & TEACHER SCHEDULE (THEO KỲ, THEO KHÓA, THEO TUẦN)
    // =========================================================================
    public async Task<Result<StudentTimetableResponseDto>> GetStudentTimetableAsync(
        Guid studentId,
        Guid? termId = null,
        Guid? courseId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var enrollments = await _context.ClassEnrollments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(e => e.Class)
                .ThenInclude(c => c!.Course)
            .Include(e => e.Class)
                .ThenInclude(c => c!.Term)
            .Include(e => e.Class)
                .ThenInclude(c => c!.Schedules)
            .Include(e => e.Class)
                .ThenInclude(c => c!.Sessions)
                    .ThenInclude(s => s.Attendances.Where(a => a.StudentId == studentId))
            .Where(e => e.StudentId == studentId && e.Class != null)
            .ToListAsync(ct);

        var allClasses = enrollments.Select(e => e.Class!).DistinctBy(c => c.ClassId).ToList();

        // Nạp các đơn đăng ký khóa học đã kích hoạt / hoàn thành của học sinh để liên kết chặt với lớp & TKB
        var studentRegistrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.Term)
            .Where(r => r.StudentId == studentId && (r.Status == "ACTIVE" || r.Status == "COMPLETED" || r.Status == "PAID"))
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync(ct);

        var primaryRegistration = studentRegistrations.FirstOrDefault();

        // Chỉ lấy các Đợt học thực sự gắn với lớp đang học hoặc khóa học đã đăng ký của học sinh
        var relevantTermIds = allClasses.Where(c => c.TermId.HasValue).Select(c => c.TermId!.Value)
            .Concat(studentRegistrations.Where(r => r.TermId.HasValue).Select(r => r.TermId!.Value))
            .Distinct()
            .ToList();

        var studentCampusIds = allClasses.Select(c => c.CampusId).Distinct().ToList();
        var availableTermsEntities = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => relevantTermIds.Count > 0
                ? relevantTermIds.Contains(t.TermId)
                : (studentCampusIds.Count == 0 || studentCampusIds.Contains(t.CampusId)))
            .OrderByDescending(t => t.AcademicYear)
            .ThenBy(t => t.TermOrder)
            .ToListAsync(ct);

        var availableTerms = availableTermsEntities.Select(MapToAcademicTermDto).ToList();

        // Filter classes by termId and courseId (có xét cả fallback từ đơn đăng ký đã mua của học sinh)
        var filteredClasses = allClasses.AsEnumerable();
        if (termId.HasValue && termId.Value != Guid.Empty)
        {
            filteredClasses = filteredClasses.Where(c => (c.TermId ?? primaryRegistration?.TermId) == termId.Value);
        }

        if (courseId.HasValue && courseId.Value != Guid.Empty)
        {
            filteredClasses = filteredClasses.Where(c => (c.CourseId ?? primaryRegistration?.CourseId) == courseId.Value);
        }

        var filteredClassList = filteredClasses.ToList();
        var filteredClassIds = filteredClassList.Select(c => c.ClassId).ToList();

        // Load student's MicroGroups in these classes for pedagogical context
        var studentMicroGroups = await _context.ClassGroupMembers
            .AsNoTracking()
            .Include(m => m.Group)
            .Where(m => m.StudentId == studentId && m.Group != null && filteredClassIds.Contains(m.Group.ClassId))
            .ToListAsync(ct);

        var microGroupByClassId = studentMicroGroups
            .Where(m => m.Group != null)
            .GroupBy(m => m.Group!.ClassId)
            .ToDictionary(g => g.Key, g => g.First().Group!);

        // Load Skill names via Content gRPC
        var skillNameMap = await GetSkillNameDictionaryAsync(ct);

        // Build per-class / per-course / per-term progress summary
        var classProgressList = new List<StudentCourseTermProgressDto>();
        var sessionItems = new List<StudentTimetableSessionItemDto>();

        foreach (var cls in filteredClassList)
        {
            var allClassSessions = cls.Sessions.Where(s => s.Status != "CANCELLED").OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime).ToList();
            var completedCount = allClassSessions.Count(s => s.Status == "COMPLETED");
            var presentCount = allClassSessions.Count(s => s.Attendances.Any(a => a.StudentId == studentId && a.Status == "PRESENT"));
            var lateCount = allClassSessions.Count(s => s.Attendances.Any(a => a.StudentId == studentId && a.Status == "LATE"));
            var absentCount = allClassSessions.Count(s => s.Attendances.Any(a => a.StudentId == studentId && a.Status == "ABSENT"));
            var excusedCount = allClassSessions.Count(s => s.Attendances.Any(a => a.StudentId == studentId && a.Status == "EXCUSED"));

            var attendedTotal = presentCount + lateCount;
            var recordedTotal = presentCount + lateCount + absentCount + excusedCount;
            var attendanceRate = recordedTotal > 0
                ? Math.Round((double)attendedTotal / recordedTotal * 100.0, 1)
                : 0.0;

            var resolvedCourseId = cls.CourseId ?? primaryRegistration?.CourseId;
            var resolvedCourseCode = cls.Course?.Code ?? primaryRegistration?.Course?.Code;
            var resolvedCourseName = cls.Course?.Name ?? primaryRegistration?.Course?.Name;
            var resolvedTermId = cls.TermId ?? primaryRegistration?.TermId;
            var resolvedTermCode = cls.Term?.Code ?? primaryRegistration?.Term?.Code;
            var resolvedTermName = cls.Term?.Name ?? primaryRegistration?.Term?.Name;
            var resolvedAcademicYear = cls.Term?.AcademicYear ?? primaryRegistration?.Term?.AcademicYear ?? cls.AcademicYear;

            classProgressList.Add(new StudentCourseTermProgressDto(
                ClassId: cls.ClassId,
                ClassName: cls.Name,
                CourseId: resolvedCourseId,
                CourseCode: resolvedCourseCode,
                CourseName: resolvedCourseName,
                TermId: resolvedTermId,
                TermCode: resolvedTermCode,
                TermName: resolvedTermName,
                AcademicYear: resolvedAcademicYear,
                StartDate: cls.StartDate ?? cls.Term?.StartDate ?? primaryRegistration?.StartDate,
                EndDate: cls.EndDate ?? cls.Term?.EndDate ?? primaryRegistration?.EndDate,
                TotalSessions: allClassSessions.Count,
                CompletedSessions: completedCount,
                PresentCount: presentCount,
                AbsentCount: absentCount,
                LateCount: lateCount,
                ExcusedCount: excusedCount,
                AttendanceRatePercent: attendanceRate,
                WeeklyPattern: cls.Schedules
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.DayOfWeek)
                    .ThenBy(s => s.SlotNumber)
                    .Select(MapToScheduleSlotDto)
                    .ToList()
            ));

            microGroupByClassId.TryGetValue(cls.ClassId, out var microGroup);

            foreach (var session in cls.Sessions)
            {
                if (fromDate.HasValue && session.SessionDate < fromDate.Value)
                    continue;
                if (toDate.HasValue && session.SessionDate > toDate.Value)
                    continue;

                var att = session.Attendances.FirstOrDefault(a => a.StudentId == studentId);
                var effectiveTeacherId = session.TeacherId ?? cls.TeacherId;
                var isSubstitute = session.TeacherId.HasValue && cls.TeacherId.HasValue && session.TeacherId.Value != cls.TeacherId.Value;

                string? skillName = null;
                if (session.SkillId.HasValue && skillNameMap.TryGetValue(session.SkillId.Value, out var matchedSkillName))
                {
                    skillName = matchedSkillName;
                }

                sessionItems.Add(new StudentTimetableSessionItemDto(
                    SessionId: session.SessionId,
                    ClassId: cls.ClassId,
                    ClassName: cls.Name,
                    ClassType: cls.ClassType,
                    FocusDomain: cls.DomainCode,
                    CourseId: resolvedCourseId,
                    CourseCode: resolvedCourseCode,
                    CourseName: resolvedCourseName,
                    TermId: resolvedTermId,
                    TermCode: resolvedTermCode,
                    TermName: resolvedTermName,
                    AcademicYear: resolvedAcademicYear,
                    SessionNo: session.SessionNo,
                    SessionDate: session.SessionDate,
                    DayOfWeek: (int)session.SessionDate.DayOfWeek,
                    DayOfWeekLabel: GetVietnameseDayOfWeekLabel((int)session.SessionDate.DayOfWeek),
                    StartTime: session.StartTime,
                    EndTime: session.EndTime,
                    Room: session.Room ?? string.Empty,
                    TeacherId: effectiveTeacherId,
                    IsSubstituteTeacher: isSubstitute,
                    SkillId: session.SkillId,
                    SkillName: skillName,
                    TopicTitle: session.TopicTitle,
                    RescheduledFromId: session.RescheduledFromId,
                    SessionStatus: session.Status,
                    AttendanceStatus: att?.Status ?? "UNRECORDED",
                    CheckInAt: att?.CheckInAt,
                    AttendanceNote: att?.Note,
                    MicroGroupName: microGroup?.GroupName,
                    AssignedWorksheetTitle: microGroup?.AssignedWorksheetTitle ?? microGroup?.RecommendedWorksheetTitle
                ));
            }
        }

        var orderedSessions = sessionItems
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToList();

        var totalPresentInView = orderedSessions.Count(s => s.AttendanceStatus == "PRESENT" || s.AttendanceStatus == "LATE");
        var totalAbsentInView = orderedSessions.Count(s => s.AttendanceStatus == "ABSENT");
        var totalUpcomingInView = orderedSessions.Count(s => s.SessionStatus == "SCHEDULED");
        var totalRecordedInView = totalPresentInView + totalAbsentInView + orderedSessions.Count(s => s.AttendanceStatus == "EXCUSED");
        var overallRate = totalRecordedInView > 0
            ? Math.Round((double)totalPresentInView / totalRecordedInView * 100.0, 1)
            : 0.0;

        return Result.Success(new StudentTimetableResponseDto(
            StudentId: studentId,
            SelectedTermId: termId,
            SelectedCourseId: courseId,
            FromDate: fromDate,
            ToDate: toDate,
            TotalSessionsInView: orderedSessions.Count,
            PresentSessionsCount: totalPresentInView,
            AbsentSessionsCount: totalAbsentInView,
            UpcomingSessionsCount: totalUpcomingInView,
            OverallAttendanceRatePercent: overallRate,
            AvailableTerms: availableTerms,
            EnrolledClassesByTermAndCourse: classProgressList,
            Sessions: orderedSessions
        ));
    }

    public async Task<Result<TeacherScheduleResponseDto>> GetTeacherScheduleAsync(
        Guid teacherId,
        Guid? termId = null,
        Guid? courseId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        // Query classes where teacher is primary OR has substitute sessions
        var classesQuery = _context.Classes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Course)
            .Include(c => c.Term)
            .Include(c => c.Schedules)
            .Include(c => c.Enrollments)
            .Include(c => c.Groups)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Attendances)
            .Where(c => c.TeacherId == teacherId || c.Sessions.Any(s => s.TeacherId == teacherId));

        if (termId.HasValue && termId.Value != Guid.Empty)
        {
            classesQuery = classesQuery.Where(c => c.TermId == termId.Value);
        }

        if (courseId.HasValue && courseId.Value != Guid.Empty)
        {
            classesQuery = classesQuery.Where(c => c.CourseId == courseId.Value);
        }

        var involvedClasses = await classesQuery.ToListAsync(ct);

        var campusIds = involvedClasses.Select(c => c.CampusId).Distinct().ToList();
        var availableTermsEntities = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => campusIds.Count == 0 || campusIds.Contains(t.CampusId))
            .OrderByDescending(t => t.AcademicYear)
            .ThenBy(t => t.TermOrder)
            .ToListAsync(ct);

        var availableTerms = availableTermsEntities.Select(MapToAcademicTermDto).ToList();
        var skillNameMap = await GetSkillNameDictionaryAsync(ct);

        var classSummaries = new List<TeacherClassTermSummaryDto>();
        var sessionItems = new List<TeacherScheduleSessionItemDto>();

        foreach (var cls in involvedClasses)
        {
            bool isPrimaryClassTeacher = cls.TeacherId == teacherId;

            if (isPrimaryClassTeacher)
            {
                classSummaries.Add(new TeacherClassTermSummaryDto(
                    ClassId: cls.ClassId,
                    ClassName: cls.Name,
                    ClassType: cls.ClassType,
                    FocusDomain: cls.DomainCode,
                    CourseId: cls.CourseId,
                    CourseCode: cls.Course?.Code,
                    CourseName: cls.Course?.Name,
                    TermId: cls.TermId,
                    TermCode: cls.Term?.Code,
                    TermName: cls.Term?.Name,
                    AcademicYear: cls.Term?.AcademicYear ?? cls.AcademicYear,
                    EnrolledStudentsCount: cls.Enrollments.Count,
                    MaxCapacity: cls.MaxCapacity,
                    TotalSessions: cls.Sessions.Count(s => s.Status != "CANCELLED"),
                    CompletedSessions: cls.Sessions.Count(s => s.Status == "COMPLETED"),
                    WeeklyPattern: cls.Schedules
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.DayOfWeek)
                        .ThenBy(s => s.SlotNumber)
                        .Select(MapToScheduleSlotDto)
                        .ToList()
                ));
            }

            foreach (var session in cls.Sessions)
            {
                var effectiveTeacherId = session.TeacherId ?? cls.TeacherId;
                bool isSessionTaughtByTeacher = effectiveTeacherId == teacherId;

                if (!isPrimaryClassTeacher && !isSessionTaughtByTeacher)
                {
                    continue;
                }

                if (fromDate.HasValue && session.SessionDate < fromDate.Value)
                    continue;
                if (toDate.HasValue && session.SessionDate > toDate.Value)
                    continue;

                string teachingRole;
                if (isPrimaryClassTeacher && isSessionTaughtByTeacher)
                {
                    teachingRole = "PRIMARY";
                }
                else if (!isPrimaryClassTeacher && isSessionTaughtByTeacher)
                {
                    teachingRole = "SUBSTITUTE_IN";
                }
                else
                {
                    teachingRole = "SUBSTITUTE_OUT";
                }

                string? skillName = null;
                if (session.SkillId.HasValue && skillNameMap.TryGetValue(session.SkillId.Value, out var matchedSkillName))
                {
                    skillName = matchedSkillName;
                }

                sessionItems.Add(new TeacherScheduleSessionItemDto(
                    SessionId: session.SessionId,
                    ClassId: cls.ClassId,
                    ClassName: cls.Name,
                    ClassType: cls.ClassType,
                    FocusDomain: cls.DomainCode,
                    CourseId: cls.CourseId,
                    CourseCode: cls.Course?.Code,
                    CourseName: cls.Course?.Name,
                    TermId: cls.TermId,
                    TermCode: cls.Term?.Code,
                    TermName: cls.Term?.Name,
                    AcademicYear: cls.Term?.AcademicYear ?? cls.AcademicYear,
                    SessionNo: session.SessionNo,
                    SessionDate: session.SessionDate,
                    DayOfWeek: (int)session.SessionDate.DayOfWeek,
                    DayOfWeekLabel: GetVietnameseDayOfWeekLabel((int)session.SessionDate.DayOfWeek),
                    StartTime: session.StartTime,
                    EndTime: session.EndTime,
                    Room: session.Room ?? string.Empty,
                    PrimaryTeacherId: cls.TeacherId,
                    ActualTeacherId: effectiveTeacherId,
                    TeachingRole: teachingRole,
                    SkillId: session.SkillId,
                    SkillName: skillName,
                    TopicTitle: session.TopicTitle,
                    RescheduledFromId: session.RescheduledFromId,
                    SessionStatus: session.Status,
                    Notes: session.Notes,
                    EnrolledStudentsCount: cls.Enrollments.Count,
                    PresentCount: session.Attendances.Count(a => a.Status == "PRESENT" || a.Status == "LATE"),
                    AbsentCount: session.Attendances.Count(a => a.Status == "ABSENT"),
                    MicroGroupsCount: cls.Groups.Count
                ));
            }
        }

        var orderedSessions = sessionItems
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToList();

        return Result.Success(new TeacherScheduleResponseDto(
            TeacherId: teacherId,
            SelectedTermId: termId,
            SelectedCourseId: courseId,
            FromDate: fromDate,
            ToDate: toDate,
            TotalAssignedClasses: classSummaries.Count,
            TotalSessionsInView: orderedSessions.Count,
            CompletedSessionsCount: orderedSessions.Count(s => s.SessionStatus == "COMPLETED" && s.TeachingRole != "SUBSTITUTE_OUT"),
            SubstituteSessionsCount: orderedSessions.Count(s => s.TeachingRole == "SUBSTITUTE_IN"),
            UpcomingSessionsCount: orderedSessions.Count(s => s.SessionStatus == "SCHEDULED" && s.TeachingRole != "SUBSTITUTE_OUT"),
            AvailableTerms: availableTerms,
            AssignedClassesByTermAndCourse: classSummaries,
            Sessions: orderedSessions
        ));
    }

    // =========================================================================
    // HELPER MAPPERS
    // =========================================================================
    private async Task<IReadOnlyList<ClassSessionDetailDto>> GetClassSessionsInternalAsync(
        Class cls,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyList<SkillTreeNodeDto> skills,
        CancellationToken ct)
    {
        var skillMap = skills.ToDictionary(s => s.SkillId, s => s.Name);

        var query = cls.Sessions.AsEnumerable();
        if (fromDate.HasValue)
        {
            query = query.Where(s => s.SessionDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(s => s.SessionDate <= toDate.Value);
        }

        var ordered = query
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ThenBy(s => s.SessionNo)
            .Select(s => MapToSessionDetailDto(s, cls, skillMap))
            .ToList();

        return await Task.FromResult(ordered);
    }

    private async Task<Dictionary<Guid, string>> GetSkillNameDictionaryAsync(CancellationToken ct)
    {
        try
        {
            var skills = await _contentGrpcClient.GetSkillsTreeAsync(ct);
            return skills.ToDictionary(s => s.SkillId, s => s.Name);
        }
        catch
        {
            return new Dictionary<Guid, string>();
        }
    }

    private static AcademicTermDto MapToAcademicTermDto(AcademicTerm term)
    {
        return new AcademicTermDto(
            TermId: term.TermId,
            CampusId: term.CampusId,
            Code: term.Code,
            Name: term.Name,
            AcademicYear: term.AcademicYear,
            TermOrder: term.TermOrder,
            StartDate: term.StartDate,
            EndDate: term.EndDate,
            Status: term.Status,
            TotalClassesCount: term.Classes?.Count ?? 0,
            TotalRegistrationsCount: term.Registrations?.Count ?? 0,
            CreatedAt: term.CreatedAt
        );
    }

    private static ClassScheduleSlotDto MapToScheduleSlotDto(ClassSchedule s)
    {
        return new ClassScheduleSlotDto(
            ScheduleId: s.ScheduleId,
            DayOfWeek: s.DayOfWeek,
            DayOfWeekLabel: GetVietnameseDayOfWeekLabel(NormalizeDayOfWeek(s.DayOfWeek)),
            SlotNo: s.SlotNumber,
            StartTime: s.StartTime,
            EndTime: s.EndTime,
            Room: s.Room ?? string.Empty,
            IsActive: s.IsActive
        );
    }

    private static ClassOverviewDto MapToClassOverviewDto(Class c)
    {
        return new ClassOverviewDto(
            ClassId: c.ClassId,
            CampusId: c.CampusId,
            CourseId: c.CourseId,
            CourseCode: c.Course?.Code,
            CourseName: c.Course?.Name,
            TermId: c.TermId,
            TermCode: c.Term?.Code,
            TermName: c.Term?.Name,
            AcademicYear: c.Term?.AcademicYear ?? c.AcademicYear,
            Name: c.Name,
            TeacherId: c.TeacherId,
            ClassType: c.ClassType,
            ClassTypeLabel: c.ClassType == 1 ? "THEMATIC_REMEDIAL" : "FOUNDATION",
            FocusDomain: c.DomainCode,
            MaxCapacity: c.MaxCapacity,
            EnrolledStudentsCount: c.Enrollments?.Count ?? 0,
            StartDate: c.StartDate ?? c.Term?.StartDate,
            EndDate: c.EndDate ?? c.Term?.EndDate,
            Status: c.Status,
            WeeklySchedules: (c.Schedules ?? Array.Empty<ClassSchedule>())
                .Where(s => s.IsActive)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.SlotNumber)
                .Select(MapToScheduleSlotDto)
                .ToList(),
            TotalSessionsCount: (c.Sessions ?? Array.Empty<ClassSession>()).Count(s => s.Status != "CANCELLED"),
            CompletedSessionsCount: (c.Sessions ?? Array.Empty<ClassSession>()).Count(s => s.Status == "COMPLETED")
        );
    }

    private static ClassSessionDetailDto MapToSessionDetailDto(
        ClassSession s,
        Class cls,
        IReadOnlyDictionary<Guid, string> skillMap)
    {
        var effectiveTeacherId = s.TeacherId ?? cls.TeacherId;
        var isSubstitute = s.TeacherId.HasValue && cls.TeacherId.HasValue && s.TeacherId.Value != cls.TeacherId.Value;

        string? skillName = null;
        if (s.SkillId.HasValue && skillMap.TryGetValue(s.SkillId.Value, out var matchedName))
        {
            skillName = matchedName;
        }

        var attendances = s.Attendances ?? Array.Empty<SessionAttendance>();

        return new ClassSessionDetailDto(
            SessionId: s.SessionId,
            ClassId: cls.ClassId,
            ClassName: cls.Name,
            CourseId: cls.CourseId,
            CourseCode: cls.Course?.Code,
            CourseName: cls.Course?.Name,
            TermId: cls.TermId,
            TermCode: cls.Term?.Code,
            TermName: cls.Term?.Name,
            ScheduleId: s.ScheduleId,
            SessionNo: s.SessionNo,
            SessionDate: s.SessionDate,
            DayOfWeek: (int)s.SessionDate.DayOfWeek,
            DayOfWeekLabel: GetVietnameseDayOfWeekLabel((int)s.SessionDate.DayOfWeek),
            StartTime: s.StartTime,
            EndTime: s.EndTime,
            Room: s.Room ?? string.Empty,
            PrimaryTeacherId: cls.TeacherId,
            ActualTeacherId: effectiveTeacherId,
            IsSubstituteTeacher: isSubstitute,
            SkillId: s.SkillId,
            SkillName: skillName,
            TopicTitle: s.TopicTitle,
            RescheduledFromId: s.RescheduledFromId,
            Status: s.Status,
            Notes: s.Notes,
            TotalStudentsCount: cls.Enrollments?.Count ?? 0,
            PresentCount: attendances.Count(a => a.Status == "PRESENT"),
            AbsentCount: attendances.Count(a => a.Status == "ABSENT"),
            LateCount: attendances.Count(a => a.Status == "LATE"),
            ExcusedCount: attendances.Count(a => a.Status == "EXCUSED")
        );
    }

    private static int NormalizeDayOfWeek(int dayOfWeek)
    {
        // Supports standard C# DayOfWeek (0 = Sunday .. 6 = Saturday) and 7/8 = Sunday
        if (dayOfWeek == 7 || dayOfWeek == 8)
            return 0;
        return Math.Clamp(dayOfWeek, 0, 6);
    }

    private static string GetVietnameseDayOfWeekLabel(int dayOfWeek)
    {
        return NormalizeDayOfWeek(dayOfWeek) switch
        {
            0 => "Chủ Nhật",
            1 => "Thứ Hai",
            2 => "Thứ Ba",
            3 => "Thứ Tư",
            4 => "Thứ Năm",
            5 => "Thứ Sáu",
            6 => "Thứ Bảy",
            _ => $"Thứ ({dayOfWeek})"
        };
    }
}
