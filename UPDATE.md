# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [19/11/2026] - Nâng Cấp Xếp Lớp Ma Trận 4 Miền × 3 Cấp Bậc & Chuẩn Hóa Phân Giải Thực Thể Động

- **Chuẩn Hóa Phân Giải Thực Thể Động & Dữ Liệu Khóa Học Cho Cổng Phụ Huynh ([`GetParentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQueryHandler.cs), [`IdentityGrpcClient.cs`](./V-Eval-Practice_Service.Infrastructure/GrpcClients/IdentityGrpcClient.cs))**:
  - Truy vấn 100% dữ liệu trường THPT thật từ Identity Service gRPC (`item.SchoolName`), hiển thị `"Chưa cập nhật trường THPT"` hoặc tên cơ sở đào tạo thay vì gán tên trường cố định.
  - Phân giải lớp học thực tế từ bảng ghi danh `v_eval_practice.ClassEnrollments`, hiển thị `"Chưa phân lớp học"` khi học sinh chưa vào lớp.
  - Phân giải điểm mục tiêu thật (`TargetScore`), hiển thị `"Mục tiêu xét tuyển ĐGNL ({targetScore} điểm)"` hoặc `"Chưa thiết lập mục tiêu"`.
  - Triệt tiêu hoàn toàn fallback giáo viên giả lập, hiển thị chính xác `"Chưa có giáo viên phụ trách"` khi lớp chưa gán `TeacherId`.
  - Tự động gieo mầm (seed) 3 khóa học chuẩn hóa (`CRS_SPRINT`, `CRS_STD`, `CRS_PRO`) vào bảng `v_eval_enrollment."Courses"` trên PostgreSQL khi khởi động hệ thống.
- **Triển Khai Xếp Lớp Phân Tầng Theo Ma Trận 4 Miền × 3 Cấp Bậc ([`AutoClusterThematicClassesCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Classes/Commands/AutoClusterThematicClasses/AutoClusterThematicClassesCommandHandler.cs), [`ClassesController.cs`](./V-Eval-Practice_Service.API/Controllers/ClassesController.cs))**:
  - Chuyển đổi sang Ma trận 4 Miền × 3 Cấp bậc (tối thiểu 12 lớp chuẩn): mỗi học sinh được xếp vào đúng 4 lớp (1 lớp / miền) dựa trên độ nắm vững `masteryScore`.
  - Hỗ trợ `IncludeEmptyClasses = true`, tự động tách lớp khi vượt trần `20 HS/lớp`, và chế độ bảo toàn 100% học sinh cũ (`PreserveExistingEnrollments = true`).
- **Đồng Bộ Họ Tên Học Sinh & Khắc Phục Xung Đột Phòng Học ([`GetAlertsDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/EarlyWarnings/Queries/GetAlertsDashboard/GetAlertsDashboardQueryHandler.cs), [`ScheduleService.cs`](./V-Eval-Practice_Service.Infrastructure/Services/ScheduleService.cs))**:
  - Triển khai `GetStudentNamesAsync` gọi Identity Service gRPC truy vấn họ tên học sinh thực tế, khử trùng lặp cảnh báo sớm.
  - Bổ sung hàm kiểm tra xung đột phòng học (`RoomConflict`) và giảng viên (`TeacherConflict`) trả về `409 Conflict` chi tiết.
- **Kiểm Thử & Nghiệm Thu**:
  - `dotnet build`: Hoàn thành thành công, 0 lỗi, 0 cảnh báo.
  - `dotnet test`: Toàn bộ **20/20 unit tests Passed 100%**.
