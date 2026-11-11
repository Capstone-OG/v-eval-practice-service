using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetMyRoadmap;

/// <summary>
/// Query tra cứu lộ trình học tập cá nhân hóa đang kích hoạt của học sinh (Core Flow 2 - API 2)
/// </summary>
public record GetMyRoadmapQuery(Guid StudentId) : IRequest<Result<RoadmapTimelineDto>>;
