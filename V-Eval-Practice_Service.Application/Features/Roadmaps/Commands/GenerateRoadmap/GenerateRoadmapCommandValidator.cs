using System;
using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;

/// <summary>
/// Validator kiểm định tham số đầu vào cho GenerateRoadmapCommand
/// </summary>
public class GenerateRoadmapCommandValidator : AbstractValidator<GenerateRoadmapCommand>
{
    public GenerateRoadmapCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.DiagnosticSubmissionId)
            .NotEmpty().WithMessage("DiagnosticSubmissionId không được để trống.");

        RuleFor(x => x.ExamDate)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Ngày thi chính thức (ExamDate) phải ở tương lai.");

        RuleFor(x => x.StudyHoursPerDay)
            .InclusiveBetween(0.5, 12.0)
            .WithMessage("Quỹ thời gian tự học mỗi ngày (StudyHoursPerDay) phải nằm trong khoảng từ 0.5 đến 12.0 giờ.");
    }
}
