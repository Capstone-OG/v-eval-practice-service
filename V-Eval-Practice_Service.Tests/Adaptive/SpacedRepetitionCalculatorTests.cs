using System;
using Xunit;
using V_Eval_Practice_Service.Application.Common.Adaptive;

namespace V_Eval_Practice_Service.Tests.Adaptive;

public class SpacedRepetitionCalculatorTests
{
    [Fact]
    public void CalculateNextReview_WhenConsecutive3Times_ShouldSetMasteredTrueAndIntervalTo7Days()
    {
        // Arrange: Học sinh làm đúng lần thứ 3 liên tiếp -> Mastery
        bool isCorrect = true;
        int currentConsecutive = 2;
        int currentInterval = 3;
        double currentEase = 2.60;

        // Act
        var result = SpacedRepetitionCalculator.CalculateNextReview(
            isCorrect, currentConsecutive, currentInterval, currentEase);

        // Assert
        Assert.Equal(3, result.ConsecutiveCorrect);
        Assert.Equal(7, result.IntervalDays);
        Assert.True(result.IsMastered, "Question must be marked mastered upon achieving 3 consecutive correct reviews.");
        Assert.Equal(DateTime.UtcNow.Date.AddDays(7), result.NextReviewDate.Date);
    }
}
