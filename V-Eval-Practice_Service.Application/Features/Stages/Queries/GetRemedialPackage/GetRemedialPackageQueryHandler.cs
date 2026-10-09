using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Queries.GetRemedialPackage;

/// <summary>
/// Handler xử lý lấy gói cứu trợ phụ đạo (Remedial Node) linh hoạt 100% theo Dạng bài/Ngân hàng đề
/// </summary>
public class GetRemedialPackageQueryHandler : IRequestHandler<GetRemedialPackageQuery, Result<RemedialPackageResponseDto>>
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<GetRemedialPackageQueryHandler> _logger;

    public GetRemedialPackageQueryHandler(
        IStageProgressRepository stageProgressRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<GetRemedialPackageQueryHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<RemedialPackageResponseDto>> Handle(GetRemedialPackageQuery request, CancellationToken ct)
    {
        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<RemedialPackageResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        var node = progress.RoadmapNode;
        Guid skillId = node?.SkillId ?? Guid.Empty;

        var remedialQuestions = new List<RemedialQuestionDto>();
        string skillName = "Chuyên đề trọng tâm";
        string skillDescription = string.Empty;

        // 1. Tra cứu thông tin chi tiết của Dạng bài / Kỹ năng từ Content Service qua gRPC
        if (skillId != Guid.Empty)
        {
            try
            {
                var skillTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
                var matchedSkill = skillTree.FirstOrDefault(s => s.SkillId == skillId);
                if (matchedSkill != null)
                {
                    skillName = matchedSkill.Name;
                    skillDescription = matchedSkill.Description;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải SkillTree từ Content Service qua gRPC.");
            }

            // 2. Truy vấn câu hỏi cơ bản từ Ngân hàng đề thuộc đúng Dạng bài này
            try
            {
                var quizResult = await _contentGrpcClient.GetMilestoneQuizAsync(skillId, node?.QuizExamId, questionCount: 5, ct);
                if (quizResult != null && quizResult.Questions.Count > 0)
                {
                    if (string.IsNullOrEmpty(skillDescription))
                    {
                        var firstQ = quizResult.Questions.FirstOrDefault(q => !string.IsNullOrEmpty(q.SkillName));
                        if (firstQ != null)
                        {
                            skillName = firstQ.SkillName;
                        }
                    }

                    // Ưu tiên chọn 3 câu hỏi cơ bản (DifficultyLevel thấp nhất: Nhận biết / Thông hiểu)
                    var sortedQuestions = quizResult.Questions
                        .OrderBy(q => q.DifficultyLevel)
                        .Take(3)
                        .ToList();

                    int order = 1;
                    foreach (var q in sortedQuestions)
                    {
                        double difficultyB = q.DifficultyLevel == 1 ? -1.2 : -0.6;
                        string cognitiveLevel = q.DifficultyLevel == 1 ? "Nhận biết cơ bản" : "Thông hiểu nền tảng";

                        remedialQuestions.Add(new RemedialQuestionDto(
                            q.QuestionId,
                            order++,
                            q.Content,
                            q.Options.Select(o => new RemedialOptionDto(o.OptionId, o.Content)).ToList(),
                            difficultyB,
                            cognitiveLevel
                        ));
                    }

                    _logger.LogInformation("Đã lấy thành công {Count} câu hỏi cứu trợ từ Ngân hàng đề thuộc kỹ năng {SkillName} ({SkillId})",
                        remedialQuestions.Count, skillName, skillId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể kết nối Content Service qua gRPC để lấy câu hỏi cứu trợ. Sử dụng câu hỏi cứu trợ fallback.");
            }
        }

        // 3. Cơ chế Fallback an toàn nếu Ngân hàng đề tạm ngắt kết nối
        if (remedialQuestions.Count == 0)
        {
            remedialQuestions.AddRange(GetDefaultRemedialQuestions(skillId, skillName));
        }

        // 4. Sinh tóm tắt lý thuyết & phương pháp động theo đúng Dạng bài/Kỹ năng
        string summaryFormula = !string.IsNullOrWhiteSpace(skillDescription)
            ? $"TỔNG HỢP KIẾN THỨC & PHƯƠNG PHÁP - [{skillName.ToUpperInvariant()}]:\n{skillDescription}\n\n* Hướng dẫn: Đọc kỹ câu hỏi, nhận diện đúng yêu cầu cốt lõi và kiểm tra lại phương án trước khi chọn."
            : $"TỔNG HỢP KIẾN THỨC & PHƯƠNG PHÁP - [{skillName.ToUpperInvariant()}]:\n1. Nắm chắc định nghĩa và tính chất cơ sở của {skillName}.\n2. Áp dụng quy tắc biến đổi tuần tự theo phương pháp chuẩn.\n3. Đọc kỹ đề bài và loại trừ các phương án gây nhiễu.";

        string remedialVideoUrl = "https://storage.v-eval.edu.vn/videos/remedial/quick-concept-recap.mp4";

        var response = new RemedialPackageResponseDto(
            progress.Id,
            progress.RoadmapNodeId,
            skillId,
            progress.CurrentStep,
            progress.Status,
            progress.ConsecutiveIncorrect,
            Title: $"Gói Can Thiệp Cứu Trợ Phụ Đạo - {skillName}",
            SummaryFormula: summaryFormula,
            RemedialVideoUrl: remedialVideoUrl,
            Instruction: $"Học sinh vui lòng đọc kỹ tóm tắt phương pháp của chuyên đề '{skillName}' và hoàn thành 3 câu hỏi cơ bản dưới đây để lấy lại phản xạ căn bản trước khi quay lại luyện tập thích ứng.",
            Questions: remedialQuestions
        );

        return Result<RemedialPackageResponseDto>.Success(response);
    }

    private static List<RemedialQuestionDto> GetDefaultRemedialQuestions(Guid skillId, string skillName)
    {
        return new List<RemedialQuestionDto>
        {
            new(
                Guid.Parse("11111111-0000-0000-0000-000000000001"),
                1,
                $"Câu hỏi củng cố 1: Khái niệm cơ bản và định nghĩa nền tảng của dạng bài '{skillName}' là gì?",
                new List<RemedialOptionDto>
                {
                    new("A", "Định nghĩa và tính chất cốt lõi của dạng bài"),
                    new("B", "Phương pháp mở rộng nâng cao"),
                    new("C", "Trường hợp ngoại lệ đặc biệt"),
                    new("D", "Dạng biến thể phức tạp")
                },
                DifficultyB: -1.2,
                CognitiveLevel: "Nhận biết cơ bản"
            ),
            new(
                Guid.Parse("11111111-0000-0000-0000-000000000002"),
                2,
                $"Câu hỏi củng cố 2: Bước đầu tiên quan trọng nhất khi giải quyết dạng bài '{skillName}' là:",
                new List<RemedialOptionDto>
                {
                    new("A", "Xác định rõ giả thiết, yêu cầu và điều kiện bài toán"),
                    new("B", "Tính toán ngay lập tức không cần kiểm tra điều kiện"),
                    new("C", "Đoán mò phương án có giá trị lớn nhất"),
                    new("D", "Bỏ qua các bước biến đổi trung gian")
                },
                DifficultyB: -0.8,
                CognitiveLevel: "Thông hiểu nền tảng"
            ),
            new(
                Guid.Parse("11111111-0000-0000-0000-000000000003"),
                3,
                $"Câu hỏi củng cố 3: Khi đối chiếu kết quả trong chuyên đề '{skillName}', học sinh cần lưu ý điều gì?",
                new List<RemedialOptionDto>
                {
                    new("A", "Kiểm tra tính hợp lý của kết quả so với điều kiện xác định ban đầu"),
                    new("B", "Chỉ chọn đáp án đầu tiên tìm thấy"),
                    new("C", "Không cần xem lại đơn vị đo lường và dấu biểu thức"),
                    new("D", "Bỏ qua bước so sánh giả thiết")
                },
                DifficultyB: -0.5,
                CognitiveLevel: "Thông hiểu nền tảng"
            )
        };
    }
}
