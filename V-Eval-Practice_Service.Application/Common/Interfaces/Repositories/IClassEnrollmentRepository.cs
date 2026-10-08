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
        string campusName = "",
        CancellationToken ct = default);

    Task<ClassEnrollment?> GetEnrollmentByStudentIdAsync(
        Guid studentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetEnrolledStudentIdsByCampusIdAsync(
        Guid campusId,
        CancellationToken ct = default);

    Task<Class> CreateThematicClassWithEnrollmentsAsync(
        Guid campusId,
        string name,
        Guid? domainId,
        string? domainCode,
        int clusterIndex,
        IEnumerable<Guid> studentIds,
        CancellationToken ct = default);

    Task<Class?> GetClassByIdAsync(
        Guid classId,
        CancellationToken ct = default);

    Task<int> SaveChangesAsync(
        CancellationToken ct = default);
}
