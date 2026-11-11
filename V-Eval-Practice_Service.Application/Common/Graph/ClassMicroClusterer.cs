using System;
using System.Collections.Generic;
using System.Linq;

namespace V_Eval_Practice_Service.Application.Common.Graph;

/// <summary>
/// DTO đại diện cho một học sinh với hồ sơ kỹ năng phục vụ chia nhóm vi mô
/// </summary>
public class StudentSkillProfileItem
{
    public Guid StudentId { get; set; }
    public Dictionary<Guid, double> SkillScores { get; set; } = new();
    public double[] DomainFeatures { get; set; } = new double[4]; // [Lang, Math, NatSci, SocSci]
}

/// <summary>
/// Kết quả phân chia một nhóm học tập vi mô
/// </summary>
public class MicroGroupResult
{
    public int GroupIndex { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string FocusArea { get; set; } = string.Empty;
    public List<Guid> WeakSkillIds { get; set; } = new();
    public string RecommendedWorksheetTitle { get; set; } = string.Empty;
    public List<Guid> StudentIds { get; set; } = new();
}

public interface IClassMicroClusterer
{
    /// <summary>
    /// Phân chia học sinh trong một lớp thành các nhóm vi mô từ 3 đến 5 học sinh
    /// theo nguyên tắc đồng nhất năng lực & điểm nghẽn kiến thức (Homogeneous Ability & Weakness).
    /// </summary>
    List<MicroGroupResult> Partition(
        List<StudentSkillProfileItem> students,
        Dictionary<Guid, string>? skillNameMap = null,
        int preferredGroupSize = 4);
}

public class ClassMicroClusterer : IClassMicroClusterer
{
    public List<MicroGroupResult> Partition(
        List<StudentSkillProfileItem> students,
        Dictionary<Guid, string>? skillNameMap = null,
        int preferredGroupSize = 4)
    {
        if (students == null || students.Count < 3)
        {
            throw new InvalidOperationException("Lớp học cần tối thiểu 3 học sinh để có thể chia nhóm học tập vi mô (3 - 5 bạn/nhóm).");
        }

        int n = students.Count;

        // 1. Nếu số lượng học sinh từ 3 đến 5: Tạo chính xác 1 nhóm
        if (n <= 5)
        {
            var singleGroup = CreateGroupResult(
                groupIndex: 1,
                memberStudents: students,
                skillNameMap: skillNameMap
            );
            return new List<MicroGroupResult> { singleGroup };
        }

        // 2. Tính số lượng nhóm M sao cho 3 <= n / M <= 5
        int minM = (int)Math.Ceiling(n / 5.0); // Không nhóm nào > 5
        int maxM = (int)Math.Floor(n / 3.0);   // Không nhóm nào < 3

        int targetSize = Math.Clamp(preferredGroupSize, 3, 5);
        int targetM = (int)Math.Round((double)n / targetSize);
        int m = Math.Clamp(targetM, minM, maxM);

        // 3. Tính kích thước mục tiêu cho từng nhóm: m nhóm, mỗi nhóm có kích thước base hoặc base+1
        int baseSize = n / m;
        int remainder = n % m;
        var groupQuotas = new int[m];
        for (int i = 0; i < m; i++)
        {
            groupQuotas[i] = i < remainder ? baseSize + 1 : baseSize;
        }

        // 4. Chuẩn hóa vector đặc trưng cho từng học sinh để tính khoảng cách
        // Gom tất cả các SkillId phổ biến
        var allSkillIds = students
            .SelectMany(s => s.SkillScores.Keys)
            .Distinct()
            .ToList();

        var featureVectors = new List<double[]>();
        foreach (var s in students)
        {
            var vec = new List<double>();
            // 4 miền năng lực chuẩn
            vec.AddRange(s.DomainFeatures);

            // Điểm từng kỹ năng (nếu có)
            foreach (var skId in allSkillIds)
            {
                vec.Add(s.SkillScores.TryGetValue(skId, out var val) ? val : 0.50);
            }
            featureVectors.Add(vec.ToArray());
        }

        int dim = featureVectors[0].Length;

        // 5. Khởi tạo m centroids phân tán (K-Means++)
        var centroids = new List<double[]>();
        var rand = new Random(42);
        int firstIndex = rand.Next(n);
        centroids.Add((double[])featureVectors[firstIndex].Clone());

        for (int c = 1; c < m; c++)
        {
            var distances = new double[n];
            double sumDistSq = 0.0;

            for (int i = 0; i < n; i++)
            {
                double minDistSq = double.MaxValue;
                for (int j = 0; j < centroids.Count; j++)
                {
                    double d = EuclideanDistance(featureVectors[i], centroids[j]);
                    minDistSq = Math.Min(minDistSq, d * d);
                }
                distances[i] = minDistSq;
                sumDistSq += minDistSq;
            }

            double rVal = rand.NextDouble() * (sumDistSq > 0 ? sumDistSq : 1.0);
            double cumulative = 0.0;
            int chosenIdx = 0;
            for (int i = 0; i < n; i++)
            {
                cumulative += distances[i];
                if (cumulative >= rVal)
                {
                    chosenIdx = i;
                    break;
                }
            }
            centroids.Add((double[])featureVectors[chosenIdx].Clone());
        }

        // 6. Gán học sinh vào các nhóm thỏa mãn chính xác quota kích thước (3 - 5 người)
        // Dùng thuật toán tham lam ưu tiên cặp (học sinh, centroid) có khoảng cách ngắn nhất
        var groupAssignments = new List<List<int>>();
        for (int i = 0; i < m; i++)
        {
            groupAssignments.Add(new List<int>());
        }

        // Tạo danh sách tất cả các cặp (studentIdx, clusterIdx, distance)
        var pairDistances = new List<(int StudentIdx, int ClusterIdx, double Dist)>();
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                pairDistances.Add((i, j, EuclideanDistance(featureVectors[i], centroids[j])));
            }
        }
        pairDistances.Sort((a, b) => a.Dist.CompareTo(b.Dist));

        var assignedStudents = new HashSet<int>();
        foreach (var pair in pairDistances)
        {
            if (assignedStudents.Contains(pair.StudentIdx))
                continue;

            if (groupAssignments[pair.ClusterIdx].Count < groupQuotas[pair.ClusterIdx])
            {
                groupAssignments[pair.ClusterIdx].Add(pair.StudentIdx);
                assignedStudents.Add(pair.StudentIdx);
            }

            if (assignedStudents.Count == n)
                break;
        }

        // Fallback an toàn (nếu còn học sinh sót do quota)
        for (int i = 0; i < n; i++)
        {
            if (!assignedStudents.Contains(i))
            {
                // Tìm nhóm còn chỗ trống
                int targetGroup = -1;
                for (int j = 0; j < m; j++)
                {
                    if (groupAssignments[j].Count < groupQuotas[j])
                    {
                        targetGroup = j;
                        break;
                    }
                }
                if (targetGroup >= 0)
                {
                    groupAssignments[targetGroup].Add(i);
                    assignedStudents.Add(i);
                }
            }
        }

        // 7. Xây dựng kết quả sư phạm cho từng nhóm vi mô
        var results = new List<MicroGroupResult>();
        for (int i = 0; i < m; i++)
        {
            var memberStudents = groupAssignments[i].Select(idx => students[idx]).ToList();
            var groupRes = CreateGroupResult(
                groupIndex: i + 1,
                memberStudents: memberStudents,
                skillNameMap: skillNameMap
            );
            results.Add(groupRes);
        }

        return results;
    }

    private MicroGroupResult CreateGroupResult(
        int groupIndex,
        List<StudentSkillProfileItem> memberStudents,
        Dictionary<Guid, string>? skillNameMap)
    {
        // 1. Tính điểm trung bình 4 miền
        double avgLang = memberStudents.Average(s => s.DomainFeatures[0]);
        double avgMath = memberStudents.Average(s => s.DomainFeatures[1]);
        double avgNat = memberStudents.Average(s => s.DomainFeatures[2]);
        double avgSoc = memberStudents.Average(s => s.DomainFeatures[3]);

        var domainAverages = new (string Code, string Name, double Score)[]
        {
            ("DOM_LANG", "Ngôn ngữ & Đọc hiểu", avgLang),
            ("DOM_MATH", "Toán học & Tư duy Logic", avgMath),
            ("DOM_NAT_SCI", "Khoa học Tự nhiên", avgNat),
            ("DOM_SOC_SCI", "Khoa học Xã hội", avgSoc)
        };

        var lowestDomain = domainAverages.OrderBy(d => d.Score).First();

        // 2. Phân tích các kỹ năng cụ thể bị yếu (MasteryScore < 0.60)
        var allSkillsInGroup = memberStudents
            .SelectMany(s => s.SkillScores)
            .GroupBy(kv => kv.Key)
            .Select(g => new { SkillId = g.Key, AvgScore = g.Average(x => x.Value) })
            .OrderBy(x => x.AvgScore)
            .ToList();

        var weakSkills = allSkillsInGroup.Where(x => x.AvgScore < 0.60).ToList();
        var weakSkillIds = weakSkills.Select(x => x.SkillId).ToList();

        string focusArea;
        if (weakSkills.Count > 0 && skillNameMap != null)
        {
            var topWeakNames = weakSkills
                .Take(2)
                .Select(ws => skillNameMap.TryGetValue(ws.SkillId, out var name) ? name : "Kỹ năng chuyên đề")
                .ToList();
            focusArea = string.Join(" & ", topWeakNames);
        }
        else if (lowestDomain.Score < 0.70)
        {
            focusArea = lowestDomain.Name;
        }
        else
        {
            focusArea = "Luyện đề Nâng cao & Vận dụng cao";
        }

        string groupName = $"Nhóm {groupIndex:D2} - Bàn trọng tâm: {focusArea}";
        string recommendedWorksheet = $"Phiếu bài tập vi mô: Củng cố {focusArea}";

        return new MicroGroupResult
        {
            GroupIndex = groupIndex,
            GroupName = groupName,
            FocusArea = focusArea,
            WeakSkillIds = weakSkillIds,
            RecommendedWorksheetTitle = recommendedWorksheet,
            StudentIds = memberStudents.Select(s => s.StudentId).ToList()
        };
    }

    private static double EuclideanDistance(double[] a, double[] b)
    {
        double sum = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            double diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }
}
