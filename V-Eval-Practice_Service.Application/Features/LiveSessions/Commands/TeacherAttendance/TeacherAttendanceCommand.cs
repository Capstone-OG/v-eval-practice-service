using System;
using System.Collections.Generic;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.TeacherAttendance;

public record TeacherAttendanceCommand(
    Guid SessionId,
    List<StudentAttendanceItemDto> Items,
    Guid? GradedBy
) : IRequest<Result<TeacherAttendanceResponseDto>>;
