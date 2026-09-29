using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetMilestoneQuiz;

/// <summary>
/// Query lấy đề thi Quiz củng cố của chặng học (Core Flow 2 - API 5)
/// </summary>
/// <param name="NodeId">Mã định danh chặng học (Milestone / RoadmapNode)</param>
/// <param name="StudentId">Mã định danh học sinh (xác thực quyền sở hữu)</param>
public record GetMilestoneQuizQuery(
    Guid NodeId,
    Guid? StudentId = null
) : IRequest<Result<MilestoneQuizDto>>;
