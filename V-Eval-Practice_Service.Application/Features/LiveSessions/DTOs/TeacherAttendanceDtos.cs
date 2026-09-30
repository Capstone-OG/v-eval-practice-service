using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

public class StudentAttendanceItemDto
{
    public Guid StudentId { get; set; }
    public string AttendanceStatus { get; set; } = "ATTENDED";
}

public class TeacherAttendanceRequestDto
{
    public List<StudentAttendanceItemDto> Items { get; set; } = new();
}

public class TeacherAttendanceResponseDto
{
    public Guid SessionId { get; set; }
    public int TotalGraded { get; set; }
    public int TotalAttended { get; set; }
    public int TotalAbsent { get; set; }
    public DateTime GradedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
