using System;
using System.Collections.Generic;
using System.Linq;

namespace V_Eval_Practice_Service.Application.Common.Adaptive;

public record CandidateQuestion(
    Guid QuestionId,
    string Content,
    IReadOnlyList<CandidateQuestionOption> Options,
    int DifficultyLevel,
    double ItemDifficultyB,
    double ItemDiscriminationA,
    Guid SkillId,
    string SkillName
);

public record CandidateQuestionOption(
    string OptionId,
    string Content
);

public interface IZpdQuestionSelector
{
    CandidateQuestion? SelectNextQuestion(
        double studentPlt,
        IEnumerable<CandidateQuestion> pool,
        ISet<Guid> answeredQuestionIds);

    double CalculateIrt2PlProbability(double theta, double a, double b);
    double ConvertPltToTheta(double plt);
}

public class ZpdQuestionSelector : IZpdQuestionSelector
{
    /// <summary>
    /// Chuyển đổi xác suất thành thạo BKT P(Lt) in [0.01, 0.99] sang thang năng lực IRT theta in [-2.5, +2.5]
    /// </summary>
    public double ConvertPltToTheta(double plt)
    {
        // Kẹp giá trị an toàn tránh log(0) hoặc chia cho 0
        double clampedPlt = Math.Clamp(plt, 0.05, 0.95);
        double logit = Math.Log(clampedPlt / (1.0 - clampedPlt));
        // Thang đo theta thông dụng [-2.5, +2.5]
        return Math.Clamp(logit, -2.5, 2.5);
    }

    /// <summary>
    /// Tính xác suất trả lời đúng theo mô hình IRT 2PL: P = 1 / (1 + exp(-1.7 * a * (theta - b)))
    /// </summary>
    public double CalculateIrt2PlProbability(double theta, double a, double b)
    {
        double exponent = -1.7 * a * (theta - b);
        return 1.0 / (1.0 + Math.Exp(exponent));
    }

    /// <summary>
    /// Chọn câu hỏi tối ưu trong vùng phát triển gần nhất (ZPD: P in [0.60, 0.75])
    /// </summary>
    public CandidateQuestion? SelectNextQuestion(
        double studentPlt,
        IEnumerable<CandidateQuestion> pool,
        ISet<Guid> answeredQuestionIds)
    {
        // 1. Lọc các câu chưa từng trả lời
        var unattempted = pool.Where(q => !answeredQuestionIds.Contains(q.QuestionId)).ToList();
        if (unattempted.Count == 0) return null;

        double theta = ConvertPltToTheta(studentPlt);

        // 2. Tính xác suất P cho từng câu hỏi
        var scoredQuestions = unattempted.Select(q => new
        {
            Question = q,
            Probability = CalculateIrt2PlProbability(theta, q.ItemDiscriminationA, q.ItemDifficultyB)
        }).ToList();

        // 3. Ưu tiên 1: Vùng ZPD lý tưởng P in [0.60, 0.75]
        var idealZpd = scoredQuestions
            .Where(x => x.Probability >= 0.60 && x.Probability <= 0.75)
            .OrderBy(x => Math.Abs(x.Probability - 0.675)) // Gần tâm vùng ZPD nhất
            .Select(x => x.Question)
            .FirstOrDefault();

        if (idealZpd != null) return idealZpd;

        // 4. Ưu tiên 2 (Fallback): Nới lỏng dải ZPD P in [0.50, 0.85]
        var relaxedZpd = scoredQuestions
            .Where(x => x.Probability >= 0.50 && x.Probability <= 0.85)
            .OrderBy(x => Math.Abs(x.Probability - 0.675))
            .Select(x => x.Question)
            .FirstOrDefault();

        if (relaxedZpd != null) return relaxedZpd;

        // 5. Ưu tiên 3: Lấy câu hỏi có khoảng cách xác suất gần nhất với mục tiêu 0.675
        return scoredQuestions
            .OrderBy(x => Math.Abs(x.Probability - 0.675))
            .Select(x => x.Question)
            .FirstOrDefault();
    }
}
