using System;
using System.Collections.Generic;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.SubmitMilestoneQuiz;

public record SubmitMilestoneQuizCommand(
    Guid NodeId,
    Guid? StudentId,
    List<MilestoneQuizAnswerItemDto> Answers,
    int TotalTimeSpentSeconds
) : IRequest<Result<SubmitMilestoneQuizResponseDto>>;
