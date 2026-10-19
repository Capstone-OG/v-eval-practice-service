using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Queries.GetClassHeatmap;

public class GetClassHeatmapQueryHandler : IRequestHandler<GetClassHeatmapQuery, Result<ClassHeatmapResponseDto>>
{
    private readonly IClassHeatmapRepository _heatmapRepo;
    private readonly IStudentActivityRepository _activityRepo;
    private readonly IClassHeatmapCacheService _cacheService;
    private readonly IClassHeatmapAggregatorService _aggregatorService;
    private readonly ILogger<GetClassHeatmapQueryHandler> _logger;

    public GetClassHeatmapQueryHandler(
        IClassHeatmapRepository heatmapRepo,
        IStudentActivityRepository activityRepo,
        IClassHeatmapCacheService cacheService,
        IClassHeatmapAggregatorService aggregatorService,
        ILogger<GetClassHeatmapQueryHandler> logger)
    {
        _heatmapRepo = heatmapRepo;
        _activityRepo = activityRepo;
        _cacheService = cacheService;
        _aggregatorService = aggregatorService;
        _logger = logger;
    }

    public async Task<Result<ClassHeatmapResponseDto>> Handle(GetClassHeatmapQuery request, CancellationToken ct)
    {
        string cacheKey = $"heatmap:{request.ClassId}:{request.DomainCode ?? "all"}";

        // 1. Kiểm tra bộ đệm Cache (< 300ms) khi không có bộ lọc động minMastery
        if (!request.MinMastery.HasValue)
        {
            var cached = await _cacheService.GetAsync<ClassHeatmapResponseDto>(cacheKey, ct);
            if (cached != null)
            {
                _logger.LogInformation("[Heatmap] Trả kết quả từ bộ đệm Cache cho Lớp {ClassId} (Key: {Key}).", request.ClassId, cacheKey);
                return Result<ClassHeatmapResponseDto>.Success(cached);
            }
        }

        // 2. Kiểm tra lớp học tồn tại
        var classEntity = await _heatmapRepo.GetClassByIdAsync(request.ClassId, ct);
        if (classEntity == null)
        {
            return Result<ClassHeatmapResponseDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học với ID {request.ClassId}."));
        }

        // 3. Lấy dữ liệu tổng hợp cột (ClassHeatmapSummary)
        var summaries = await _heatmapRepo.GetSummariesByClassIdAsync(request.ClassId, ct);
        if (!summaries.Any())
        {
            _logger.LogInformation("[Heatmap] Chưa có bảng tổng hợp, thực thi quét tổng hợp trực tiếp cho Lớp {ClassId}...", request.ClassId);
            await _aggregatorService.AggregateClassHeatmapAsync(request.ClassId, ct);
            summaries = await _heatmapRepo.GetSummariesByClassIdAsync(request.ClassId, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.DomainCode))
        {
            summaries = summaries.Where(s => s.DomainCode == request.DomainCode).ToList();
        }

        // 4. Xây dựng danh sách cảnh báo sư phạm (Pedagogical Alerts)
        var pedagogicalAlerts = summaries
            .Where(s => s.AlertStatus == "CRITICAL_RED" || s.StrugglingRate >= 0.40)
            .OrderByDescending(s => s.StrugglingRate)
            .Select(s => new PedagogicalAlertDto(
                SkillId: s.SkillId,
                SkillName: s.SkillName,
                DomainCode: s.DomainCode,
                StrugglingRate: s.StrugglingRate,
                StrugglingCount: s.StrugglingCount,
                AveragePlt: s.AveragePlt,
                AlertLevel: s.AlertStatus,
                SuggestedAction: s.IsBimodal
                    ? "Phát hiện phân hóa 2 cực: Khuyến nghị phân nhóm vi mô 3-5 bạn để kèm riêng và giao bài phân hóa."
                    : "Tổ chức buổi Live Q&A chuyên đề 30 - 45 phút để chữa lại dạng bài này."
            ))
            .ToList();

        // 5. Xây dựng danh sách cột (Columns)
        var columns = summaries.Select(s => new HeatmapColumnDto(
            SkillId: s.SkillId,
            SkillName: s.SkillName,
            DomainCode: s.DomainCode,
            ClassAveragePlt: s.AveragePlt,
            AlertStatus: s.AlertStatus,
            IsBimodal: s.IsBimodal
        )).ToList();

        // 6. Lấy danh sách học sinh ghi danh và các ô ma trận nhiệt
        var enrollments = await _heatmapRepo.GetClassEnrollmentsAsync(request.ClassId, ct);
        var cells = await _heatmapRepo.GetHeatmapsByClassIdAsync(request.ClassId, request.DomainCode, ct);
        var cellsByStudent = cells.GroupBy(c => c.StudentId).ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<HeatmapRowDto>();

        foreach (var enrollment in enrollments)
        {
            var studentId = enrollment.StudentId;
            cellsByStudent.TryGetValue(studentId, out var studentCellsList);
            studentCellsList ??= new List<Domain.Entities.ClassSkillHeatmap>();

            if (request.MinMastery.HasValue)
            {
                studentCellsList = studentCellsList.Where(c => c.PltValue >= request.MinMastery.Value).ToList();
            }

            var cellDtos = studentCellsList.Select(c => new HeatmapCellDto(
                SkillId: c.SkillId,
                Plt: c.PltValue,
                MasteryLevel: c.MasteryLevel,
                LastUpdated: c.UpdatedAt
            )).ToList();

            int weakCount = studentCellsList.Count(c => c.PltValue < 0.50);
            string riskStatus = weakCount >= 2
                ? "HIGH_RISK"
                : (weakCount == 1 ? "MEDIUM_RISK" : "SAFE");

            double avgPlt = studentCellsList.Any()
                ? Math.Round(studentCellsList.Average(c => c.PltValue), 2)
                : 0.50;

            rows.Add(new HeatmapRowDto(
                StudentId: studentId,
                StudentName: $"Học sinh #{studentId.ToString()[..6].ToUpper()}",
                AvatarUrl: null,
                ThetaScore: avgPlt,
                RiskStatus: riskStatus,
                SkillCells: cellDtos
            ));
        }

        var response = new ClassHeatmapResponseDto(
            ClassId: request.ClassId,
            ClassName: classEntity.Name ?? "Lớp học Cơ sở V-Eval",
            TotalStudents: enrollments.Count,
            LastAggregatedAt: summaries.FirstOrDefault()?.CalculatedAt ?? DateTime.UtcNow,
            PedagogicalAlerts: pedagogicalAlerts,
            Columns: columns,
            Rows: rows
        );

        // 7. Lưu vào Cache nếu không có filter động minMastery (TTL: 20 phút)
        if (!request.MinMastery.HasValue)
        {
            await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(20), ct);
        }

        return Result<ClassHeatmapResponseDto>.Success(response);
    }
}
