using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VEval.Shared.Grpc.Content;
using V_Eval_Practice_Service.Application.Common.Interfaces;

namespace V_Eval_Practice_Service.Infrastructure.GrpcClients;

public class ContentGrpcClient : IContentGrpcClient
{
    private readonly string _serviceUrl;
    private readonly ILogger<ContentGrpcClient> _logger;

    public ContentGrpcClient(IConfiguration configuration, ILogger<ContentGrpcClient> logger)
    {
        _serviceUrl = configuration["GrpcSettings:ContentServiceUrl"] ?? "http://localhost:5249";
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, ExamQuestionKeyDto>> GetExamAnswerKeysAsync(
        Guid examId,
        CancellationToken ct = default)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_serviceUrl);
            var client = new ContentService.ContentServiceClient(channel);

            var request = new GetExamAnswerKeyRequest
            {
                ExamId = examId.ToString()
            };

            var response = await client.GetExamAnswerKeyAsync(request, cancellationToken: ct);

            var dictionary = new Dictionary<Guid, ExamQuestionKeyDto>();
            foreach (var key in response.AnswerKeys)
            {
                if (Guid.TryParse(key.QuestionId, out var qId))
                {
                    dictionary[qId] = new ExamQuestionKeyDto(
                        qId,
                        key.QuestionOrder,
                        key.CorrectOption,
                        key.SkillId,
                        key.DifficultyLevel,
                        key.SkillName,
                        key.DomainId,
                        key.DomainName
                    );
                }
            }

            return dictionary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi gRPC Content Service ({ServiceUrl}) cho exam {ExamId}",
                _serviceUrl, examId);
            throw;
        }
    }

    public async Task<IReadOnlyList<SkillTreeNodeDto>> GetSkillsTreeAsync(CancellationToken ct = default)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_serviceUrl);
            var client = new ContentService.ContentServiceClient(channel);

            var request = new GetSkillsTreeRequest();
            var response = await client.GetSkillsTreeAsync(request, cancellationToken: ct);

            var list = new List<SkillTreeNodeDto>();
            foreach (var node in response.Skills)
            {
                if (Guid.TryParse(node.SkillId, out var skillId))
                {
                    var prereqIds = new List<Guid>();
                    foreach (var pidStr in node.PrerequisiteIds)
                    {
                        if (Guid.TryParse(pidStr, out var pid))
                        {
                            prereqIds.Add(pid);
                        }
                    }

                    double weight = node.Weight > 0 ? node.Weight : 0.05;

                    list.Add(new SkillTreeNodeDto(
                        skillId,
                        node.Name,
                        weight,
                        prereqIds,
                        node.Description
                    ));
                }
            }

            _logger.LogInformation("Nhận được {Count} kỹ năng từ Content Service gRPC ({ServiceUrl})",
                list.Count, _serviceUrl);
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi gRPC Content Service ({ServiceUrl}) GetSkillsTree", _serviceUrl);
            throw;
        }
    }
}
