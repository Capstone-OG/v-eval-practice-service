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
/// Query lấy gói cứu trợ phụ đạo (Remedial Node - BR-03) gồm tóm tắt công thức, video recap và 3 câu hỏi cơ bản
/// Tự động xoay vòng đề biến thể theo đợt nếu học sinh chưa đạt yêu cầu (tránh học vẹt nhớ đáp án cũ)
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

        // 0. Xác định các câu hỏi cứu trợ đã từng làm và số lần học sinh đã thử sức gói cứu trợ
        var attemptedQuestionIds = progress.AdaptiveAttempts?
            .Select(a => a.QuestionId)
            .ToHashSet() ?? new HashSet<Guid>();

        int remedialAttemptCount = progress.AdaptiveAttempts?
            .Count(a => a.PatternId != null && a.PatternId.StartsWith("REMEDIAL")) / 3 ?? 0;

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
                // Yêu cầu ngân hàng đề cấp nhiều câu hỏi để lọc bỏ những câu đã từng làm
                var quizResult = await _contentGrpcClient.GetMilestoneQuizAsync(skillId, node?.QuizExamId, questionCount: 20, ct);
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

                    // Ưu tiên chọn 3 câu hỏi cơ bản CHƯA TỪNG LÀM (DifficultyLevel thấp nhất: Nhận biết / Thông hiểu)
                    var candidateQuestions = quizResult.Questions
                        .Where(q => !attemptedQuestionIds.Contains(q.QuestionId))
                        .OrderBy(q => q.DifficultyLevel)
                        .Take(3)
                        .ToList();

                    // Nếu ngân hàng đề ít câu hơn và đã làm hết, lấy câu ít xuất hiện nhất
                    if (candidateQuestions.Count < 3)
                    {
                        var additional = quizResult.Questions
                            .Where(q => !candidateQuestions.Any(c => c.QuestionId == q.QuestionId))
                            .OrderBy(q => q.DifficultyLevel)
                            .Take(3 - candidateQuestions.Count)
                            .ToList();
                        candidateQuestions.AddRange(additional);
                    }

                    int order = 1;
                    foreach (var q in candidateQuestions)
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

                    _logger.LogInformation("Đã lấy thành công {Count} câu hỏi cứu trợ biến thể từ Ngân hàng đề thuộc kỹ năng {SkillName} ({SkillId}), đợt thử {Attempt}",
                        remedialQuestions.Count, skillName, skillId, remedialAttemptCount + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể kết nối Content Service qua gRPC để lấy câu hỏi cứu trợ. Sử dụng câu hỏi cứu trợ fallback.");
            }
        }

        // 3. Cơ chế Fallback an toàn nếu Ngân hàng đề tạm ngắt kết nối: Cấp bộ câu hỏi biến thể xoay vòng theo đợt
        if (remedialQuestions.Count == 0)
        {
            remedialQuestions.AddRange(GetDefaultRemedialQuestions(skillId, skillName, remedialAttemptCount));
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
            Title: $"Gói Can Thiệp Cứu Trợ Phụ Đạo - {skillName} (Đợt {remedialAttemptCount + 1})",
            SummaryFormula: summaryFormula,
            RemedialVideoUrl: remedialVideoUrl,
            Instruction: $"Học sinh vui lòng đọc kỹ tóm tắt phương pháp của chuyên đề '{skillName}' và hoàn thành 3 câu hỏi cơ bản dưới đây để lấy lại phản xạ căn bản trước khi quay lại luyện tập thích ứng.",
            Questions: remedialQuestions
        );

        return Result<RemedialPackageResponseDto>.Success(response);
    }

    /// <summary>
    /// Bộ câu hỏi cứu trợ Fallback đa dạng theo từng đợt thử (Đợt 1 -> Đợt 2 -> Đợt 3) để tránh học vẹt
    /// </summary>
    private static List<RemedialQuestionDto> GetDefaultRemedialQuestions(Guid skillId, string skillName, int attemptIndex)
    {
        int setIndex = attemptIndex % 3;

        return setIndex switch
        {
            // BỘ ĐỀ BIẾN THỂ ĐỢT 1
            0 => new List<RemedialQuestionDto>
            {
                new(
                    Guid.Parse("11111111-0000-0000-0000-000000000001"),
                    1,
                    $"[Đợt 1] Câu hỏi củng cố 1: Khái niệm cơ bản và định nghĩa nền tảng của dạng bài '{skillName}' là gì?",
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
                    $"[Đợt 1] Câu hỏi củng cố 2: Bước đầu tiên quan trọng nhất khi giải quyết dạng bài '{skillName}' là:",
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
                    $"[Đợt 1] Câu hỏi củng cố 3: Khi đối chiếu kết quả trong chuyên đề '{skillName}', học sinh cần lưu ý điều gì?",
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
            },

            // BỘ ĐỀ BIẾN THỂ ĐỢT 2 (KHI LÀM SAI ĐỢT 1 - ĐỀ HOÀN TOÀN MỚI, ĐÁP ÁN B, C, D)
            1 => new List<RemedialQuestionDto>
            {
                new(
                    Guid.Parse("22222222-0000-0000-0000-000000000001"),
                    1,
                    $"[Đợt 2 - Biến thể] Câu hỏi củng cố 1: Để nhận diện dấu hiệu áp dụng phương pháp trong dạng bài '{skillName}', ta cần quan sát đặc điểm nào?",
                    new List<RemedialOptionDto>
                    {
                        new("A", "Dấu hiệu hình thức bề ngoài không liên quan"),
                        new("B", "Cấu trúc giả thiết và mối quan hệ giữa các đại lượng đã cho"),
                        new("C", "Phương án có chứa số lớn nhất"),
                        new("D", "Số lượng chữ số trong câu hỏi")
                    },
                    DifficultyB: -1.1,
                    CognitiveLevel: "Nhận biết cơ bản"
                ),
                new(
                    Guid.Parse("22222222-0000-0000-0000-000000000002"),
                    2,
                    $"[Đợt 2 - Biến thể] Câu hỏi củng cố 2: Lỗi sai phổ biến học sinh hay mắc phải khi làm bài '{skillName}' là gì?",
                    new List<RemedialOptionDto>
                    {
                        new("A", "Trình bày quá chi tiết từng bước"),
                        new("B", "Đọc kỹ đề và vẽ sơ đồ tóm tắt"),
                        new("C", "Quên kiểm tra điều kiện xác định và áp dụng sai công thức biến đổi"),
                        new("D", "Thử lại kết quả bằng giá trị cụ thể")
                    },
                    DifficultyB: -0.7,
                    CognitiveLevel: "Thông hiểu nền tảng"
                ),
                new(
                    Guid.Parse("22222222-0000-0000-0000-000000000003"),
                    3,
                    $"[Đợt 2 - Biến thể] Câu hỏi củng cố 3: Sau khi tìm ra kết quả trung gian, thao tác nào dưới đây giúp bảo đảm tính chính xác tuyệt đối?",
                    new List<RemedialOptionDto>
                    {
                        new("A", "Chọn ngay đáp án mà không đối chiếu lại yêu cầu bài toán"),
                        new("B", "Loại trừ ngay các phương án có chứa phân số"),
                        new("C", "Bỏ qua giả thiết phụ đã cho trong đề"),
                        new("D", "Thay thế ngược lại vào biểu thức ban đầu để kiểm chứng tính đúng đắn của nghiệm")
                    },
                    DifficultyB: -0.4,
                    CognitiveLevel: "Thông hiểu nền tảng"
                )
            },

            // BỘ ĐỀ BIẾN THỂ ĐỢT 3 (KHI LÀM SAI ĐỢT 2 - ĐỀ HOÀN TOÀN MỚI, ĐÁP ÁN C, B, A)
            _ => new List<RemedialQuestionDto>
            {
                new(
                    Guid.Parse("33333333-0000-0000-0000-000000000001"),
                    1,
                    $"[Đợt 3 - Biến thể] Câu hỏi củng cố 1: Bước chuẩn hóa quan trọng nhất trước khi áp dụng công thức của '{skillName}' là:",
                    new List<RemedialOptionDto>
                    {
                        new("A", "Bỏ qua mọi phép biến đổi tương đương"),
                        new("B", "Đoán trước kết quả mà không cần lập luận"),
                        new("C", "Đưa bài toán về dạng thức chuẩn tắc quen thuộc và đồng nhất biến số"),
                        new("D", "Chọn phương án ngẫu nhiên")
                    },
                    DifficultyB: -1.0,
                    CognitiveLevel: "Nhận biết cơ bản"
                ),
                new(
                    Guid.Parse("33333333-0000-0000-0000-000000000002"),
                    2,
                    $"[Đợt 3 - Biến thể] Câu hỏi củng cố 2: Chiến lược nào giúp loại bỏ nhanh các phương án gây nhiễu trong dạng bài '{skillName}'?",
                    new List<RemedialOptionDto>
                    {
                        new("A", "Luôn chọn phương án dài nhất"),
                        new("B", "Đánh giá tính chẵn lẻ, miền giá trị hoặc xét trường hợp biên đặc biệt"),
                        new("C", "Chỉ chọn phương án không chứa dấu trừ"),
                        new("D", "Loại bỏ ngẫu nhiên 2 phương án đầu tiên")
                    },
                    DifficultyB: -0.6,
                    CognitiveLevel: "Thông hiểu nền tảng"
                ),
                new(
                    Guid.Parse("33333333-0000-0000-0000-000000000003"),
                    3,
                    $"[Đợt 3 - Biến thể] Câu hỏi củng cố 3: Bài học kinh nghiệm cốt lõi để không bao giờ mất điểm ở dạng bài '{skillName}' là gì?",
                    new List<RemedialOptionDto>
                    {
                        new("A", "Rèn luyện tính cẩn thận, nắm chắc bản chất lý thuyết và kiểm tra điều kiện nghiệm"),
                        new("B", "Làm thật nhanh để dành thời gian cho câu khác mà không cần kiểm tra"),
                        new("C", "Học vẹt đáp án trắc nghiệm của các đề trước"),
                        new("D", "Chỉ làm bài khi có máy tính cầm tay hỗ trợ")
                    },
                    DifficultyB: -0.4,
                    CognitiveLevel: "Thông hiểu nền tảng"
                )
            }
        };
    }
}
