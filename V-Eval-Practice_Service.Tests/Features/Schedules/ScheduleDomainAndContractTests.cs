using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using V_Eval_Practice_Service.Application.Features.Schedules.DTOs;
using V_Eval_Practice_Service.Domain.Entities;
using Xunit;

namespace V_Eval_Practice_Service.Tests.Features.Schedules;

public class ScheduleDomainAndContractTests
{
    [Fact]
    public void AcademicTerm_ShouldLinkClassesAndRegistrationsByTerm()
    {
        // Arrange
        var campusId = Guid.NewGuid();
        var term = new AcademicTerm
        {
            TermId = Guid.NewGuid(),
            CampusId = campusId,
            Code = "2026_HK1",
            Name = "Học kỳ 1 (2026-2027)",
            AcademicYear = "2026-2027",
            TermOrder = 1,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            Status = "ACTIVE"
        };

        var course = new Course
        {
            CourseId = Guid.NewGuid(),
            CampusId = campusId,
            Code = "DGNL_STD_12W",
            Name = "Khóa Chuẩn 12 Tuần",
            DurationWeeks = 12,
            MaxClassSize = 20
        };

        var cls = new Class
        {
            ClassId = Guid.NewGuid(),
            CampusId = campusId,
            TermId = term.TermId,
            Term = term,
            CourseId = course.CourseId,
            Course = course,
            Name = "Lớp ĐGNL-HK1-01",
            AcademicYear = term.AcademicYear,
            MaxCapacity = 20,
            StartDate = term.StartDate,
            EndDate = term.EndDate
        };

        var registration = new CourseRegistration
        {
            RegistrationId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = course.CourseId,
            TermId = term.TermId,
            Term = term,
            Status = "ACTIVE"
        };

        term.Classes.Add(cls);
        term.Registrations.Add(registration);

        // Assert
        term.Classes.Should().ContainSingle(c => c.ClassId == cls.ClassId);
        term.Registrations.Should().ContainSingle(r => r.RegistrationId == registration.RegistrationId);
        cls.Term.Should().BeSameAs(term);
        cls.Course.Should().BeSameAs(course);
    }

    [Fact]
    public void ClassSchedule_And_ClassSession_ShouldSupportSubstituteTeacherAndMakeupSessionChain()
    {
        // Arrange
        var primaryTeacherId = Guid.NewGuid();
        var substituteTeacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        var weeklySlot = new ClassSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ClassId = classId,
            DayOfWeek = 2, // Thứ Ba
            SlotNumber = 5,
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(20, 0),
            Room = "P.201"
        };

        var originalSession = new ClassSession
        {
            SessionId = Guid.NewGuid(),
            ClassId = classId,
            ScheduleId = weeklySlot.ScheduleId,
            Schedule = weeklySlot,
            SessionNo = 4,
            SessionDate = new DateOnly(2026, 10, 13),
            StartTime = weeklySlot.StartTime,
            EndTime = weeklySlot.EndTime,
            Room = weeklySlot.Room,
            TeacherId = primaryTeacherId,
            TopicTitle = "Buổi 4: Cực trị hàm số hợp",
            Status = "RESCHEDULED"
        };

        var makeupSession = new ClassSession
        {
            SessionId = Guid.NewGuid(),
            ClassId = classId,
            ScheduleId = weeklySlot.ScheduleId,
            SessionNo = 4,
            SessionDate = new DateOnly(2026, 10, 18),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(11, 0),
            Room = "P.204",
            TeacherId = substituteTeacherId,
            TopicTitle = "[Học bù] Buổi 4: Cực trị hàm số hợp",
            RescheduledFromId = originalSession.SessionId,
            RescheduledFromSession = originalSession,
            Status = "SCHEDULED"
        };

        originalSession.MakeupSessions.Add(makeupSession);

        // Assert
        originalSession.Status.Should().Be("RESCHEDULED");
        makeupSession.RescheduledFromId.Should().Be(originalSession.SessionId);
        makeupSession.TeacherId.Should().Be(substituteTeacherId);
        originalSession.MakeupSessions.Should().ContainSingle(m => m.SessionId == makeupSession.SessionId);
    }

    [Fact]
    public void SessionAttendance_ShouldTrackStudentStatusAndLinkToSession()
    {
        // Arrange
        var session = new ClassSession
        {
            SessionId = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            SessionNo = 1,
            SessionDate = new DateOnly(2026, 10, 6),
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(20, 0),
            Room = "P.201",
            Status = "COMPLETED"
        };

        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var s3 = Guid.NewGuid();

        session.Attendances.Add(new SessionAttendance
        {
            AttendanceId = Guid.NewGuid(),
            SessionId = session.SessionId,
            StudentId = s1,
            Status = "PRESENT",
            CheckInAt = DateTime.UtcNow
        });

        session.Attendances.Add(new SessionAttendance
        {
            AttendanceId = Guid.NewGuid(),
            SessionId = session.SessionId,
            StudentId = s2,
            Status = "LATE",
            CheckInAt = DateTime.UtcNow,
            Note = "Trễ 10 phút"
        });

        session.Attendances.Add(new SessionAttendance
        {
            AttendanceId = Guid.NewGuid(),
            SessionId = session.SessionId,
            StudentId = s3,
            Status = "ABSENT",
            Note = "Vắng không phép"
        });

        // Assert
        session.Attendances.Should().HaveCount(3);
        session.Attendances.Count(a => a.Status == "PRESENT" || a.Status == "LATE").Should().Be(2);
        session.Attendances.Count(a => a.Status == "ABSENT").Should().Be(1);
    }

    [Fact]
    public void DiagnosticSubmissionHistory_ShouldPopulateRadarChartAndSkillPriors()
    {
        // Arrange
        var questions = new List<V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs.QuestionResultDto>
        {
            GetDiagnosticSubmissionByIdHandlerTestHelper.CreateQuestion(1, true, "sk_math_01", "Hàm số & Cực trị", "dom_math", "Toán học & Phân tích số liệu"),
            GetDiagnosticSubmissionByIdHandlerTestHelper.CreateQuestion(2, false, "sk_math_01", "Hàm số & Cực trị", "dom_math", "Toán học & Phân tích số liệu"),
            GetDiagnosticSubmissionByIdHandlerTestHelper.CreateQuestion(3, true, "sk_lang_01", "Đọc hiểu Tiếng Việt", "dom_lang", "Sử dụng Ngôn ngữ")
        };

        var skillBreakdowns = new List<V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs.SkillDiagnosticDto>
        {
            new()
            {
                SkillId = "sk_math_01",
                SkillName = "Hàm số & Cực trị",
                DomainId = "dom_math",
                DomainName = "Toán học & Phân tích số liệu",
                TotalQuestions = 2,
                CorrectCount = 1,
                AccuracyPercentage = 50.0,
                IsWeak = true
            },
            new()
            {
                SkillId = "sk_lang_01",
                SkillName = "Đọc hiểu Tiếng Việt",
                DomainId = "dom_lang",
                DomainName = "Sử dụng Ngôn ngữ",
                TotalQuestions = 1,
                CorrectCount = 1,
                AccuracyPercentage = 100.0,
                IsWeak = false
            }
        };

        // Act
        var radar = V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionById.GetDiagnosticSubmissionByIdHandler.BuildRadarChart(questions, 66.7);
        var priors = V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionById.GetDiagnosticSubmissionByIdHandler.BuildSkillPriors(skillBreakdowns, 0.35);

        // Assert
        radar.Should().HaveCount(2);
        radar.Should().Contain(r => r.DomainId == "dom_math" && r.StudentPct == 50.0);
        radar.Should().Contain(r => r.DomainId == "dom_lang" && r.StudentPct == 100.0);

        priors.Should().HaveCount(2);
        priors.Should().Contain(p => p.SkillId == "sk_math_01" && p.PL0 > 0 && p.PL0 < 1);
        priors.Should().Contain(p => p.SkillId == "sk_lang_01" && p.PL0 >= 0.85);
    }
}

internal static class GetDiagnosticSubmissionByIdHandlerTestHelper
{
    public static V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs.QuestionResultDto CreateQuestion(
        int order,
        bool isCorrect,
        string skillId,
        string skillName,
        string domainId,
        string domainName) => new()
    {
        QuestionId = Guid.NewGuid(),
        QuestionOrder = order,
        IsCorrect = isCorrect,
        SkillId = skillId,
        SkillName = skillName,
        DomainId = domainId,
        DomainName = domainName,
        DifficultyLevel = 2,
        TimeSpentSeconds = 30
    };
}


