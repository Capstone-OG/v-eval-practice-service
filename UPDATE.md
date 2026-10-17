# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [18/10/2026] - Hoàn Tất Giai Đoạn 1 & 2 Core Flow 5: CSDL, Hybrid Aggregation Engine & Background Worker

- **Giai Đoạn 1: Triển Khai 4 Thực Thể Domain Mới & Fluent API (`Domain/Entities/` & `Infrastructure/Persistence/`)**:
  - `ClassSkillHeatmap`: Lưu trữ từng ô ma trận nhiệt $P(L_t)$ cho học sinh và kỹ năng.
  - `ClassHeatmapSummary`: Tổng hợp thống kê cột kỹ năng toàn lớp (Mean, StdDev, StrugglingRate, Bimodal Flag, AlertStatus).
  - `StudentDailyActivity`: Ghi nhận chuyên cần và thời gian học tập hằng ngày theo chuẩn Active Study Day (Streak & Streak Freeze).
  - `StudentReadinessMetric`: Chỉ số sẵn sàng thi tổng hợp $R_{\text{exam}}$ cho Cổng Phụ huynh & Học sinh.
  - Cấu hình Fluent API trong `PracticeDbContext`, tự động hóa DDL khởi tạo trong `Program.cs` và file di trú `SQL_Migration_Flow5_Analytics.sql`.
- **Giai Đoạn 2: Xây Dựng Động Cơ Hybrid Aggregation Engine & Background Worker 15 Phút**:
  - `IClassHeatmapRepository` & `ClassHeatmapRepository`: Truy vấn ma trận nhiệt, lọc phân môn, batch upsert các bản ghi tóm tắt.
  - `IStudentActivityRepository` & `StudentActivityRepository`: Thuật toán tính Streak lùi ngày, tổng giờ học tuần và lưu metric.
  - `IClassHeatmapCacheService` & `ClassHeatmapCacheService`: Bộ đệm trong bộ nhớ `IMemoryCache` (< 300ms) kèm cơ chế xóa cache theo lớp `InvalidateClassHeatmapAsync`.
  - `IClassHeatmapAggregatorService` & `ClassHeatmapAggregatorService`: Tính toán thống kê khảo thí (Mean, StdDev, StrugglingRate, Bimodal Flag, 3 cấp độ cảnh báo) và chỉ số $R_{\text{exam}} = 0.50 S_\theta + 0.35 C_{\text{BKT}} + 0.15 F_{\text{consistency}}$.
  - `ClassHeatmapAggregationWorker`: Hosted Service chạy ngầm định kỳ 15 phút quét toàn bộ các lớp `ACTIVE`.
  - Tích hợp Write-Through O(1) vào các use case nộp bài thực tế: `SubmitAnswerCommandHandler`, `SubmitMockExamCommandHandler`, và `TrackVideoCommandHandler`.
- **Kiểm Thử & Biên Dịch**:
  - Biên dịch `dotnet build` toàn bộ solution `V-Eval-Practice_Service.sln` thành công 100% (**0 Warning, 0 Error**).
