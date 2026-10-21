using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetPrescriptionSession;

public class GetPrescriptionSessionQueryHandler : IRequestHandler<GetPrescriptionSessionQuery, Result<PrescriptionSessionResponseDto>>
{
    private readonly IScorePredictionRepository _predictionRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, 
        PropertyNameCaseInsensitive = true 
    };

    public GetPrescriptionSessionQueryHandler(
        IScorePredictionRepository predictionRepository,
        IContentGrpcClient contentGrpcClient)
    {
        _predictionRepository = predictionRepository;
        _contentGrpcClient = contentGrpcClient;
    }

    public async Task<Result<PrescriptionSessionResponseDto>> Handle(GetPrescriptionSessionQuery request, CancellationToken cancellationToken)
    {
        if (request.StudentId == Guid.Empty)
        {
            return Result<PrescriptionSessionResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh hợp lệ."));
        }

        if (string.IsNullOrWhiteSpace(request.PatternId))
        {
            return Result<PrescriptionSessionResponseDto>.Failure(
                Error.Validation("Prescription.PatternIdRequired", "Mã dạng bài (patternId) không được để trống."));
        }

        var prediction = await _predictionRepository.GetLatestByStudentIdAsync(request.StudentId, cancellationToken);
        if (prediction == null || string.IsNullOrWhiteSpace(prediction.PrescriptionItems))
        {
            return Result<PrescriptionSessionResponseDto>.Failure(
                Error.NotFound("Prediction.NotFound", "Chưa tìm thấy dữ liệu Toa thuốc bù điểm cho học sinh này. Vui lòng hoàn thành bài thi thử chuẩn hóa hoặc kích hoạt tính toán."));
        }

        List<PrescriptionItemDto> prescriptions;
        try
        {
            prescriptions = JsonSerializer.Deserialize<List<PrescriptionItemDto>>(prediction.PrescriptionItems, JsonOptions) ?? new();
        }
        catch
        {
            prescriptions = new List<PrescriptionItemDto>();
        }

        var targetPrescription = prescriptions.FirstOrDefault(p =>
            string.Equals(p.PatternId, request.PatternId, StringComparison.OrdinalIgnoreCase));

        if (targetPrescription == null)
        {
            return Result<PrescriptionSessionResponseDto>.Failure(
                Error.NotFound("Prescription.PatternNotFound", $"Dạng bài '{request.PatternId}' không nằm trong Toa thuốc bù điểm hiện tại của học sinh."));
        }

        // Bốc câu hỏi biến thể từ Content Service
        List<PrescriptionQuestionDto> questionDtos = new();

        try
        {
            var milestoneQuiz = await _contentGrpcClient.GetMilestoneQuizAsync(targetPrescription.SkillId, null, questionCount: 3, cancellationToken);
            if (milestoneQuiz != null && milestoneQuiz.Questions.Count > 0)
            {
                int order = 1;
                foreach (var q in milestoneQuiz.Questions)
                {
                    questionDtos.Add(new PrescriptionQuestionDto
                    {
                        QuestionId = q.QuestionId,
                        QuestionOrder = order++,
                        Content = q.Content,
                        Options = q.Options.Select(o => new PrescriptionOptionDto
                        {
                            OptionId = o.OptionId,
                            Content = o.Content
                        }).ToList(),
                        DifficultyB = q.DifficultyLevel * 0.2
                    });
                }
            }
        }
        catch
        {
            // Fallback an toàn nếu gRPC Content Service bận
        }

        // Nếu Content Service chưa có câu hỏi hoặc đang khởi động, cấp 3 câu hỏi biến thể chuẩn hoá theo dạng bài
        if (questionDtos.Count == 0)
        {
            questionDtos = GenerateFallbackQuestions(targetPrescription);
        }

        var response = new PrescriptionSessionResponseDto
        {
            SessionId = Guid.NewGuid(),
            PatternId = targetPrescription.PatternId,
            PatternName = targetPrescription.PatternName,
            TargetDeltaScore = targetPrescription.DeltaScore,
            SuggestedDurationMinutes = 15,
            TotalQuestions = questionDtos.Count,
            Questions = questionDtos
        };

        return Result<PrescriptionSessionResponseDto>.Success(response);
    }

    private static List<PrescriptionQuestionDto> GenerateFallbackQuestions(PrescriptionItemDto prescription)
    {
        return new List<PrescriptionQuestionDto>
        {
            new()
            {
                QuestionId = Guid.NewGuid(),
                QuestionOrder = 1,
                Content = $"[Biến thể 1 - {prescription.PatternName}] Cho các mệnh đề logic P, Q. Biết rằng mệnh đề P kéo theo Q là đúng và P đúng. Kết luận nào sau đây là bắt buộc phải đúng?",
                Options = new List<PrescriptionOptionDto>
                {
                    new() { OptionId = "A", Content = "Mệnh đề Q phải đúng." },
                    new() { OptionId = "B", Content = "Mệnh đề Q có thể sai." },
                    new() { OptionId = "C", Content = "Cả P và Q đều sai." },
                    new() { OptionId = "D", Content = "Không xác định được giá trị chân lý của Q." }
                },
                DifficultyB = 0.2
            },
            new()
            {
                QuestionId = Guid.NewGuid(),
                QuestionOrder = 2,
                Content = $"[Biến thể 2 - {prescription.PatternName}] Trong điều kiện thực tế, nếu phủ định của mệnh đề điều kiện được thiết lập, hãy chọn nhận định chính xác nhất về tính khả thi của hệ thống:",
                Options = new List<PrescriptionOptionDto>
                {
                    new() { OptionId = "A", Content = "Tiền đề đúng nhưng kết luận sai." },
                    new() { OptionId = "B", Content = "Tiền đề sai và kết luận đúng." },
                    new() { OptionId = "C", Content = "Cả hai vế đều đảo ngược hoàn toàn." },
                    new() { OptionId = "D", Content = "Hệ thống luôn rơi vào nghịch lý tự thân." }
                },
                DifficultyB = 0.4
            },
            new()
            {
                QuestionId = Guid.NewGuid(),
                QuestionOrder = 3,
                Content = $"[Biến thể 3 - {prescription.PatternName}] Ứng dụng quy nạp logic vào việc suy diễn tập nghiệm, kết quả suy luận hợp lệ cuối cùng đạt được là:",
                Options = new List<PrescriptionOptionDto>
                {
                    new() { OptionId = "A", Content = "Tập nghiệm là giao của các điều kiện ràng buộc độc lập." },
                    new() { OptionId = "B", Content = "Tập nghiệm là hợp của các phủ định rời rạc." },
                    new() { OptionId = "C", Content = "Tập nghiệm luôn là tập rỗng." },
                    new() { OptionId = "D", Content = "Mọi phần tử bất kỳ đều thỏa mãn điều kiện biên." }
                },
                DifficultyB = 0.5
            }
        };
    }
}
