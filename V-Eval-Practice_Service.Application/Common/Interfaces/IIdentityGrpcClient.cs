using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces;

public interface IIdentityGrpcClient
{
    Task<(bool Exists, string? CampusId, string? CampusName, int TargetScore)> GetStudentProfileAsync(Guid studentId, CancellationToken ct = default);
    Task<List<(Guid StudentId, string Name, string Email, string CampusId, string SchoolName, int TargetScore)>> GetParentStudentsAsync(Guid parentId, CancellationToken ct = default);
    Task<(bool Exists, string FullName, string Phone, string Email, string Specialty)> GetTeacherProfileAsync(Guid teacherId, CancellationToken ct = default);
    Task<System.Collections.Generic.Dictionary<Guid, string>> GetStudentNamesAsync(System.Collections.Generic.IEnumerable<Guid> studentIds, CancellationToken ct = default);
}
