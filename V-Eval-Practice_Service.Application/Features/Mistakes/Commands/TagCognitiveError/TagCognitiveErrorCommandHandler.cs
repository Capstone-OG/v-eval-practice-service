using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Commands.TagCognitiveError;

public class TagCognitiveErrorCommandHandler : IRequestHandler<TagCognitiveErrorCommand, Result<TagCognitiveErrorResponseDto>>
{
    private readonly IMistakeNotebookRepository _mistakeNotebookRepository;
    private readonly ILogger<TagCognitiveErrorCommandHandler> _logger;

    public TagCognitiveErrorCommandHandler(
        IMistakeNotebookRepository mistakeNotebookRepository,
        ILogger<TagCognitiveErrorCommandHandler> logger)
    {
        _mistakeNotebookRepository = mistakeNotebookRepository;
        _logger = logger;
    }

    public async Task<Result<TagCognitiveErrorResponseDto>> Handle(TagCognitiveErrorCommand request, CancellationToken ct)
    {
        var item = await _mistakeNotebookRepository.GetByIdAsync(request.MistakeNotebookId, ct);
        if (item == null)
        {
            return Result<TagCognitiveErrorResponseDto>.Failure(
                Error.NotFound("MistakeNotebook.NotFound", $"Không tìm thấy bản ghi Sổ tay lỗi sai {request.MistakeNotebookId}"));
        }

        string tag = request.Request.CognitiveErrorTag.ToUpperInvariant();
        item.CognitiveErrorTag = tag;
        item.StudentNotes = request.Request.StudentNotes;
        item.UpdatedAt = DateTime.UtcNow;

        await _mistakeNotebookRepository.UpdateAsync(item, ct);

        _logger.LogInformation("Học sinh {StudentId} gắn nhãn lỗi nhận thức [{Tag}] cho câu hỏi {QuestionId}",
            item.StudentId, tag, item.QuestionId);

        string message = tag switch
        {
            "CARELESS" => "Đã ghi nhận nhãn 'Tính ẩu'. Hãy rèn luyện sự cẩn trọng và kiểm tra lại các bước tính toán trung gian.",
            "MISREAD_QUESTION" => "Đã ghi nhận nhãn 'Đọc nhầm đề'. Hãy gạch chân từ khóa quan trọng và đọc kỹ giả thiết bài toán.",
            "MISSING_CONCEPT" => "Đã ghi nhận nhãn 'Hổng lý thuyết'. Hãy xem lại bài giảng và nắm vững công thức nền tảng của dạng bài.",
            _ => "Đã cập nhật nhãn nhận thức thành công."
        };

        var response = new TagCognitiveErrorResponseDto(
            Id: item.Id,
            CognitiveErrorTag: tag,
            StudentNotes: item.StudentNotes,
            Message: message
        );

        return Result<TagCognitiveErrorResponseDto>.Success(response);
    }
}
