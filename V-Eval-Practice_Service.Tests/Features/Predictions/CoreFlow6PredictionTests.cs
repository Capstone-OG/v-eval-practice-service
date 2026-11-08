using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Moq;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.Commands.RecalculatePrediction;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;
using V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetMyForecast;
using V_Eval_Practice_Service.Domain.Entities;
using Xunit;

namespace V_Eval_Practice_Service.Tests.Features.Predictions;

public class CoreFlow6PredictionTests
{
    [Fact]
    public async Task GetMyForecast_WhenNoPredictionExists_ShouldAutoInitializeViaRecalculate()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var predictionRepo = new Mock<IScorePredictionRepository>();
        var sender = new Mock<ISender>();

        var bootstrappedEntity = new ScorePrediction
        {
            PredictionId = Guid.NewGuid(),
            StudentId = studentId,
            PredictedScore = 780,
            PredictedScoreMin = 732,
            PredictedScoreMax = 828,
            ConfidenceRate = 0.95,
            TargetScore = 850,
            DeficitScore = 70,
            SemScore = 24.5,
            CalculationSource = "AUTO_INIT",
            CreatedAt = DateTime.UtcNow
        };

        predictionRepo.SetupSequence(r => r.GetLatestByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScorePrediction?)null)
            .ReturnsAsync(bootstrappedEntity);

        sender.Setup(s => s.Send(It.Is<RecalculatePredictionCommand>(c =>
                c.EffectiveStudentId == studentId && c.Request.TriggerSource == "AUTO_INIT"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecalculatePredictionResponseDto>.Success(new RecalculatePredictionResponseDto
            {
                PredictionId = bootstrappedEntity.PredictionId,
                StudentId = studentId,
                PredictedScore = 780
            }));

        var handler = new GetMyForecastQueryHandler(predictionRepo.Object, sender.Object);

        // Act
        var result = await handler.Handle(new GetMyForecastQuery(studentId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(studentId);
        result.Value.Forecast.PredictedScore.Should().Be(780);
        result.Value.Forecast.CalculationSource.Should().Be("AUTO_INIT");
        result.Value.BalanceSheet.Status.Should().Be("CRITICAL");
        sender.Verify(s => s.Send(It.IsAny<RecalculatePredictionCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecalculatePrediction_WhenFirstTimeStudentHasExamSubmission_ShouldDeriveBaseScoreFromSubmission()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var skillId = Guid.Parse("a0000001-0000-0000-0000-000000000007");

        var predictionRepo = new Mock<IScorePredictionRepository>();
        var mockExamRepo = new Mock<IMockExamRepository>();
        var examSubmissionRepo = new Mock<IExamSubmissionRepository>();
        var mistakeRepo = new Mock<IMistakeNotebookRepository>();
        var learningProfileRepo = new Mock<ILearningProfileRepository>();
        var contentGrpc = new Mock<IContentGrpcClient>();

        predictionRepo.Setup(r => r.GetLatestByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScorePrediction?)null);

        examSubmissionRepo.Setup(r => r.GetByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExamSubmission>
            {
                new ExamSubmission
                {
                    SubmissionId = Guid.NewGuid(),
                    StudentId = studentId,
                    TotalQuestions = 20,
                    TotalCorrect = 15, // 15/20 * 1200 = 900
                    TotalScore = 75,
                    CompletedAt = DateTime.UtcNow
                }
            });

        mistakeRepo.Setup(r => r.GetByFilterAsync(
                studentId, null, null, false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<MistakeNotebook>
            {
                new MistakeNotebook
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    SkillId = skillId,
                    PatternId = "PAT_TOAN_LOGIC_01",
                    CognitiveErrorTag = "CARELESS",
                    Status = "REVIEWING"
                }
            }, 1));

        learningProfileRepo.Setup(r => r.GetByStudentIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LearningProfile>());

        contentGrpc.Setup(c => c.GetSkillsTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkillTreeNodeDto>());

        var handler = new RecalculatePredictionCommandHandler(
            predictionRepo.Object,
            mockExamRepo.Object,
            examSubmissionRepo.Object,
            mistakeRepo.Object,
            learningProfileRepo.Object,
            contentGrpc.Object);

        var cmd = new RecalculatePredictionCommand(studentId, new RecalculatePredictionRequestDto
        {
            TriggerSource = "AUTO_INIT"
        });

        // Act
        var result = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PredictedScore.Should().Be(900);
        result.Value.DeficitScore.Should().Be(0);
        result.Value.Status.Should().Be("ON_TRACK");
        result.Value.PrescriptionItemsCount.Should().BeGreaterThan(0);
        predictionRepo.Verify(r => r.AddAsync(It.IsAny<ScorePrediction>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
