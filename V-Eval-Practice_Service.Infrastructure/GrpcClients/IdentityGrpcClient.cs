using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VEval.Grpc.Identity;
using V_Eval_Practice_Service.Application.Common.Interfaces;

namespace V_Eval_Practice_Service.Infrastructure.GrpcClients;

public class IdentityGrpcClient : IIdentityGrpcClient
{
    private readonly string _serviceUrl;
    private readonly ILogger<IdentityGrpcClient> _logger;

    public IdentityGrpcClient(IConfiguration configuration, ILogger<IdentityGrpcClient> logger)
    {
        _serviceUrl = configuration["GrpcSettings:IdentityServiceUrl"] ?? "http://localhost:5155";
        _logger = logger;
    }

    public async Task<(bool Exists, string? CampusId, string? CampusName, int TargetScore)> GetStudentProfileAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_serviceUrl);
            var client = new IdentityGrpc.IdentityGrpcClient(channel);

            var request = new GetStudentSummaryRequest
            {
                StudentId = studentId.ToString()
            };

            var response = await client.GetStudentProfileSummaryAsync(request, cancellationToken: ct);
            return (response.Exists, response.CampusId, response.CampusName, response.TargetScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi gRPC Identity Service ({ServiceUrl}) cho student {StudentId}",
                _serviceUrl, studentId);
            throw;
        }
    }

    public async Task<System.Collections.Generic.List<(Guid StudentId, string Name, string Email, string CampusId, string SchoolName, int TargetScore)>> GetParentStudentsAsync(
        Guid parentId,
        CancellationToken ct = default)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_serviceUrl);
            var client = new IdentityGrpc.IdentityGrpcClient(channel);

            var request = new GetParentStudentsRequest
            {
                ParentId = parentId.ToString()
            };

            var response = await client.GetParentStudentsAsync(request, cancellationToken: ct);
            var result = new System.Collections.Generic.List<(Guid StudentId, string Name, string Email, string CampusId, string SchoolName, int TargetScore)>();

            if (response?.Students != null)
            {
                foreach (var item in response.Students)
                {
                    if (Guid.TryParse(item.StudentId, out var sId))
                    {
                        result.Add((
                            sId,
                            item.Name,
                            item.Email,
                            item.CampusId,
                            !string.IsNullOrWhiteSpace(item.SchoolName) ? item.SchoolName : string.Empty,
                            item.TargetScore > 0 ? item.TargetScore : 0
                        ));
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Identity gRPC] Không thể lấy danh sách học sinh cho Phụ huynh {ParentId}.", parentId);
            return new System.Collections.Generic.List<(Guid StudentId, string Name, string Email, string CampusId, string SchoolName, int TargetScore)>();
        }
    }

    public async Task<(bool Exists, string FullName, string Phone, string Email, string Specialty)> GetTeacherProfileAsync(
        Guid teacherId,
        CancellationToken ct = default)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_serviceUrl);
            var client = new IdentityGrpc.IdentityGrpcClient(channel);

            var request = new GetTeacherProfileRequest
            {
                TeacherId = teacherId.ToString()
            };

            var response = await client.GetTeacherProfileAsync(request, cancellationToken: ct);
            if (response != null && response.Exists)
            {
                return (true, response.FullName, response.Phone, response.Email, response.Specialty);
            }
            return (false, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Identity gRPC] Không thể lấy hồ sơ giáo viên cho TeacherId {TeacherId}.", teacherId);
            return (false, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }
}
