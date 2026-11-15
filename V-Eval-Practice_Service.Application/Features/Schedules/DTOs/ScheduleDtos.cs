using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Schedules.DTOs;

// ==========================================
// 1. ACADEMIC TERMS (HỌC KỲ / ĐỢT TUYỂN SINH)
// ==========================================
public record AcademicTermDto(
    Guid TermId,
    Guid CampusId,
    string Code,
    string Name,
    string AcademicYear,
    int TermOrder,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int TotalClassesCount,
    int TotalRegistrationsCount,
    DateTime CreatedAt
);

public record CreateAcademicTermRequestDto(
    Guid CampusId,
    string Code,
    string Name,
    string AcademicYear,
    int TermOrder,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status = "ACTIVE"
);

public record UpdateAcademicTermStatusRequestDto(
    string Status
);

// ==========================================
// 2. COURSES (KHÓA HỌC / CHƯƠNG TRÌNH ĐÀO TẠO)
// ==========================================
public record CourseSummaryDto(
    Guid CourseId,
    Guid CampusId,
    string Code,
    string Name,
    string PlanType,
    int DurationWeeks,
    decimal TuitionFee,
    string RetakePolicy,
    decimal RetakeFee,
    int MaxClassSize,
    bool IsActive,
    int TotalClassesCount,
    DateTime CreatedAt
);

public record CreateCourseRequestDto(
    Guid CampusId,
    string Code,
    string Name,
    string PlanType = "STANDARD",
    int DurationWeeks = 12,
    decimal TuitionFee = 4500000m,
    string RetakePolicy = "FREE_1X",
    decimal RetakeFee = 0m,
    int MaxClassSize = 20
);

// ==========================================
// 3. CLASSES (LỚP HỌC THEO KỲ & KHÓA)
// ==========================================
public record ClassScheduleSlotDto(
    Guid? ScheduleId,
    int DayOfWeek,
    string DayOfWeekLabel,
    int SlotNo,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Room,
    bool IsActive = true
);

public record ClassOverviewDto(
    Guid ClassId,
    Guid CampusId,
    Guid? CourseId,
    string? CourseCode,
    string? CourseName,
    Guid? TermId,
    string? TermCode,
    string? TermName,
    string? AcademicYear,
    string Name,
    Guid? TeacherId,
    int ClassType,
    string ClassTypeLabel,
    string? FocusDomain,
    int MaxCapacity,
    int EnrolledStudentsCount,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    IReadOnlyList<ClassScheduleSlotDto> WeeklySchedules,
    int TotalSessionsCount,
    int CompletedSessionsCount
);

public record CreateClassWithScheduleRequestDto(
    Guid CampusId,
    Guid? CourseId,
    Guid? TermId,
    string Name,
    Guid? TeacherId,
    int ClassType = 0,
    string? FocusDomain = null,
    int MaxCapacity = 20,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    IReadOnlyList<ClassScheduleSlotInputDto>? WeeklySchedules = null,
    bool AutoGenerateSessions = true
);

public record ClassScheduleSlotInputDto(
    int DayOfWeek,
    int SlotNo,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Room
);

public record SaveClassSchedulesRequestDto(
    IReadOnlyList<ClassScheduleSlotInputDto> Slots,
    bool RegenerateFutureSessions = false
);

// ==========================================
// 4. CLASS SESSIONS (BUỔI HỌC THỰC TẾ, DẠY THAY, HỌC BÙ)
// ==========================================
public record ClassSessionDetailDto(
    Guid SessionId,
    Guid ClassId,
    string ClassName,
    Guid? CourseId,
    string? CourseCode,
    string? CourseName,
    Guid? TermId,
    string? TermCode,
    string? TermName,
    Guid? ScheduleId,
    int SessionNo,
    DateOnly SessionDate,
    int DayOfWeek,
    string DayOfWeekLabel,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Room,
    Guid? PrimaryTeacherId,
    Guid? ActualTeacherId,
    bool IsSubstituteTeacher,
    Guid? SkillId,
    string? SkillName,
    string? TopicTitle,
    Guid? RescheduledFromId,
    string Status,
    string? Notes,
    int TotalStudentsCount,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount
);

public record GenerateClassSessionsRequestDto(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    bool OverwriteScheduledSessions = false
);

public record GenerateClassSessionsResponseDto(
    Guid ClassId,
    string ClassName,
    DateOnly FromDate,
    DateOnly ToDate,
    int GeneratedSessionsCount,
    int SkippedConflictCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ClassSessionDetailDto> Sessions
);

public record UpdateClassSessionRequestDto(
    DateOnly? SessionDate = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    string? Room = null,
    Guid? ActualTeacherId = null,
    Guid? SkillId = null,
    string? TopicTitle = null,
    string? Status = null,
    string? Notes = null
);

public record RescheduleClassSessionRequestDto(
    DateOnly NewSessionDate,
    TimeOnly NewStartTime,
    TimeOnly NewEndTime,
    string NewRoom,
    Guid? SubstituteTeacherId = null,
    string Reason = "Hoãn buổi học & xếp lịch học bù"
);

// ==========================================
// 5. SESSION ATTENDANCES (ĐIỂM DANH CHUYÊN CẦN)
// ==========================================
public record SessionAttendanceItemDto(
    Guid AttendanceId,
    Guid SessionId,
    Guid StudentId,
    string Status,
    DateTime? CheckInAt,
    Guid? RecordedBy,
    string? Note
);

public record StudentAttendanceInputDto(
    Guid StudentId,
    string Status,
    string? Note = null
);

public record SubmitSessionAttendanceRequestDto(
    IReadOnlyList<StudentAttendanceInputDto> Attendances,
    bool MarkSessionCompleted = true
);

public record SessionAttendanceSummaryResponseDto(
    Guid SessionId,
    Guid ClassId,
    string ClassName,
    int SessionNo,
    DateOnly SessionDate,
    string SessionStatus,
    int TotalEnrolled,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    int TriggeredAlertsCount,
    IReadOnlyList<SessionAttendanceItemDto> Records
);

// ==========================================
// 6. STUDENT TIMETABLE & TEACHER SCHEDULE VIEWS
// ==========================================
public record StudentTimetableSessionItemDto(
    Guid SessionId,
    Guid ClassId,
    string ClassName,
    int ClassType,
    string? FocusDomain,
    Guid? CourseId,
    string? CourseCode,
    string? CourseName,
    Guid? TermId,
    string? TermCode,
    string? TermName,
    string? AcademicYear,
    int SessionNo,
    DateOnly SessionDate,
    int DayOfWeek,
    string DayOfWeekLabel,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Room,
    Guid? TeacherId,
    bool IsSubstituteTeacher,
    Guid? SkillId,
    string? SkillName,
    string? TopicTitle,
    Guid? RescheduledFromId,
    string SessionStatus,
    string AttendanceStatus,
    DateTime? CheckInAt,
    string? AttendanceNote,
    string? MicroGroupName,
    string? AssignedWorksheetTitle
);

public record StudentCourseTermProgressDto(
    Guid ClassId,
    string ClassName,
    Guid? CourseId,
    string? CourseCode,
    string? CourseName,
    Guid? TermId,
    string? TermCode,
    string? TermName,
    string? AcademicYear,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int TotalSessions,
    int CompletedSessions,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    double AttendanceRatePercent,
    IReadOnlyList<ClassScheduleSlotDto> WeeklyPattern
);

public record StudentTimetableResponseDto(
    Guid StudentId,
    Guid? SelectedTermId,
    Guid? SelectedCourseId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int TotalSessionsInView,
    int PresentSessionsCount,
    int AbsentSessionsCount,
    int UpcomingSessionsCount,
    double OverallAttendanceRatePercent,
    IReadOnlyList<AcademicTermDto> AvailableTerms,
    IReadOnlyList<StudentCourseTermProgressDto> EnrolledClassesByTermAndCourse,
    IReadOnlyList<StudentTimetableSessionItemDto> Sessions
);

public record TeacherScheduleSessionItemDto(
    Guid SessionId,
    Guid ClassId,
    string ClassName,
    int ClassType,
    string? FocusDomain,
    Guid? CourseId,
    string? CourseCode,
    string? CourseName,
    Guid? TermId,
    string? TermCode,
    string? TermName,
    string? AcademicYear,
    int SessionNo,
    DateOnly SessionDate,
    int DayOfWeek,
    string DayOfWeekLabel,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Room,
    Guid? PrimaryTeacherId,
    Guid? ActualTeacherId,
    string TeachingRole,
    Guid? SkillId,
    string? SkillName,
    string? TopicTitle,
    Guid? RescheduledFromId,
    string SessionStatus,
    string? Notes,
    int EnrolledStudentsCount,
    int PresentCount,
    int AbsentCount,
    int MicroGroupsCount
);

public record TeacherClassTermSummaryDto(
    Guid ClassId,
    string ClassName,
    int ClassType,
    string? FocusDomain,
    Guid? CourseId,
    string? CourseCode,
    string? CourseName,
    Guid? TermId,
    string? TermCode,
    string? TermName,
    string? AcademicYear,
    int EnrolledStudentsCount,
    int MaxCapacity,
    int TotalSessions,
    int CompletedSessions,
    IReadOnlyList<ClassScheduleSlotDto> WeeklyPattern
);

public record TeacherScheduleResponseDto(
    Guid TeacherId,
    Guid? SelectedTermId,
    Guid? SelectedCourseId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int TotalAssignedClasses,
    int TotalSessionsInView,
    int CompletedSessionsCount,
    int SubstituteSessionsCount,
    int UpcomingSessionsCount,
    IReadOnlyList<AcademicTermDto> AvailableTerms,
    IReadOnlyList<TeacherClassTermSummaryDto> AssignedClassesByTermAndCourse,
    IReadOnlyList<TeacherScheduleSessionItemDto> Sessions
);
