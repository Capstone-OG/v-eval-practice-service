using System;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces;

public interface IIdentityGrpcClient
{
    Task<(bool Exists, string? CampusId, int TargetScore)> GetStudentProfileAsync(Guid studentId, CancellationToken ct = default);
}
