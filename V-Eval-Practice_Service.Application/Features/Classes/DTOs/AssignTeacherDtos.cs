using System;

namespace V_Eval_Practice_Service.Application.Features.Classes.DTOs;

public class AssignTeacherRequestDto
{
    public Guid TeacherId { get; set; }
}

public class AssignTeacherResponseDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid CampusId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid? AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
