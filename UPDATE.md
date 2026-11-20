# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [20/11/2026] - Bổ Sung API Chỉnh Sửa & Xóa / Ngừng Mở Khóa Học Theo Đợt (Course CRUD)

- **Mở Rộng Hợp Đồng DTO & Service Cho Chỉnh Sửa / Xóa Khóa Học ([`ScheduleDtos.cs`](./V-Eval-Practice_Service.Application/Features/Schedules/DTOs/ScheduleDtos.cs), [`IScheduleService.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Services/IScheduleService.cs), [`ScheduleService.cs`](./V-Eval-Practice_Service.Infrastructure/Services/ScheduleService.cs))**:
  - Bổ sung `UpdateCourseRequestDto` cho phép cập nhật tên khóa, mã khóa, loại chương trình (`PlanType`), thời lượng tuần (`DurationWeeks`), học phí (`TuitionFee`), sĩ số tối đa (`MaxClassSize`) và trạng thái mở bán (`IsActive`).
  - Triển khai `UpdateCourseAsync`: kiểm tra trùng mã khóa (`Course.DuplicateCode`) khi thay đổi mã, cập nhật trực tiếp vào bảng `v_eval_enrollment."Courses"`.
  - Triển khai `DeleteCourseAsync` với cơ chế bảo toàn dữ liệu tài chính & lớp học:
    - Nếu khóa học đã có lớp học (`Classes`) hoặc đã có học sinh đăng ký/đóng học phí (`CourseRegistrations`) -> tự động chuyển sang ẩn/ngừng mở (`IsActive = false` - Soft Archive) để không phá vỡ lịch sử biên lai & ghi danh.
    - Nếu khóa học chưa có ràng buộc nào -> xóa hẳn (`Remove`) khỏi cơ sở dữ liệu.
- **Mở Rộng REST Endpoints Tại [`CoursesController.cs`](./V-Eval-Practice_Service.API/Controllers/CoursesController.cs)**:
  - `PUT /api/v1/courses/{courseId:guid}` (cập nhật thông tin & học phí khóa học).
  - `DELETE /api/v1/courses/{courseId:guid}` (xóa hoặc ngừng mở bán khóa học an toàn).
- **Bổ Sung `CampusId` Vào `ChildAnalyticsDto` Cho Cổng Phụ Huynh ([`ParentDashboardDtos.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/DTOs/ParentDashboardDtos.cs), [`GetParentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQueryHandler.cs))**:
  - Trả về `CampusId` thật của học sinh (từ Identity gRPC hoặc lớp đang ghi danh) trong `GET /api/practice/analytics/parent-dashboard/{parentId}` giúp Cổng Học Phí Phụ huynh lọc chính xác Đợt học & Khóa học theo cơ sở của con, đồng bộ 100% với Học sinh.
- **Kiểm Thử & Nghiệm Thu**:
  - `dotnet build V-Eval-Practice_Service.Infrastructure.csproj --no-restore`: Thành công **0 Warning, 0 Error**.
  - `dotnet test V-Eval-Practice_Service.Tests.csproj --no-restore`: Toàn bộ **20/20 unit tests Passed 100%**.
