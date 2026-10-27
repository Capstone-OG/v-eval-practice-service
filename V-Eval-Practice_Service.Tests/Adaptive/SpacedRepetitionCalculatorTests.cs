using System;
using Xunit;
using V_Eval_Practice_Service.Application.Common.Adaptive;

namespace V_Eval_Practice_Service.Tests.Adaptive;

public class SpacedRepetitionCalculatorTests
{
    [Fact]
    public void CalculateNextReview_WhenCorrectFirstTime_ShouldSetIntervalTo1Day()
    {
        // Arrange
        bool isCorrect = true;
        int currentConsecutive = 0;
        int currentInterval = 0;
        double currentEase = 2.50;

        // Act
        var result = SpacedRepetitionCalculator.CalculateNextReview(
            isCorrect, currentConsecutive, currentInterval, currentEase);

        // Assert
        Assert.Equal(1, result.ConsecutiveCorrect);
        Assert.Equal(1, result.IntervalDays);
        Assert.False(result.IsMastered);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1), result.NextReviewDate.Date);
    }

    [Fact]
    public void CalculateNextReview_WhenConsecutive3Times_ShouldSetMasteredTrueAndIntervalTo7Days()
    {
        // Arrange: Học sinh làm đúng lần thứ 3 liên tiếp
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

    [Fact]
    public void CalculateNextReview_WhenIncorrect_ShouldResetIntervalTo1DayAndConsecutiveToZero()
    {
        // Arrange: Học sinh trả lời sai sau khi đã có chuỗi ôn tập dài
        bool isCorrect = false;
        int currentConsecutive = 4;
        int currentInterval = 14;
        double currentEase = 2.70;

        // Act
        var result = SpacedRepetitionCalculator.CalculateNextReview(
            isCorrect, currentConsecutive, currentInterval, currentEase);

        // Assert
        Assert.Equal(0, result.ConsecutiveCorrect);
        Assert.Equal(1, result.IntervalDays);
        Assert.False(result.IsMastered);
        Assert.Equal(2.50, result.EaseFactor); // Ease giảm 0.20
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1), result.NextReviewDate.Date);
    }
}
