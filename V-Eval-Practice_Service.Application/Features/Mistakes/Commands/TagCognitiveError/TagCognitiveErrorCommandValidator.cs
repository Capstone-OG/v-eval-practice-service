using System;
using System.Linq;
using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Commands.TagCognitiveError;

public class TagCognitiveErrorCommandValidator : AbstractValidator<TagCognitiveErrorCommand>
{
    private static readonly string[] ValidTags = { "CARELESS", "MISREAD_QUESTION", "MISSING_CONCEPT" };

    public TagCognitiveErrorCommandValidator()
    {
        RuleFor(x => x.MistakeNotebookId)
            .NotEmpty().WithMessage("Mã bản ghi Sổ tay lỗi sai không được để trống.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Dữ liệu gắn nhãn lỗi nhận thức không được null.");

        RuleFor(x => x.Request.CognitiveErrorTag)
            .NotEmpty().WithMessage("Nhãn nhận thức không được để trống.")
            .Must(tag => ValidTags.Contains(tag?.ToUpperInvariant()))
            .WithMessage("Nhãn nhận thức không hợp lệ. Chỉ chấp nhận: CARELESS, MISREAD_QUESTION, MISSING_CONCEPT.");
    }
}
