using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.TriggerHeatmapLiveSession;

public class TriggerHeatmapLiveSessionCommandHandler : IRequestHandler<TriggerHeatmapLiveSessionCommand, Result<TriggerLiveSessionResponseDto>>
{
    private readonly IClassHeatmapRepository _heatmapRepo;
    private readonly ILearningRoadmapRepository _roadmapRepo;
    private readonly ILogger<TriggerHeatmapLiveSessionCommandHandler> _logger;

    public TriggerHeatmapLiveSessionCommandHandler(
        IClassHeatmapRepository heatmapRepo,
        ILearningRoadmapRepository roadmapRepo,
        ILogger<TriggerHeatmapLiveSessionCommandHandler> logger)
    {
        _heatmapRepo = heatmapRepo;
        _roadmapRepo = roadmapRepo;
        _logger = logger;
    }

    public async Task<Result<TriggerLiveSessionResponseDto>> Handle(TriggerHeatmapLiveSessionCommand request, CancellationToken ct)
    {
        // 1. Kiểm tra lớp học tồn tại
        var classEntity = await _heatmapRepo.GetClassByIdAsync(request.ClassId, ct);
        if (classEntity == null)
        {
            return Result<TriggerLiveSessionResponseDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học với ID {request.ClassId}."));
        }

        _logger.LogInformation("[Live Action] Giảng viên kích hoạt 1-Click Live Session cứu trợ cho Lớp {ClassId}, Kỹ năng {SkillId}.",
            request.ClassId, request.SkillId);

        // 2. Xác định danh sách học sinh đang bị hổng kiến thức ở kỹ năng này (P(Lt) < 0.50)
        var heatmaps = await _heatmapRepo.GetHeatmapsByClassIdAsync(request.ClassId, null, ct);
        var strugglingStudentIds = heatmaps
            .Where(h => h.SkillId == request.SkillId && h.PltValue < 0.50)
            .Select(h => h.StudentId)
            .Distinct()
            .ToList();

        // Nếu chưa có log điểm yếu riêng lẻ, lấy toàn bộ học sinh đang ghi danh trong lớp
        if (!strugglingStudentIds.Any())
        {
            var enrollments = await _heatmapRepo.GetClassEnrollmentsAsync(request.ClassId, ct);
            strugglingStudentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        }

        // 3. Khởi tạo bản ghi LiveSession
        var liveSession = new LiveSession
        {
            SessionId = Guid.NewGuid(),
            ClassId = request.ClassId,
            TeacherId = classEntity.TeacherId,
            Title = request.SessionTitle,
            Description = $"[1-Click Heatmap Action] Buổi Live Q&A chuyên đề cứu trợ cho kỹ năng {request.SkillId}.",
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : 45,
            MeetingUrl = string.IsNullOrWhiteSpace(request.MeetingUrl) ? "https://meet.google.com/xyz-veval-live" : request.MeetingUrl,
            Status = "SCHEDULED",
            CreatedAt = DateTime.UtcNow
        };

        await _roadmapRepo.AddLiveSessionAsync(liveSession, ct);

        // 4. Tự động liên kết LiveSession vào các chặng học (RoadmapNodes) của các học sinh thuộc diện can thiệp
        if (strugglingStudentIds.Any())
        {
            var targetNodes = await _roadmapRepo.GetNodesBySkillAndStudentIdsAsync(request.SkillId, strugglingStudentIds, ct);
            foreach (var node in targetNodes)
            {
                node.LiveSessionId = liveSession.SessionId;
            }
        }

        await _roadmapRepo.SaveChangesAsync(ct);

        _logger.LogInformation("[Live Action] Đã tạo thành công LiveSession {SessionId} và liên kết với {Count} học sinh.",
            liveSession.SessionId, strugglingStudentIds.Count);

        var response = new TriggerLiveSessionResponseDto(
            SessionId: liveSession.SessionId,
            ClassId: request.ClassId,
            Title: liveSession.Title,
            ScheduledAt: liveSession.ScheduledAt,
            TargetedStudentsCount: strugglingStudentIds.Count,
            Status: "SCHEDULED",
            Message: $"Đã tạo buổi Live Q&A thành công và tự động cập nhật lịch học của {strugglingStudentIds.Count} học sinh có P(Lt) < 0.50."
        );

        return Result<TriggerLiveSessionResponseDto>.Success(response);
    }
}
