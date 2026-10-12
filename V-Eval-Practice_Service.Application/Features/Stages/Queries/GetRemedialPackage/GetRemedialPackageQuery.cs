using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Queries.GetRemedialPackage;

/// <summary>
/// Query lấy gói cứu trợ phụ đạo (Remedial Node) khi bị kích hoạt BR-03
/// </summary>
public record GetRemedialPackageQuery(Guid StageProgressId) : IRequest<Result<RemedialPackageResponseDto>>;
