using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Queries.GetTeacherSchedule;

public record GetTeacherScheduleQuery(
    Guid TeacherId
) : IRequest<Result<TeacherScheduleDto>>;
