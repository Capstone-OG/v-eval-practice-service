namespace V_Eval_Practice_Service.Domain.Constants;

/// <summary>
/// Chuẩn hóa 6 mức độ tư duy theo thang đo Bloom cải tiến (Revised Bloom's Taxonomy)
/// Dùng để định lượng độ khó câu hỏi và đánh giá năng lực nhận thức của học sinh.
/// </summary>
public static class BloomTaxonomy
{
    /// <summary>Mức 1: Nhớ lại các sự kiện, thuật ngữ, định nghĩa, công thức cơ bản.</summary>
    public const int Remembering = 1;

    /// <summary>Mức 2: Hiểu ý nghĩa, diễn giải, so sánh, phân loại kiến thức cơ bản.</summary>
    public const int Understanding = 2;

    /// <summary>Mức 3: Vận dụng quy tắc, công thức, định lý vào tình huống bài toán quen thuộc.</summary>
    public const int Applying = 3;

    /// <summary>Mức 4: Phân tích cấu trúc dữ liệu, mối quan hệ nhân quả, suy luận logic từ dữ kiện.</summary>
    public const int Analyzing = 4;

    /// <summary>Mức 5: Đánh giá tính đúng đắn, phản biện, thẩm định phương án tối ưu dựa trên tiêu chí.</summary>
    public const int Evaluating = 5;

    /// <summary>Mức 6: Tổng hợp liên môn, thiết kế giải pháp mới, lập mô hình giải bài toán phức hợp.</summary>
    public const int Creating = 6;

    /// <summary>
    /// Lấy tên đầy đủ chuẩn hóa kèm mã mức độ tư duy Bloom.
    /// </summary>
    public static string GetName(int level) => level switch
    {
        1 => "Mức 1 - Nhận biết (Remembering)",
        2 => "Mức 2 - Thông hiểu (Understanding)",
        3 => "Mức 3 - Vận dụng (Applying)",
        4 => "Mức 4 - Phân tích (Analyzing)",
        5 => "Mức 5 - Đánh giá (Evaluating)",
        6 => "Mức 6 - Sáng tạo (Creating)",
        _ => $"Mức {level}"
    };

    /// <summary>
    /// Lấy nhãn ngắn gọn cho giao diện và báo cáo tóm tắt.
    /// </summary>
    public static string GetShortName(int level) => level switch
    {
        1 => "Nhận biết",
        2 => "Thông hiểu",
        3 => "Vận dụng",
        4 => "Phân tích",
        5 => "Đánh giá",
        6 => "Sáng tạo",
        _ => $"Mức {level}"
    };
}
