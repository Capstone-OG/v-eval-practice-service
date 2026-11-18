using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Features.Analytics.Queries.GetParentDashboard;
using V_Eval_Practice_Service.Domain.Entities;
using Xunit;

namespace V_Eval_Practice_Service.Tests.Features.Analytics;

public class GetParentDashboardQueryHandlerTests
{
    private readonly Mock<IIdentityGrpcClient> _identityGrpcMock;
    private readonly Mock<IStudentActivityRepository> _activityRepoMock;
    private readonly Mock<IClassHeatmapRepository> _heatmapRepoMock;
    private readonly Mock<IClassHeatmapAggregatorService> _aggregatorServiceMock;
    private readonly Mock<IClassEnrollmentRepository> _classEnrollmentRepoMock;
    private readonly Mock<ILogger<GetParentDashboardQueryHandler>> _loggerMock;
    private readonly GetParentDashboardQueryHandler _handler;

    public GetParentDashboardQueryHandlerTests()
    {
        _identityGrpcMock = new Mock<IIdentityGrpcClient>();
        _activityRepoMock = new Mock<IStudentActivityRepository>();
        _heatmapRepoMock = new Mock<IClassHeatmapRepository>();
        _aggregatorServiceMock = new Mock<IClassHeatmapAggregatorService>();
        _classEnrollmentRepoMock = new Mock<IClassEnrollmentRepository>();
        _loggerMock = new Mock<ILogger<GetParentDashboardQueryHandler>>();

        _handler = new GetParentDashboardQueryHandler(
            _identityGrpcMock.Object,
            _activityRepoMock.Object,
            _heatmapRepoMock.Object,
            _aggregatorServiceMock.Object,
            _classEnrollmentRepoMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Handle_WhenNoLinkedStudents_ReturnsEmptyChildrenList()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        _identityGrpcMock
            .Setup(c => c.GetParentStudentsAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid StudentId, string Name, string Email, string CampusId, string SchoolName, int TargetScore)>());

        var query = new GetParentDashboardQuery(parentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ParentId.Should().Be(parentId);
        result.Value.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenLinkedStudentExists_ResolvesRealClassAndTeacherFromGrpcAndEnrollment()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var studentTuple = (
            StudentId: studentId,
            Name: "Phạm Tấn Lộc",
            Email: "loc.pham@example.com",
            CampusId: "CAMPUS_Q1",
            SchoolName: "THPT Chuyên Lê Hồng Phong - TP.HCM",
            TargetScore: 920
        );

        _identityGrpcMock
            .Setup(c => c.GetParentStudentsAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, string, string, string, int)> { studentTuple });

        var mockClass = new Class
        {
            ClassId = Guid.NewGuid(),
            Name = "Lớp Bứt Phá Điểm 10 - Offline Quận 1",
            TeacherId = teacherId
        };

        var mockEnrollment = new ClassEnrollment
        {
            EnrollmentId = Guid.NewGuid(),
            StudentId = studentId,
            ClassId = mockClass.ClassId,
            Class = mockClass,
            Status = "ENROLLED"
        };

        _classEnrollmentRepoMock
            .Setup(r => r.GetEnrollmentByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockEnrollment);

        _identityGrpcMock
            .Setup(c => c.GetTeacherProfileAsync(teacherId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Exists: true, FullName: "ThS. Hoàng Trọng Nghĩa", Phone: "0909 112 233", Email: "nghia.ht@veval.edu.vn", Specialty: "Toán học & Khoa học Tự nhiên"));

        var readinessMetric = new StudentReadinessMetric
        {
            StudentId = studentId,
            ExamReadinessIndex = 88.5,
            ReadinessTier = "READY",
            NormalizedTheta = 890,
            SkillCoveragePlt = 85.0,
            ConsistencyScore = 90.0
        };

        _activityRepoMock
            .Setup(r => r.GetReadinessMetricAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readinessMetric);

        _activityRepoMock
            .Setup(r => r.CalculateStudyHoursInPastDaysAsync(studentId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(14.5);

        _activityRepoMock
            .Setup(r => r.CalculateCurrentStreakAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var query = new GetParentDashboardQuery(parentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Children.Should().HaveCount(1);

        var child = result.Value.Children[0];
        child.StudentId.Should().Be(studentId);
        child.StudentName.Should().Be("Phạm Tấn Lộc");
        child.Grade.Should().Be("Lớp Bứt Phá Điểm 10 - Offline Quận 1");
        child.School.Should().Be("THPT Chuyên Lê Hồng Phong - TP.HCM");
        child.TargetMajor.Should().Contain("Khoa học Máy tính - ĐH Bách Khoa");
        child.AssignedTeacherName.Should().Be("ThS. Hoàng Trọng Nghĩa");
        child.TeacherContactPhone.Should().Be("0909 112 233");
        child.ExamReadiness.ReadinessIndex.Should().Be(88.5);
        child.WeeklyDisciplines.StudyHoursThisWeek.Should().Be(14.5);
        child.WeeklyDisciplines.CompletionStatus.Should().Be("EXCEEDED_GOAL");
    }
}
