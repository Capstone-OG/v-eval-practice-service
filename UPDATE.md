# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [19/11/2026] - Chuẩn Hóa Phân Giải Thực Thể Động & Dữ Liệu Khóa Học Cho Cổng Phụ Huynh

- **Phân Giải Dữ Liệu Thực Thể Phụ Huynh & Học Sinh ([`GetParentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQueryHandler.cs), [`IdentityGrpcClient.cs`](./V-Eval-Practice_Service.Infrastructure/GrpcClients/IdentityGrpcClient.cs))**:
  - Truy vấn 100% dữ liệu trường THPT thật từ Identity Service gRPC (`item.SchoolName`), nếu chưa khai báo trả về thông điệp thân thiện `"Chưa cập nhật trường THPT"` hoặc tên cơ sở đào tạo thay vì gán tên trường cố định.
  - Phân giải lớp học thực tế từ bảng ghi danh `v_eval_practice.ClassEnrollments`, hiển thị `"Chưa phân lớp học"` khi học sinh chưa vào lớp.
  - Phân giải điểm mục tiêu thật (`TargetScore`), hiển thị `"Mục tiêu xét tuyển ĐGNL ({targetScore} điểm)"` hoặc `"Chưa thiết lập mục tiêu"`.
  - Triệt tiêu hoàn toàn fallback giáo viên giả lập, hiển thị chính xác `"Chưa có giáo viên phụ trách"` khi lớp chưa gán `TeacherId`.
- **Khởi Tạo Dữ Liệu Khóa Học Chuẩn Hóa & Phân Hệ Học Phí ([`Program.cs`](./V-Eval-Practice_Service.API/Program.cs), [`GetStudentTuitionStatusQuery.cs`](./V-Eval-Practice_Service.Application/Features/Payments/Queries/GetStudentTuitionStatus/GetStudentTuitionStatusQuery.cs))**:
  - Tự động gieo mầm (seed) 3 khóa học chuẩn hóa (`CRS_SPRINT`, `CRS_STD`, `CRS_PRO`) vào bảng `v_eval_enrollment."Courses"` trên PostgreSQL khi khởi động hệ thống.
  - API `GET /api/practice/courses` và phân hệ học phí đọc 100% các gói học với mã định danh GUID và đơn giá niêm yết thật từ CSDL.
- **Kiểm Thử & Nghiệm Thu**:
  - `dotnet build`: Hoàn thành thành công, 0 lỗi, 0 cảnh báo.
  - `dotnet test`: Toàn bộ **20/20 unit tests Passed 100%**.
