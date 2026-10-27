using System;
using System.Collections.Generic;
using Xunit;
using V_Eval_Practice_Service.Application.Common.Adaptive;

namespace V_Eval_Practice_Service.Tests.Adaptive;

public class ZpdQuestionSelectorTests
{
    private readonly ZpdQuestionSelector _sut = new();

    [Theory]
    [InlineData(0.50, 0.0)]     // log(1) = 0
    [InlineData(0.85, 1.7346)]  // log(0.85 / 0.15) ≈ +1.7346
    [InlineData(0.15, -1.7346)] // log(0.15 / 0.85) ≈ -1.7346
    public void ConvertPltToTheta_ShouldMapPltToValidThetaRange(double plt, double expectedThetaApprox)
    {
        // Act
        double theta = _sut.ConvertPltToTheta(plt);

        // Assert
        Assert.InRange(theta, -2.5, 2.5);
        Assert.True(Math.Abs(theta - expectedThetaApprox) < 0.05, 
            $"Converted theta ({theta}) should be close to expected ({expectedThetaApprox}).");
    }

    [Fact]
    public void CalculateIrt2PlProbability_WhenThetaEqualsItemDifficulty_ShouldReturnFiftyPercent()
    {
        // Arrange: Năng lực học sinh đúng bằng độ khó câu hỏi (theta = b)
        double theta = 1.0;
        double a = 1.5;
        double b = 1.0;

        // Act
        double probability = _sut.CalculateIrt2PlProbability(theta, a, b);

        // Assert: P = 1 / (1 + exp(0)) = 0.50
        Assert.Equal(0.50, probability, precision: 4);
    }

    [Fact]
    public void SelectNextQuestion_ShouldPickQuestionWithinIdealZpdRange()
    {
        // Arrange
        double studentPlt = 0.50; // Theta ≈ 0.0
        var skillId = Guid.NewGuid();

        var qTooEasy = new CandidateQuestion(
            Guid.NewGuid(), "Câu quá dễ", new List<CandidateQuestionOption>(), 1, -2.0, 1.0, skillId, "Toán");
        var qIdeal = new CandidateQuestion(
            Guid.NewGuid(), "Câu ZPD lý tưởng", new List<CandidateQuestionOption>(), 2, -0.4, 1.0, skillId, "Toán"); // P ≈ 0.665
        var qTooHard = new CandidateQuestion(
            Guid.NewGuid(), "Câu quá khó", new List<CandidateQuestionOption>(), 4, 2.0, 1.0, skillId, "Toán");

        var pool = new List<CandidateQuestion> { qTooEasy, qIdeal, qTooHard };
        var answeredIds = new HashSet<Guid>();

        // Act
        var selected = _sut.SelectNextQuestion(studentPlt, pool, answeredIds);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal(qIdeal.QuestionId, selected.QuestionId);
    }
}
