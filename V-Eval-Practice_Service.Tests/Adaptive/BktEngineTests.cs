using Xunit;
using V_Eval_Practice_Service.Application.Common.Adaptive;

namespace V_Eval_Practice_Service.Tests.Adaptive;

public class BktEngineTests
{
    private readonly BktEngine _sut = new();

    [Fact]
    public void ComputePosterior_WhenAnswerIsCorrect_ShouldIncreaseMasteryProbability()
    {
        // Arrange
        double priorPlt = 0.20;
        bool isCorrect = true;
        int timeSpentSeconds = 30;
        double itemDifficultyB = 0.0;

        // Act
        var result = _sut.ComputePosterior(priorPlt, isCorrect, timeSpentSeconds, itemDifficultyB);

        // Assert
        Assert.True(result.PosteriorPlt > priorPlt, 
            $"Posterior Plt ({result.PosteriorPlt}) must be higher than Prior Plt ({priorPlt}) upon correct answer.");
        Assert.False(result.IsLuckyGuess);
    }

    [Fact]
    public void ComputePosterior_WhenAnswerIsIncorrect_ShouldDecreaseMasteryProbability()
    {
        // Arrange
        double priorPlt = 0.60;
        bool isCorrect = false;
        int timeSpentSeconds = 25;
        double itemDifficultyB = 0.0;

        // Act
        var result = _sut.ComputePosterior(priorPlt, isCorrect, timeSpentSeconds, itemDifficultyB);

        // Assert
        Assert.True(result.PosteriorPlt < priorPlt, 
            $"Posterior Plt ({result.PosteriorPlt}) must be lower than Prior Plt ({priorPlt}) upon incorrect answer.");
        Assert.False(result.IsLuckyGuess);
    }

    [Fact]
    public void ComputePosterior_WhenLuckyGuessPenaltyApplies_ShouldLimitPltGrowth()
    {
        // Arrange: Rule BR-08 - Answered correctly in < 5s for advanced question (b >= 0.50)
        double priorPlt = 0.30;
        bool isCorrect = true;
        int rapidTimeSpent = 3;
        int normalTimeSpent = 40;
        double advancedDifficulty = 0.80;

        // Act
        var penalizedResult = _sut.ComputePosterior(priorPlt, isCorrect, rapidTimeSpent, advancedDifficulty);
        var normalResult = _sut.ComputePosterior(priorPlt, isCorrect, normalTimeSpent, advancedDifficulty);

        // Assert
        Assert.True(penalizedResult.IsLuckyGuess, "Lucky guess flag must be true when solving hard question under 5 seconds.");
        Assert.True(penalizedResult.PosteriorPlt < normalResult.PosteriorPlt, 
            "Penalized posterior Plt must grow more conservatively than a standard thoughtful attempt.");
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(0.999)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void ComputePosterior_WithBoundaryValues_ShouldClampBetweenSafeRange(double extremePrior)
    {
        // Act
        var result = _sut.ComputePosterior(extremePrior, isCorrect: true, timeSpentSeconds: 15, itemDifficultyB: 0.1);

        // Assert
        Assert.InRange(result.PosteriorPlt, 0.0100, 0.9900);
    }
}
