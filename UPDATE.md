# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [02/11/2026] - Liên Thông Trực Tuyến Kết Quả Khảo Sát Chẩn Đoán (Core Flow 1) Sang Dashboard Phân Tích Học Tập & Ma Trận Nhiệt (Core Flow 5)

- **Đồng Bộ Write-Through Bài Thi Chẩn Đoán ([`SubmitDiagnosticCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Commands/SubmitDiagnostic/SubmitDiagnosticCommandHandler.cs), [`DiagnosticSubmissionDtos.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/DTOs/DiagnosticSubmissionDtos.cs), [`SubmitDiagnosticCommand.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Commands/SubmitDiagnostic/SubmitDiagnosticCommand.cs))**:
  - Hỗ trợ lưu trữ trọn vẹn vào PostgreSQL cho cả đề chuẩn từ Content Service và đề sinh động từ Google Gemini AI thông qua metadata câu hỏi tuỳ chọn và ánh xạ UUID kỹ năng tất định.
  - Tự động cập nhật đồng thời `ExamSubmissions`, `LearningProfiles`, `ClassSkillHeatmaps` (4 miền chuẩn V-ACT) và `StudentDailyActivities` (`IsStreakCounted = true`) ngay khi học sinh nộp bài khảo sát chẩn đoán, cho phép làm nhiều bài khảo sát để theo dõi sự tiến bộ theo thời gian.
- **Chuẩn Hoá Số Liệu Thực Tế Trên Dashboard Học Sinh ([`GetStudentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetStudentDashboard/GetStudentDashboardQueryHandler.cs))**:
  - Quy đổi chính xác kết quả bài khảo sát 30 câu sang thang điểm V-ACT `0 - 1200`, hiển thị đúng tỷ lệ làm chủ 4 miền, chuỗi ngày học thực tế và danh sách bài khảo sát/thi thử đã hoàn thành, loại bỏ hoàn toàn các con số mặc định giả định khi học sinh đã có dữ liệu làm bài.
- **Kiểm Thử & Xác Thực**:
  - Chạy `dotnet build --nologo` (**0 Warning, 0 Error**) và `dotnet test --nologo` (**4/4 unit tests pass**).
