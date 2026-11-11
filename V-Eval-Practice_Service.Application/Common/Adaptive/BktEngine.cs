using System;

namespace V_Eval_Practice_Service.Application.Common.Adaptive;

public record BktUpdateResult(
    double PriorPlt,
    double PosteriorPlt,
    bool IsLuckyGuess,
    bool IsCorrect
);

public interface IBktEngine
{
    BktUpdateResult ComputePosterior(
        double priorPlt,
        bool isCorrect,
        int timeSpentSeconds,
        double itemDifficultyB,
        double pTransit = 0.15,
        double pSlip = 0.10,
        double defaultGuess = 0.25);
}

public class BktEngine : IBktEngine
{
    public BktUpdateResult ComputePosterior(
        double priorPlt,
        bool isCorrect,
        int timeSpentSeconds,
        double itemDifficultyB,
        double pTransit = 0.15,
        double pSlip = 0.10,
        double defaultGuess = 0.25)
    {
        // 1. Kiểm tra phạt đoán mò (Lucky Guess Penalty):
        // Nếu trả lời đúng nhưng thời gian < 5s đối với câu hỏi có độ khó b >= 0.50
        bool isLuckyGuess = false;
        double pGuess = defaultGuess;

        if (isCorrect && timeSpentSeconds < 5 && itemDifficultyB >= 0.50)
        {
            isLuckyGuess = true;
            pGuess = 0.60; // Phạt tăng xác suất đoán mò lên 60%
        }

        // Kẹp prior trong khoảng an toàn [0.01, 0.99]
        double l_prev = Math.Clamp(priorPlt, 0.01, 0.99);

        // 2. Cập nhật Bayesian Posterior theo quan sát
        double pObserved;
        if (isCorrect)
        {
            double numerator = l_prev * (1.0 - pSlip);
            double denominator = (l_prev * (1.0 - pSlip)) + ((1.0 - l_prev) * pGuess);
            pObserved = denominator > 0 ? (numerator / denominator) : l_prev;
        }
        else
        {
            double numerator = l_prev * pSlip;
            double denominator = (l_prev * pSlip) + ((1.0 - l_prev) * (1.0 - pGuess));
            pObserved = denominator > 0 ? (numerator / denominator) : l_prev;
        }

        // 3. Cập nhật bước chuyển dịch tri thức (Knowledge Transition)
        double l_next = pObserved + ((1.0 - pObserved) * pTransit);
        double finalPosterior = Math.Clamp(Math.Round(l_next, 4), 0.0100, 0.9900);

        return new BktUpdateResult(
            priorPlt,
            finalPosterior,
            isLuckyGuess,
            isCorrect
        );
    }
}
