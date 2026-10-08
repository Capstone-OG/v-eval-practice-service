using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure.Persistence;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class ClassEnrollmentRepository : IClassEnrollmentRepository
{
    private readonly PracticeDbContext _context;
    private readonly ILogger<ClassEnrollmentRepository> _logger;

    public ClassEnrollmentRepository(PracticeDbContext context, ILogger<ClassEnrollmentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(Guid ClassId, string ClassName, Guid EnrollmentId)> EnrollStudentAsync(
        Guid studentId,
        string campusId,
        string placementClass,
        Guid diagnosticSubmissionId,
        string campusName = "",
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(campusId, out var campusGuid))
        {
            // If campusId is not a UUID string, generate a deterministic GUID from the campus string
            campusGuid = Guid.NewGuid();
        }

        string tierKeyword = placementClass.ToUpperInvariant() switch
        {
            "FOUNDATION" => "Nền tảng",
            "BREAKTHROUGH" => "Bứt phá",
            _ => "Tăng tốc"
        };

        string fullTierName = placementClass.ToUpperInvariant() switch
        {
            "FOUNDATION" => "Lớp Nền tảng (Foundation)",
            "BREAKTHROUGH" => "Lớp Bứt phá (Breakthrough)",
            _ => "Lớp Tăng tốc (Acceleration)"
        };

        string campusDisplay = !string.IsNullOrWhiteSpace(campusName)
            ? campusName
            : $"Cơ sở {campusId}";

        // 1. Find or create matching class at this campus
        var targetClass = await _context.Classes
            .FirstOrDefaultAsync(c => c.CampusId == campusGuid &&
                                      c.Status == "ACTIVE" &&
                                      c.Name.Contains(tierKeyword), ct);

        if (targetClass == null)
        {
            targetClass = new Class
            {
                ClassId = Guid.NewGuid(),
                CampusId = campusGuid,
                Name = $"{fullTierName} - {campusDisplay}",
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };
            await _context.Classes.AddAsync(targetClass, ct);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Created new placement class {ClassName} ({ClassId}) for Campus {CampusId}",
                targetClass.Name, targetClass.ClassId, campusId);
        }

        // 2. Check if student already has an enrollment
        var existingEnrollment = await _context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId, ct);

        Guid enrollmentId;
        if (existingEnrollment != null)
        {
            existingEnrollment.ClassId = targetClass.ClassId;
            existingEnrollment.DiagnosticSubmissionId = diagnosticSubmissionId;
            existingEnrollment.Status = "ENROLLED";
            existingEnrollment.EnrolledAt = DateTime.UtcNow;
            enrollmentId = existingEnrollment.EnrollmentId;
            _logger.LogInformation("Updated enrollment for student {StudentId} to class {ClassId}",
                studentId, targetClass.ClassId);
        }
        else
        {
            var newEnrollment = new ClassEnrollment
            {
                EnrollmentId = Guid.NewGuid(),
                ClassId = targetClass.ClassId,
                StudentId = studentId,
                DiagnosticSubmissionId = diagnosticSubmissionId,
                Status = "ENROLLED",
                EnrolledAt = DateTime.UtcNow
            };
            await _context.ClassEnrollments.AddAsync(newEnrollment, ct);
            enrollmentId = newEnrollment.EnrollmentId;
            _logger.LogInformation("Created new enrollment {EnrollmentId} for student {StudentId} in class {ClassId}",
                enrollmentId, studentId, targetClass.ClassId);
        }

        await _context.SaveChangesAsync(ct);
        return (targetClass.ClassId, targetClass.Name, enrollmentId);
    }

    public async Task<ClassEnrollment?> GetEnrollmentByStudentIdAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        return await _context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.StudentId == studentId, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetEnrolledStudentIdsByCampusIdAsync(
        Guid campusId,
        CancellationToken ct = default)
    {
        return await _context.ClassEnrollments
            .Where(e => e.Class != null && e.Class.CampusId == campusId && e.Status == "ENROLLED")
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<Class> CreateThematicClassWithEnrollmentsAsync(
        Guid campusId,
        string name,
        Guid? domainId,
        string? domainCode,
        int clusterIndex,
        IEnumerable<Guid> studentIds,
        CancellationToken ct = default)
    {
        var thematicClass = new Class
        {
            ClassId = Guid.NewGuid(),
            CampusId = campusId,
            Name = name,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            ClassType = 1, // 1 = Lớp Chuyên Đề
            DomainId = domainId,
            DomainCode = domainCode,
            ClusterIndex = clusterIndex
        };

        await _context.Classes.AddAsync(thematicClass, ct);

        foreach (var studentId in studentIds)
        {
            var enrollment = new ClassEnrollment
            {
                EnrollmentId = Guid.NewGuid(),
                ClassId = thematicClass.ClassId,
                StudentId = studentId,
                Status = "ENROLLED",
                EnrolledAt = DateTime.UtcNow
            };
            await _context.ClassEnrollments.AddAsync(enrollment, ct);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Created thematic class '{ClassName}' (Id: {ClassId}, Cluster: {Cluster}) with {Count} enrolled students",
            name, thematicClass.ClassId, clusterIndex, studentIds.Count());

        return thematicClass;
    }

    public async Task<Class?> GetClassByIdAsync(
        Guid classId,
        CancellationToken ct = default)
    {
        return await _context.Classes.FirstOrDefaultAsync(c => c.ClassId == classId, ct);
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
