using System;

namespace V_Eval_Practice_Service.Application.Common.Adaptive;

/// <summary>
/// Động cơ tính toán lịch lặp lại ngắt quãng dựa trên thuật toán SuperMemo-2 (SM-2)
/// Giúp tối ưu hóa khoảng cách ngày ôn tập để củng cố trí nhớ dài hạn (Long-term retention)
/// </summary>
public static class SpacedRepetitionCalculator
{
    public record Sm2Result(
        int ConsecutiveCorrect,
        int IntervalDays,
        double EaseFactor,
        bool IsMastered,
        DateTime NextReviewDate
    );

    /// <summary>
    /// Tính toán lần ôn tập tiếp theo dựa trên kết quả làm bài ôn tập
    /// </summary>
    /// <param name="isCorrect">Học sinh làm đúng hay sai</param>
    /// <param name="currentConsecutiveCorrect">Số lần làm đúng liên tiếp hiện tại</param>
    /// <param name="currentIntervalDays">Khoảng cách số ngày ôn tập hiện tại</param>
    /// <param name="currentEaseFactor">Hệ số dễ hiện tại</param>
    /// <returns>Kết quả cập nhật SM-2</returns>
    public static Sm2Result CalculateNextReview(
        bool isCorrect,
        int currentConsecutiveCorrect,
        int currentIntervalDays,
        double currentEaseFactor)
    {
        if (isCorrect)
        {
            int newConsecutive = currentConsecutiveCorrect + 1;
            int newInterval;

            if (newConsecutive == 1)
            {
                newInterval = 1;
            }
            else if (newConsecutive == 2)
            {
                newInterval = 3;
            }
            else if (newConsecutive == 3)
            {
                newInterval = 7;
            }
            else
            {
                newInterval = (int)Math.Max(1, Math.Round(currentIntervalDays * currentEaseFactor));
            }

            // Tăng nhẹ hệ số dễ khi làm đúng
            double newEase = Math.Min(3.00, currentEaseFactor + 0.10);

            // Đạt chuẩn thành thạo khi làm đúng liên tiếp từ 3 lần trở lên
            bool isMastered = newConsecutive >= 3;

            DateTime nextDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(newInterval), DateTimeKind.Utc);

            return new Sm2Result(
                ConsecutiveCorrect: newConsecutive,
                IntervalDays: newInterval,
                EaseFactor: Math.Round(newEase, 2),
                IsMastered: isMastered,
                NextReviewDate: nextDate
            );
        }
        else
        {
            // Khi làm sai: Reset chuỗi đúng, buộc ôn lại ngay ngày hôm sau (+1 ngày)
            double newEase = Math.Max(1.30, currentEaseFactor - 0.20);
            DateTime nextDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);

            return new Sm2Result(
                ConsecutiveCorrect: 0,
                IntervalDays: 1,
                EaseFactor: Math.Round(newEase, 2),
                IsMastered: false,
                NextReviewDate: nextDate
            );
        }
    }
}
