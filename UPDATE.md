# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [19/11/2026] - Triệt Tiêu Hoàn Toàn Giả Lập Giáo Viên: Hiển Thị Đúng Trạng Thái Chưa Phân Công Khi Lớp Chưa Có TeacherId

- **Loại Bỏ Hoàn Toàn Fallback Tên Giáo Viên Mẫu ([`GetParentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQueryHandler.cs))**:
  - Xóa bỏ triệt để chuỗi mặc định `"ThS. Nguyễn Minh Triết"` và `"0908 246 357"`.
  - Nếu lớp học chưa được phân công giáo viên (`TeacherId` là `null` hoặc `Guid.Empty` trong CSDL), trả về trung thực:
    - Họ tên: `"Chưa có giáo viên phụ trách"`.
    - Số điện thoại: `"Chưa có"`.
  - Chỉ khi lớp có `TeacherId` thật và Identity Service gRPC trả về hồ sơ tồn tại trong CSDL mới hiển thị họ tên và số điện thoại của giáo viên đó.
- **Kiểm Thử & Nghiệm Thu**:
  - `dotnet test`: Toàn bộ **20/20 unit tests Passed 100%** (0 Warning, 0 Error).
