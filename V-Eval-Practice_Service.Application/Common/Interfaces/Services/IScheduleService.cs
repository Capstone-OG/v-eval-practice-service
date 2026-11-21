using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Schedules.DTOs;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Services;

public interface IScheduleService
{
    // 1. Academic Terms (Học kỳ / Đợt tuyển sinh)
    Task<Result<IReadOnlyList<AcademicTermDto>>> GetAcademicTermsAsync(
        Guid? campusId = null,
        string? academicYear = null,
        string? status = null,
        CancellationToken ct = default);

    Task<Result<AcademicTermDto>> CreateAcademicTermAsync(
        CreateAcademicTermRequestDto request,
        CancellationToken ct = default);

    Task<Result<AcademicTermDto>> UpdateAcademicTermStatusAsync(
        Guid termId,
        string status,
        CancellationToken ct = default);

    // 2. Courses (Khóa học / Chương trình đào tạo)
    Task<Result<IReadOnlyList<CourseSummaryDto>>> GetCoursesAsync(
        Guid? campusId = null,
        bool? isActive = null,
        CancellationToken ct = default);

    Task<Result<CourseSummaryDto>> CreateCourseAsync(
        CreateCourseRequestDto request,
        CancellationToken ct = default);

    Task<Result<CourseSummaryDto>> UpdateCourseAsync(
        Guid courseId,
        UpdateCourseRequestDto request,
        CancellationToken ct = default);

    Task<Result<string>> DeleteCourseAsync(
        Guid courseId,
        CancellationToken ct = default);

    // 3. Classes & Weekly Schedule Patterns (Lớp học & Khung lịch tuần)
    Task<Result<IReadOnlyList<ClassOverviewDto>>> GetClassesAsync(
        Guid? campusId = null,
        Guid? termId = null,
        Guid? courseId = null,
        Guid? teacherId = null,
        CancellationToken ct = default);

    Task<Result<ClassOverviewDto>> CreateClassWithScheduleAsync(
        CreateClassWithScheduleRequestDto request,
        Guid? assignedBy = null,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<ClassScheduleSlotDto>>> SaveClassSchedulesAsync(
        Guid classId,
        SaveClassSchedulesRequestDto request,
        CancellationToken ct = default);

    // 4. Class Sessions (Tự động sinh buổi học, Dạy thay, Đổi phòng, Học bù)
    Task<Result<GenerateClassSessionsResponseDto>> GenerateClassSessionsAsync(
        Guid classId,
        GenerateClassSessionsRequestDto request,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<ClassSessionDetailDto>>> GetClassSessionsAsync(
        Guid classId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);

    Task<Result<ClassSessionDetailDto>> UpdateClassSessionAsync(
        Guid sessionId,
        UpdateClassSessionRequestDto request,
        CancellationToken ct = default);

    Task<Result<ClassSessionDetailDto>> RescheduleClassSessionAsync(
        Guid sessionId,
        RescheduleClassSessionRequestDto request,
        CancellationToken ct = default);

    // 5. Session Attendances (Điểm danh chuyên cần & Cảnh báo sớm BR-04)
    Task<Result<SessionAttendanceSummaryResponseDto>> GetSessionAttendancesAsync(
        Guid sessionId,
        CancellationToken ct = default);

    Task<Result<SessionAttendanceSummaryResponseDto>> SubmitSessionAttendancesAsync(
        Guid sessionId,
        SubmitSessionAttendanceRequestDto request,
        Guid? recordedBy = null,
        CancellationToken ct = default);

    // 6. Student Timetable & Teacher Schedule Views (Theo Kỳ, Theo Khóa, Theo Tuần)
    Task<Result<StudentTimetableResponseDto>> GetStudentTimetableAsync(
        Guid studentId,
        Guid? termId = null,
        Guid? courseId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);

    Task<Result<TeacherScheduleResponseDto>> GetTeacherScheduleAsync(
        Guid teacherId,
        Guid? termId = null,
        Guid? courseId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);
}

