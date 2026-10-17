# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [18/10/2026] - Hoàn Tất Giai Đoạn 1 Core Flow 5: Khởi Tạo 4 Thực Thể Domain & Cấu Hình CSDL

- **Triển Khai 4 Thực Thể Domain Mới (`Domain/Entities/`)**:
  - `ClassSkillHeatmap`: Lưu trữ từng ô ma trận nhiệt $P(L_t)$ cho học sinh và kỹ năng.
  - `ClassHeatmapSummary`: Tổng hợp thống kê cột kỹ năng toàn lớp (Mean, StdDev, StrugglingRate, Bimodal Flag, AlertStatus).
  - `StudentDailyActivity`: Ghi nhận chuyên cần và thời gian học tập hằng ngày theo chuẩn Active Study Day (Streak & Streak Freeze).
  - `StudentReadinessMetric`: Chỉ số sẵn sàng thi tổng hợp $R_{\text{exam}}$ cho Cổng Phụ huynh & Học sinh.
- **Cấu Hình Tầng Dữ Liệu & Hạ Tầng (`Infrastructure/Persistence/PracticeDbContext.cs`)**:
  - Khai báo 4 `DbSet` trong schema `v_eval_practice`.
  - Cấu hình Fluent API: Khóa chính, ràng buộc unique index (`uq_class_student_skill`, `uq_class_skill_summary`, `uq_student_activity_date`, `idx_readiness_student_id`) và quan hệ cascade delete với bảng `Classes`.
- **Tự Động Hóa DDL Khởi Tạo Database (`API/Program.cs` & `docs/SQL`)**:
  - Tích hợp câu lệnh `CREATE TABLE IF NOT EXISTS` và chỉ mục tự động khởi tạo khi service khởi động.
  - Xuất bản file script DDL di trú độc lập `SQL_Migration_Flow5_Analytics.sql`.
- **Kiểm Thử & Biên Dịch**:
  - Biên dịch `dotnet build` toàn bộ solution thành công sạch sẽ 100% (**0 Warning, 0 Error**).
