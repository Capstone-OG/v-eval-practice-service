using System;

namespace V_Eval_Practice_Service.Application.Features.Classes.DTOs;

public class AssignTeacherRequestDto
{
    public Guid TeacherId { get; set; }
    public Guid? TeacherCampusId { get; set; }
    public Guid? ManagerCampusId { get; set; }
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

public class EnrollOrTransferStudentRequestDto
{
    public Guid StudentId { get; set; }
    public Guid? ManagerCampusId { get; set; }
}

public class EnrollOrTransferStudentResponseDto
{
    public Guid RequestedClassId { get; set; }
    public Guid FinalClassId { get; set; }
    public string FinalClassName { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public int CurrentEnrolledCount { get; set; }
    public int MaxCapacity { get; set; } = 20;
    public bool IsFull { get; set; }
    public bool OverflowedToNewCohort { get; set; }
    public string Message { get; set; } = string.Empty;
}
