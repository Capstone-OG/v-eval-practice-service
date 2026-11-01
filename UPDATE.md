# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [01/11/2026] - Khắc Phục Lỗi Cú Pháp SQL Khởi Tạo CSDL Tự Động (`Program.cs`) Cho Core Flow 5 & Core Flow 6

- **Khắc Phục Khối Auto-DDL & Seed Data Khi Khởi Động ([`Program.cs`](./V-Eval-Practice_Service.API/Program.cs))**:
  - Loại bỏ triệt để các dấu xung đột Git còn sót bên trong chuỗi SQL nguyên văn `ExecuteSqlRawAsync(@"...")` và chỉnh lại phạm vi đóng ngoặc của khối kiểm tra `if (!heatmapExists)`.
  - Đảm bảo toàn bộ bảng phân tích học tập Core Flow 5 (`ClassSkillHeatmaps`, `ClassHeatmapSummaries`, `StudentDailyActivities`, `StudentReadinessMetrics`) và Core Flow 6 (`ScorePredictions`, `SystemAlerts`) cùng bộ dữ liệu mẫu chuẩn (Lớp `11111111-2222-3333-4444-555555555555`, 5 học sinh, ma trận nhiệt cảnh báo đỏ `CRITICAL_RED` 60%, chuỗi 7 ngày chuyên cần và chỉ số sẵn sàng thi `` `R_{\text{exam}} = 82.4` ``) được tự động khởi tạo an toàn khi dịch vụ chạy.
- **Kiểm Thử & Xác Thực**:
  - Chạy `dotnet build --nologo` (**0 Warning, 0 Error**) và `dotnet test --no-build --nologo` (**4/4 unit tests pass**).
