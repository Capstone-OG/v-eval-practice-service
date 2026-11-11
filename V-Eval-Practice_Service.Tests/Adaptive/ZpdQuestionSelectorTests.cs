using System;
using System.Collections.Generic;
using Xunit;
using V_Eval_Practice_Service.Application.Common.Adaptive;

namespace V_Eval_Practice_Service.Tests.Adaptive;

public class ZpdQuestionSelectorTests
{
    private readonly ZpdQuestionSelector _sut = new();

    [Fact]
    public void CalculateIrt2PlProbability_WhenThetaEqualsItemDifficulty_ShouldReturnFiftyPercent()
    {
        // Arrange: Khi năng lực học sinh đúng bằng độ khó câu hỏi (theta = b)
        double theta = 1.0;
        double a = 1.5;
        double b = 1.0;

        // Act: P = 1 / (1 + exp(-1.7 * a * (theta - b))) = 1 / (1 + exp(0)) = 0.50
        double probability = _sut.CalculateIrt2PlProbability(theta, a, b);

        // Assert
        Assert.Equal(0.50, probability, precision: 4);
    }
}
