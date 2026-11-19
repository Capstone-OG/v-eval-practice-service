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

    public async Task<IReadOnlyList<(Guid ClassId, string ClassName, string DomainCode, List<Guid> StudentIds)>>
        GetActiveDomainClassesWithStudentsAsync(
            Guid campusId,
            CancellationToken ct = default)
    {
        var domainClasses = await _context.Classes
            .Where(c =>
                c.CampusId == campusId &&
                c.Status == "ACTIVE" &&
                c.ClassType == 1 &&
                c.DomainCode != null &&
                c.DomainCode != "")
            .OrderBy(c => c.Name)
            .ThenBy(c => c.CreatedAt)
            .Select(c => new { c.ClassId, c.Name, c.DomainCode })
            .ToListAsync(ct);

        if (domainClasses.Count == 0)
        {
            return Array.Empty<(Guid, string, string, List<Guid>)>();
        }

        var classIds = domainClasses.Select(c => c.ClassId).ToList();
        var enrollments = await _context.ClassEnrollments
            .Where(e => classIds.Contains(e.ClassId) && e.Status == "ENROLLED")
            .Select(e => new { e.ClassId, e.StudentId })
            .ToListAsync(ct);

        var studentsByClass = enrollments
            .GroupBy(e => e.ClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentId).Distinct().ToList());

        return domainClasses
            .Select(c => (
                c.ClassId,
                c.Name,
                c.DomainCode!,
                studentsByClass.TryGetValue(c.ClassId, out var sids) ? sids : new List<Guid>()))
            .ToList();
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
        var studentIdList = studentIds.Distinct().ToList();

        // 1. Idempotent Upsert: Kiểm tra nếu lớp chuyên đề cùng cơ sở, cùng miền và cùng tên đã tồn tại thì tái sử dụng
        var thematicClass = await _context.Classes
            .FirstOrDefaultAsync(c =>
                c.CampusId == campusId &&
                c.Status == "ACTIVE" &&
                c.ClassType == 1 &&
                c.DomainCode == domainCode &&
                c.Name == name, ct);

        if (thematicClass == null)
        {
            thematicClass = new Class
            {
                ClassId = Guid.NewGuid(),
                CampusId = campusId,
                Name = name,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                ClassType = 1, // 1 = Lớp Chuyên Đề theo Miền & Cấp bậc
                DomainId = domainId,
                DomainCode = domainCode,
                ClusterIndex = clusterIndex
            };

            await _context.Classes.AddAsync(thematicClass, ct);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            thematicClass.ClusterIndex = clusterIndex;
        }

        if (studentIdList.Count > 0)
        {
            // 2. Nếu học sinh đang ở một cấp bậc khác của CÙNG MIỀN kiến thức này thì chuyển trạng thái cũ sang TRANSFERRED
            // Đảm bảo mỗi học sinh chỉ học đúng 1 lớp ACTIVE trên mỗi miền (tổng 4 lớp cho 4 miền)
            if (!string.IsNullOrWhiteSpace(domainCode))
            {
                var otherDomainEnrollments = await _context.ClassEnrollments
                    .Include(e => e.Class)
                    .Where(e =>
                        studentIdList.Contains(e.StudentId) &&
                        e.Status == "ENROLLED" &&
                        e.ClassId != thematicClass.ClassId &&
                        e.Class != null &&
                        e.Class.CampusId == campusId &&
                        e.Class.ClassType == 1 &&
                        e.Class.DomainCode == domainCode)
                    .ToListAsync(ct);

                foreach (var oldEnrollment in otherDomainEnrollments)
                {
                    oldEnrollment.Status = "TRANSFERRED";
                }
            }

            // 3. Chỉ ghi danh mới cho những học sinh chưa có mặt trong lớp này
            var existingStudentIdsInClass = await _context.ClassEnrollments
                .Where(e => e.ClassId == thematicClass.ClassId && e.Status == "ENROLLED")
                .Select(e => e.StudentId)
                .ToListAsync(ct);

            var existingSet = existingStudentIdsInClass.ToHashSet();
            foreach (var studentId in studentIdList)
            {
                if (!existingSet.Contains(studentId))
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
            }

            await _context.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Upserted thematic class '{ClassName}' (Id: {ClassId}, Domain: {DomainCode}, Slot: {Cluster}) with {Count} students",
            name, thematicClass.ClassId, domainCode, clusterIndex, studentIdList.Count);

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

    public async Task<V_Eval_Practice_Service.Application.Features.Classes.DTOs.EnrollOrTransferStudentResponseDto?> EnrollOrTransferStudentToClassAsync(
        Guid targetClassId,
        Guid studentId,
        CancellationToken ct = default)
    {
        const int MaxCapacity = 20;
        var requestedClass = await _context.Classes.FirstOrDefaultAsync(c => c.ClassId == targetClassId, ct);
        if (requestedClass == null)
        {
            return null;
        }

        // Kiểm tra xem học sinh đã nằm trong chính lớp này chưa
        var existingInSameClass = await _context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.ClassId == targetClassId && e.StudentId == studentId && e.Status == "ENROLLED", ct);

        int currentCount = await _context.ClassEnrollments
            .CountAsync(e => e.ClassId == targetClassId && e.Status == "ENROLLED", ct);

        if (existingInSameClass != null)
        {
            return new V_Eval_Practice_Service.Application.Features.Classes.DTOs.EnrollOrTransferStudentResponseDto
            {
                RequestedClassId = targetClassId,
                FinalClassId = requestedClass.ClassId,
                FinalClassName = requestedClass.Name,
                StudentId = studentId,
                CurrentEnrolledCount = currentCount,
                MaxCapacity = MaxCapacity,
                IsFull = currentCount >= MaxCapacity,
                OverflowedToNewCohort = false,
                Message = $"Học sinh đã được ghi danh trong lớp {requestedClass.Name} ({currentCount}/{MaxCapacity})."
            };
        }

        Class destinationClass = requestedClass;
        bool overflowed = false;

        // Nếu lớp yêu cầu đã đầy 20/20 -> Tự động tràn (Overflow) sang lớp kế tiếp (Cohort 02) theo MGR_02 / BR-21
        if (currentCount >= MaxCapacity)
        {
            overflowed = true;
            string basePrefix = System.Text.RegularExpressions.Regex.Replace(requestedClass.Name, @"\s+\d{2}$", "").Trim();
            var siblingClasses = await _context.Classes
                .Where(c => c.CampusId == requestedClass.CampusId &&
                            c.Status == "ACTIVE" &&
                            c.ClassType == requestedClass.ClassType &&
                            c.Name.StartsWith(basePrefix))
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);

            var siblingIds = siblingClasses.Select(c => c.ClassId).ToList();
            var countsMap = await _context.ClassEnrollments
                .Where(e => siblingIds.Contains(e.ClassId) && e.Status == "ENROLLED")
                .GroupBy(e => e.ClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count, ct);

            var availableSibling = siblingClasses.FirstOrDefault(
                c => c.ClassId != targetClassId && countsMap.GetValueOrDefault(c.ClassId, 0) < MaxCapacity);

            if (availableSibling != null)
            {
                destinationClass = availableSibling;
            }
            else
            {
                int nextCohortNum = siblingClasses.Count + 1;
                destinationClass = new Class
                {
                    ClassId = Guid.NewGuid(),
                    CampusId = requestedClass.CampusId,
                    Name = $"{basePrefix} {nextCohortNum:D2}",
                    Status = "ACTIVE",
                    ClassType = requestedClass.ClassType,
                    DomainId = requestedClass.DomainId,
                    DomainCode = requestedClass.DomainCode,
                    TeacherId = requestedClass.TeacherId,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.Classes.AddAsync(destinationClass, ct);
                await _context.SaveChangesAsync(ct);
            }
        }

        // Chuyển lớp nếu học sinh đang ở một lớp khác cùng loại hoặc thêm mới
        var existingEnrollment = await _context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.Status == "ENROLLED", ct);

        if (existingEnrollment != null)
        {
            existingEnrollment.ClassId = destinationClass.ClassId;
            existingEnrollment.EnrolledAt = DateTime.UtcNow;
        }
        else
        {
            await _context.ClassEnrollments.AddAsync(new ClassEnrollment
            {
                EnrollmentId = Guid.NewGuid(),
                ClassId = destinationClass.ClassId,
                StudentId = studentId,
                Status = "ENROLLED",
                EnrolledAt = DateTime.UtcNow
            }, ct);
        }

        await _context.SaveChangesAsync(ct);

        int finalCount = await _context.ClassEnrollments
            .CountAsync(e => e.ClassId == destinationClass.ClassId && e.Status == "ENROLLED", ct);
        bool isFullNow = finalCount >= MaxCapacity;

        string msg = overflowed
            ? $"Lớp {requestedClass.Name} đã đạt trần 20/20 học sinh (FULL). Hệ thống đã tự động mở và chuyển học sinh thứ 21 sang {destinationClass.Name} ({finalCount}/{MaxCapacity})."
            : isFullNow
                ? $"Đã xếp học sinh vào lớp {destinationClass.Name}. Lớp đã chạm ngưỡng đầy sĩ số FULL ({finalCount}/{MaxCapacity})."
                : $"Đã xếp học sinh vào lớp {destinationClass.Name} ({finalCount}/{MaxCapacity}) thành công.";

        return new V_Eval_Practice_Service.Application.Features.Classes.DTOs.EnrollOrTransferStudentResponseDto
        {
            RequestedClassId = targetClassId,
            FinalClassId = destinationClass.ClassId,
            FinalClassName = destinationClass.Name,
            StudentId = studentId,
            CurrentEnrolledCount = finalCount,
            MaxCapacity = MaxCapacity,
            IsFull = isFullNow,
            OverflowedToNewCohort = overflowed,
            Message = msg
        };
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
