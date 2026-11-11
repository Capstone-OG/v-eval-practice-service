using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetRoadmapNodeDetail;

/// <summary>
/// Query lấy thông tin chi tiết của 1 chặng học (Core Flow 2 - API 3)
/// </summary>
public record GetRoadmapNodeDetailQuery(Guid NodeId, Guid? StudentId = null) : IRequest<Result<RoadmapNodeDetailDto>>;
