using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Analytics.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Analytics.Queries.GetParentDashboard;

public class GetParentDashboardQueryHandler : IRequestHandler<GetParentDashboardQuery, Result<ParentDashboardResponseDto>>
{
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly IStudentActivityRepository _activityRepo;
    private readonly IClassHeatmapRepository _heatmapRepo;
    private readonly IClassHeatmapAggregatorService _aggregatorService;
    private readonly ILogger<GetParentDashboardQueryHandler> _logger;

    public GetParentDashboardQueryHandler(
        IIdentityGrpcClient identityGrpcClient,
        IStudentActivityRepository activityRepo,
        IClassHeatmapRepository heatmapRepo,
        IClassHeatmapAggregatorService aggregatorService,
        ILogger<GetParentDashboardQueryHandler> logger)
    {
        _identityGrpcClient = identityGrpcClient;
        _activityRepo = activityRepo;
        _heatmapRepo = heatmapRepo;
        _aggregatorService = aggregatorService;
        _logger = logger;
    }

    public async Task<Result<ParentDashboardResponseDto>> Handle(GetParentDashboardQuery request, CancellationToken ct)
    {
        var parentId = request.ParentId;
        _logger.LogInformation("[Parent Portal] Truy vấn dữ liệu con cho Phụ huynh {ParentId}...", parentId);

        // 1. Gọi Identity gRPC lấy danh sách học sinh liên kết
        var linkedStudents = await _identityGrpcClient.GetParentStudentsAsync(parentId, ct);

        var childrenList = new List<ChildAnalyticsDto>();

        // Nếu phụ huynh chưa liên kết với học sinh nào trong cơ sở dữ liệu, trả về danh sách rỗng 100% thực tế (không dùng mock fallback)
        if (!linkedStudents.Any())
        {
            return Result<ParentDashboardResponseDto>.Success(new ParentDashboardResponseDto(parentId, childrenList));
        }

        foreach (var student in linkedStudents)
        {
            var studentId = student.StudentId;

            // 2. Lấy hoặc tính toán Chỉ số sẵn sàng thi (Readiness Metric R_exam)
            var metric = await _activityRepo.GetReadinessMetricAsync(studentId, ct);
            if (metric == null)
            {
                await _aggregatorService.ComputeStudentReadinessAsync(studentId, ct);
                metric = await _activityRepo.GetReadinessMetricAsync(studentId, ct);
            }

            var readinessDto = new ExamReadinessDto(
                ReadinessIndex: metric?.ExamReadinessIndex ?? 0.0,
                ReadinessTier: metric?.ReadinessTier ?? "PREPARING",
                NormalizedThetaScore: metric?.NormalizedTheta ?? 0.0,
                SkillCoverageScore: metric?.SkillCoveragePlt ?? 0.0,
                ConsistencyScore: metric?.ConsistencyScore ?? 0.0
            );

            // 3. Tính toán kỷ luật tự học tuần (Weekly Disciplines)
            double studyHours = await _activityRepo.CalculateStudyHoursInPastDaysAsync(studentId, 7, ct);
            if (studyHours < 0) studyHours = 0;

            int currentStreak = await _activityRepo.CalculateCurrentStreakAsync(studentId, ct);
            if (currentStreak < 0) currentStreak = 0;

            double recommendedHours = 12.0;
            string completionStatus = studyHours >= recommendedHours
                ? "EXCEEDED_GOAL"
                : (studyHours >= recommendedHours * 0.7 ? "ON_TRACK" : "NEEDS_IMPROVEMENT");

            var disciplinesDto = new WeeklyDisciplinesDto(
                StudyHoursThisWeek: studyHours,
                RecommendedHours: recommendedHours,
                CurrentStreak: currentStreak,
                CompletionStatus: completionStatus
            );

            // 4. Sinh cảnh báo AI thông minh dành riêng cho phụ huynh theo dữ liệu thực
            var aiAlerts = new List<AiParentAlertDto>();
            if (metric != null && metric.ExamReadinessIndex >= 80)
            {
                aiAlerts.Add(new(
                    Type: "SUCCESS",
                    Title: "Tiến độ học tập xuất sắc",
                    Description: $"{student.Name} duy trì chỉ số sẵn sàng thi ở mức cao ({metric.ExamReadinessIndex:F1}/100)."
                ));
            }
            else if (studyHours < 5.0)
            {
                aiAlerts.Add(new(
                    Type: "WARNING",
                    Title: "Cần cải thiện thời lượng tự học",
                    Description: $"{student.Name} ghi nhận {studyHours:F1} giờ tự học tuần này (mục tiêu {recommendedHours} giờ). Gia đình nên nhắc nhở và động viên con."
                ));
            }
            else
            {
                aiAlerts.Add(new(
                    Type: "INFO",
                    Title: "Đang duy trì tiến độ học tập",
                    Description: $"{student.Name} đang học tập đều đặn với chuỗi streak {currentStreak} ngày."
                ));
            }

            childrenList.Add(new ChildAnalyticsDto(
                StudentId: studentId,
                StudentName: student.Name,
                Grade: "Học viên V-Eval",
                School: string.IsNullOrWhiteSpace(student.CampusId) ? "Hệ thống Khảo thí V-Eval" : $"Cơ sở {student.CampusId}",
                TargetMajor: "Đại học",
                AssignedTeacherName: "Cố vấn Học tập",
                TeacherContactPhone: "1900 8889",
                ExamReadiness: readinessDto,
                WeeklyDisciplines: disciplinesDto,
                AiParentAlerts: aiAlerts
            ));
        }

        var response = new ParentDashboardResponseDto(parentId, childrenList);
        return Result<ParentDashboardResponseDto>.Success(response);
    }
}
