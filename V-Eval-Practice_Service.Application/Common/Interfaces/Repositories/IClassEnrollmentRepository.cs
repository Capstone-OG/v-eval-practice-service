using System;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

public interface IClassEnrollmentRepository
{
    Task<(Guid ClassId, string ClassName, Guid EnrollmentId)> EnrollStudentAsync(
        Guid studentId,
        string campusId,
        string placementClass,
        Guid diagnosticSubmissionId,
        CancellationToken ct = default);

    Task<ClassEnrollment?> GetEnrollmentByStudentIdAsync(
        Guid studentId,
        CancellationToken ct = default);
}
