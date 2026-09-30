# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [01/10/2026] - Nâng Cấp Core Flow 2 (Bước 1): Mở Rộng Mô Hình Thực Thể Class & Di Trú CSDL Phục Vụ Lớp Học Chuyên Đề (Thematic Cohort)

- **Mở Rộng Mô Hình Thực Thể [`Class.cs`](./V-Eval-Practice_Service.Domain/Entities/Class.cs)**:
  - Bổ sung 4 trường dữ liệu trọng yếu cho bài toán phân cụm lớp chuyên đề K-Means:
    - `ClassType` (`int`, mặc định `0` = Lớp hành chính phổ thông theo năng lực tổng thể $\theta_0$, `1` = Lớp chuyên đề theo cụm lỗ hổng K-Means).
    - `DomainId` (`Guid?`): Định danh miền kiến thức chuyên đề (Toán, Ngôn ngữ, KHTN, KHXH).
    - `DomainCode` (`string?`, max length 50): Mã định danh chuẩn (`DOM_LANG`, `DOM_MATH`, `DOM_NAT_SCI`, `DOM_SOC_SCI`).
    - `ClusterIndex` (`int?`): Chỉ số cụm tương ứng sinh ra bởi thuật toán phân cụm K-Means.
- **Cấu Hình Fluent API & DbContext ([`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs))**:
  - Ánh xạ rõ ràng các cột `class_type`, `domain_id`, `domain_code`, `cluster_index` vào bảng `Classes` thuộc schema `v_eval_practice`.
- **Di Trú CSDL (Database Migration & Verification)**:
  - Khởi tạo migration `20260930184119_AddThematicCohortFields`.
  - Tinh chỉnh migration để chỉ tác động thêm 4 cột mới vào bảng `Classes`, đảm bảo tương thích 100% với các bảng đã có trong PostgreSQL (`LearningRoadmaps`, `LiveSessions`, `RoadmapNodes`, `LiveSessionAttendance`).
  - Thực thi `dotnet ef database update` thành công, lưu bản ghi migration vào `practice."__EFMigrationsHistory"`.
  - Xác thực trực tiếp qua kiểm tra schema CSDL Supabase PostgreSQL: 4 cột `class_type (integer)`, `cluster_index (integer)`, `domain_code (character varying)`, `domain_id (uuid)` đã được áp dụng và sẵn sàng vận hành.
- **Kiểm Thử Vận Hành & Biên Dịch (Build Check)**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
