using System;
using System.Collections.Generic;
using System.Linq;

namespace V_Eval_Practice_Service.Application.Common.Graph;

/// <summary>
/// Vector đặc trưng 4 chiều biểu diễn năng lực học sinh theo 4 miền:
/// [0] = DOM_LANG, [1] = DOM_MATH, [2] = DOM_NAT_SCI, [3] = DOM_SOC_SCI
/// </summary>
public record StudentFeatureVector(Guid StudentId, double[] Features);

/// <summary>
/// Điểm dữ liệu trên đường cong Elbow (K và tổng bình phương khoảng cách cụm WCSS)
/// </summary>
public record ElbowDataPoint(int K, double Wcss);

/// <summary>
/// Kết quả phân cụm học sinh thành một Lớp Chuyên Đề
/// </summary>
public record ClusterResult(
    int ClusterIndex,
    double[] Centroid,
    List<string> DominantWeakDomains,
    string SuggestedClassName,
    List<Guid> StudentIds,
    Guid? TargetDomainId,
    string? TargetDomainCode,
    double Wcss = 0.0
);

/// <summary>
/// Giao diện thuật toán phân cụm học sinh theo lỗ hổng kiến thức
/// </summary>
public interface IStudentKMeansClusterer
{
    List<ClusterResult> Cluster(List<StudentFeatureVector> students, int k, int maxIter = 100, int seed = 42);

    (int OptimalK, List<ElbowDataPoint> ElbowData, List<ClusterResult> Clusters)
        FindOptimalKAndCluster(List<StudentFeatureVector> students, int minK = 2, int maxK = 8);
}

/// <summary>
/// Triển khai thuật toán Elbow Method + K-Means++ gom cụm học sinh có chung lỗ hổng kiến thức
/// </summary>
public class StudentKMeansClusterer : IStudentKMeansClusterer
{
    // 4 Miền Năng Lực Chuẩn V-ACT
    public static readonly Guid DomainLanguageId = Guid.Parse("b581ee4c-7277-4be8-a156-c5b20a0a59f6");
    public static readonly Guid DomainMathLogicId = Guid.Parse("6f3765db-943e-4810-bf74-d6a8bdc215da");
    public static readonly Guid DomainNaturalScienceId = Guid.Parse("77777777-7777-7777-7777-000000000001");
    public static readonly Guid DomainSocialScienceId = Guid.Parse("77777777-7777-7777-7777-000000000002");

    public const string CodeLang = "DOM_LANG";
    public const string CodeMath = "DOM_MATH";
    public const string CodeNatSci = "DOM_NAT_SCI";
    public const string CodeSocSci = "DOM_SOC_SCI";

    /// <summary>
    /// Chạy thuật toán Elbow Method để tìm K tối ưu, sau đó gom cụm học sinh với K tối ưu đó
    /// </summary>
    public (int OptimalK, List<ElbowDataPoint> ElbowData, List<ClusterResult> Clusters)
        FindOptimalKAndCluster(List<StudentFeatureVector> students, int minK = 2, int maxK = 8)
    {
        if (students == null || students.Count == 0)
        {
            return (0, new List<ElbowDataPoint>(), new List<ClusterResult>());
        }

        int n = students.Count;

        // Nếu số học sinh ít hơn 2, chỉ tạo 1 cụm duy nhất
        if (n < 2)
        {
            var singleCluster = Cluster(students, 1);
            var elbow1 = new List<ElbowDataPoint> { new(1, 0.0) };
            return (1, elbow1, singleCluster);
        }

        // Tự động giới hạn Kmax thích ứng theo N học sinh (tối đa N/3 để mỗi lớp có ít nhất 3 học sinh)
        int adaptiveMaxK = Math.Min(maxK, Math.Max(2, n / 3));
        if (adaptiveMaxK > n)
        {
            adaptiveMaxK = n;
        }

        int effectiveMinK = Math.Min(minK, adaptiveMaxK);

        var elbowData = new List<ElbowDataPoint>();
        var clusterCandidates = new Dictionary<int, List<ClusterResult>>();

        for (int k = effectiveMinK; k <= adaptiveMaxK; k++)
        {
            var clusters = Cluster(students, k, maxIter: 100, seed: 42);
            double totalWcss = clusters.Sum(c => c.Wcss);
            elbowData.Add(new ElbowDataPoint(k, Math.Round(totalWcss, 4)));
            clusterCandidates[k] = clusters;
        }

        // Tìm điểm gập Elbow Point bằng phương pháp khoảng cách cực đại đến đường thẳng nối 2 đầu
        int optimalK = DetermineElbowPoint(elbowData);

        var optimalClusters = clusterCandidates.TryGetValue(optimalK, out var best)
            ? best
            : clusterCandidates[effectiveMinK];

        return (optimalK, elbowData, optimalClusters);
    }

    /// <summary>
    /// Chạy thuật toán Lloyd's K-Means với khởi tạo K-Means++
    /// </summary>
    public List<ClusterResult> Cluster(List<StudentFeatureVector> students, int k, int maxIter = 100, int seed = 42)
    {
        if (students == null || students.Count == 0)
            return new List<ClusterResult>();

        int n = students.Count;
        int dimensions = students[0].Features.Length;

        if (k >= n)
        {
            // Mỗi học sinh là 1 cụm
            return students.Select((s, idx) =>
            {
                var (weakDomains, className, domainId, domainCode) = AnalyzeCentroid(s.Features);
                return new ClusterResult(
                    ClusterIndex: idx,
                    Centroid: (double[])s.Features.Clone(),
                    DominantWeakDomains: weakDomains,
                    SuggestedClassName: className,
                    StudentIds: new List<Guid> { s.StudentId },
                    TargetDomainId: domainId,
                    TargetDomainCode: domainCode,
                    Wcss: 0.0
                );
            }).ToList();
        }

        var rng = new Random(seed);

        // Bước 1: Khởi tạo Centroids bằng K-Means++
        double[][] centroids = InitCentroidsKMeansPlusPlus(students, k, rng);

        int[] assignments = new int[n];
        Array.Fill(assignments, -1);

        for (int iter = 0; iter < maxIter; iter++)
        {
            bool changed = false;

            // Assignment Step: Gán mỗi học sinh vào centroid gần nhất
            for (int i = 0; i < n; i++)
            {
                int nearestCluster = 0;
                double minDistance = double.MaxValue;

                for (int c = 0; c < k; c++)
                {
                    double dist = DistanceSquared(students[i].Features, centroids[c]);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestCluster = c;
                    }
                }

                if (assignments[i] != nearestCluster)
                {
                    assignments[i] = nearestCluster;
                    changed = true;
                }
            }

            // Nếu không còn học sinh nào đổi cụm -> Hội tụ, dừng sớm
            if (!changed && iter > 0)
                break;

            // Update Step: Tính lại toạ độ Centroid theo trung bình cộng
            for (int c = 0; c < k; c++)
            {
                var clusterMembers = students.Where((_, idx) => assignments[idx] == c).ToList();

                if (clusterMembers.Count == 0)
                {
                    // Empty cluster fallback: Chọn học sinh xa centroid nhất để làm centroid mới
                    int furthestIdx = 0;
                    double maxDist = -1;
                    for (int i = 0; i < n; i++)
                    {
                        double d = DistanceSquared(students[i].Features, centroids[assignments[i]]);
                        if (d > maxDist)
                        {
                            maxDist = d;
                            furthestIdx = i;
                        }
                    }
                    centroids[c] = (double[])students[furthestIdx].Features.Clone();
                    assignments[furthestIdx] = c;
                    continue;
                }

                double[] newCentroid = new double[dimensions];
                foreach (var member in clusterMembers)
                {
                    for (int d = 0; d < dimensions; d++)
                    {
                        newCentroid[d] += member.Features[d];
                    }
                }

                for (int d = 0; d < dimensions; d++)
                {
                    newCentroid[d] /= clusterMembers.Count;
                }

                centroids[c] = newCentroid;
            }
        }

        // Tổng hợp kết quả và tính WCSS cho từng cụm
        var results = new List<ClusterResult>();

        for (int c = 0; c < k; c++)
        {
            var clusterStudentIndices = Enumerable.Range(0, n)
                .Where(i => assignments[i] == c)
                .ToList();

            var studentIds = clusterStudentIndices
                .Select(i => students[i].StudentId)
                .ToList();

            double clusterWcss = clusterStudentIndices
                .Sum(i => DistanceSquared(students[i].Features, centroids[c]));

            var (weakDomains, className, domainId, domainCode) = AnalyzeCentroid(centroids[c]);

            results.Add(new ClusterResult(
                ClusterIndex: c,
                Centroid: centroids[c].Select(val => Math.Round(val, 4)).ToArray(),
                DominantWeakDomains: weakDomains,
                SuggestedClassName: className,
                StudentIds: studentIds,
                TargetDomainId: domainId,
                TargetDomainCode: domainCode,
                Wcss: Math.Round(clusterWcss, 4)
            ));
        }

        return results.OrderBy(r => r.ClusterIndex).ToList();
    }

    /// <summary>
    /// Khởi tạo tâm cụm K-Means++ phân tán đều, giảm thiểu nguy cơ rơi vào cực tiểu cục bộ
    /// </summary>
    private static double[][] InitCentroidsKMeansPlusPlus(List<StudentFeatureVector> students, int k, Random rng)
    {
        int n = students.Count;
        var centroids = new List<double[]>();

        // 1. Chọn ngẫu nhiên 1 học sinh làm tâm cụm đầu tiên
        int firstIndex = rng.Next(n);
        centroids.Add((double[])students[firstIndex].Features.Clone());

        double[] minDistancesSquared = new double[n];

        // 2. Chọn k - 1 tâm cụm tiếp theo
        for (int c = 1; c < k; c++)
        {
            double sumDistSquared = 0.0;

            for (int i = 0; i < n; i++)
            {
                double distToLast = DistanceSquared(students[i].Features, centroids[c - 1]);
                if (c == 1 || distToLast < minDistancesSquared[i])
                {
                    minDistancesSquared[i] = distToLast;
                }
                sumDistSquared += minDistancesSquared[i];
            }

            // Roulette wheel selection
            double randomThreshold = rng.NextDouble() * sumDistSquared;
            double cumulative = 0.0;
            int selectedIdx = n - 1;

            for (int i = 0; i < n; i++)
            {
                cumulative += minDistancesSquared[i];
                if (cumulative >= randomThreshold)
                {
                    selectedIdx = i;
                    break;
                }
            }

            centroids.Add((double[])students[selectedIdx].Features.Clone());
        }

        return centroids.ToArray();
    }

    /// <summary>
    /// Tính bình phương khoảng cách Euclidean giữa 2 vector
    /// </summary>
    private static double DistanceSquared(double[] a, double[] b)
    {
        double sum = 0.0;
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            double diff = a[i] - b[i];
            sum += diff * diff;
        }
        return sum;
    }

    /// <summary>
    /// Xác định điểm gập (Elbow Point) bằng phương pháp khoảng cách hình học cực đại từ điểm đến dây cung (Chord Method)
    /// </summary>
    private static int DetermineElbowPoint(List<ElbowDataPoint> elbowData)
    {
        if (elbowData.Count <= 2)
            return elbowData[0].K;

        double x1 = elbowData.First().K;
        double y1 = elbowData.First().Wcss;
        double x2 = elbowData.Last().K;
        double y2 = elbowData.Last().Wcss;

        double lineLength = Math.Sqrt((y2 - y1) * (y2 - y1) + (x2 - x1) * (x2 - x1));
        if (lineLength < 1e-6)
            return elbowData[0].K;

        int bestK = elbowData[0].K;
        double maxPerpendicularDist = -1.0;

        for (int i = 1; i < elbowData.Count - 1; i++)
        {
            double x0 = elbowData[i].K;
            double y0 = elbowData[i].Wcss;

            // Khoảng cách từ điểm (x0, y0) đến đường thẳng nối (x1, y1) và (x2, y2)
            double distance = Math.Abs((y2 - y1) * x0 - (x2 - x1) * y0 + x2 * y1 - y2 * x1) / lineLength;

            if (distance > maxPerpendicularDist)
            {
                maxPerpendicularDist = distance;
                bestK = elbowData[i].K;
            }
        }

        return bestK;
    }

    /// <summary>
    /// Phân tích vector trọng tâm (Centroid) để xác định lỗ hổng kiến thức nổi trội và đặt tên Lớp Chuyên Đề
    /// </summary>
    private static (List<string> WeakDomains, string ClassName, Guid? DomainId, string? DomainCode) AnalyzeCentroid(double[] centroid)
    {
        if (centroid == null || centroid.Length < 4)
        {
            return (new List<string>(), "Lớp Chuyên Đề Bổ Trợ Tổng Hợp", null, null);
        }

        double scoreLang = centroid[0];
        double scoreMath = centroid[1];
        double scoreNatSci = centroid[2];
        double scoreSocSci = centroid[3];

        var domainScores = new List<(string Code, Guid Id, string Name, double Score)>
        {
            (CodeLang, DomainLanguageId, "Ngôn ngữ", scoreLang),
            (CodeMath, DomainMathLogicId, "Toán - Logic", scoreMath),
            (CodeNatSci, DomainNaturalScienceId, "KHTN", scoreNatSci),
            (CodeSocSci, DomainSocialScienceId, "KHXH", scoreSocSci)
        };

        // Lọc các miền yếu: điểm < 0.60 hoặc cách điểm thấp nhất không quá 0.10
        double minScore = domainScores.Min(d => d.Score);
        var primaryWeak = domainScores.First(d => d.Score == minScore);

        var weakList = domainScores
            .Where(d => d.Score < 0.60 || (d.Score - minScore) <= 0.08)
            .OrderBy(d => d.Score)
            .ToList();

        var weakCodes = weakList.Select(w => w.Code).ToList();

        // Đặt tên lớp sư phạm phù hợp
        string className;
        if (minScore >= 0.75)
        {
            className = "Chuyên đề: Luyện đề Nâng cao & Vận dụng cao";
        }
        else if (weakList.Count >= 3)
        {
            className = "Chuyên đề: Củng cố Nền tảng Đa môn (Toán, Văn, KHTN)";
        }
        else if (weakList.Count == 2)
        {
            className = $"Chuyên đề: Tăng cường {weakList[0].Name} & {weakList[1].Name}";
        }
        else
        {
            className = $"Chuyên đề: Trọng điểm {primaryWeak.Name}";
        }

        return (weakCodes, className, primaryWeak.Id, primaryWeak.Code);
    }
}
