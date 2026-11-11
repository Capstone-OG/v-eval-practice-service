using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Queries.GetMockExamDetail;

public record GetMockExamDetailQuery(Guid SubmissionId) : IRequest<Result<GetMockExamDetailResponseDto>>;
