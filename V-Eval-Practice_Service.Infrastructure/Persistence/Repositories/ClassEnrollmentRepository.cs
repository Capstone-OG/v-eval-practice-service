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

        const int MaxClassCapacity = 20;

        // 1. Kiểm tra xem học sinh đã có enrollment nào trước đó chưa
        var existingEnrollment = await _context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.StudentId == studentId, ct);

        // Lấy tất cả các lớp đang ACTIVE của cơ sở này tương ứng với cấp độ (tierKeyword)
        var matchingClasses = await _context.Classes
            .Where(c => c.CampusId == campusGuid &&
                        c.Status == "ACTIVE" &&
                        c.ClassType == 0 &&
                        c.Name.Contains(tierKeyword))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        // Lấy sĩ số thực tế (học sinh đang ENROLLED) của từng lớp
        var classIds = matchingClasses.Select(c => c.ClassId).ToList();
        var enrollmentCounts = await _context.ClassEnrollments
            .Where(e => classIds.Contains(e.ClassId) && e.Status == "ENROLLED")
            .GroupBy(e => e.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count, ct);

        Class? targetClass = null;

        // Nếu học sinh đã có enrollment và đang ở một lớp hợp lệ cùng tier này thì ưu tiên giữ nguyên
        if (existingEnrollment?.Class != null &&
            matchingClasses.Any(c => c.ClassId == existingEnrollment.ClassId))
        {
            targetClass = matchingClasses.First(c => c.ClassId == existingEnrollment.ClassId);
        }
        else
        {
            // Tìm lớp đầu tiên chưa đầy sĩ số (< 20 học sinh)
            targetClass = matchingClasses.FirstOrDefault(c => enrollmentCounts.GetValueOrDefault(c.ClassId, 0) < MaxClassCapacity);
        }

        // Nếu tất cả các lớp đã đầy (>= 20) hoặc chưa có lớp nào tồn tại
        if (targetClass == null)
        {
            int nextClassNumber = matchingClasses.Count + 1;
            string className = $"{fullTierName} {nextClassNumber:D2} - {campusDisplay}";

            targetClass = new Class
            {
                ClassId = Guid.NewGuid(),
                CampusId = campusGuid,
                Name = className,
                Status = "ACTIVE",
                ClassType = 0,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Classes.AddAsync(targetClass, ct);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Created new cohort class '{ClassName}' ({ClassId}) for Campus {CampusId} with max capacity {Capacity}",
                targetClass.Name, targetClass.ClassId, campusId, MaxClassCapacity);
        }

        // 2. Ghi danh hoặc cập nhật ghi danh
        Guid enrollmentId;
        if (existingEnrollment != null)
        {
            existingEnrollment.ClassId = targetClass.ClassId;
            existingEnrollment.DiagnosticSubmissionId = diagnosticSubmissionId;
            existingEnrollment.Status = "ENROLLED";
            existingEnrollment.EnrolledAt = DateTime.UtcNow;
            enrollmentId = existingEnrollment.EnrollmentId;
            _logger.LogInformation("Updated enrollment for student {StudentId} to class {ClassId} ('{ClassName}')",
                studentId, targetClass.ClassId, targetClass.Name);
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
            _logger.LogInformation("Created new enrollment {EnrollmentId} for student {StudentId} in class {ClassId} ('{ClassName}')",
                enrollmentId, studentId, targetClass.ClassId, targetClass.Name);
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

    public async Task<IReadOnlyList<ClassEnrollment>> GetEnrollmentsByClassIdAsync(
        Guid classId,
        CancellationToken ct = default)
    {
        return await _context.ClassEnrollments
            .Where(e => e.ClassId == classId && e.Status == "ENROLLED")
            .ToListAsync(ct);
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
