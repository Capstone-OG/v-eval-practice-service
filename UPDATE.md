# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [18/11/2026] - Nâng Tầm Dữ Liệu Cổng Phụ Huynh: Triệt Tiêu Hardcode, Nối CSDL Thật Cho Thông Tin Trường Lớp & gRPC Danh Tính Giáo Viên Phụ Trách

- **Triệt Tiêu Hardcode Trong Parent Dashboard ([`GetParentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQueryHandler.cs))**:
  - Xóa bỏ 100% các dữ liệu mock/placeholder cũ: `"Học viên V-Eval"`, `"Hệ thống Khảo thí V-Eval"`, `"Đại học"`, `"Cố vấn Học tập"`, `"1900 8889"`.
  - Tích hợp truy vấn lớp học thực tế từ `v_eval_practice.ClassEnrollments` qua `IClassEnrollmentRepository`.
  - Lấy tên lớp học thật của học sinh (`enrolledClass.Name`, ví dụ `"Lớp Bứt Phá Điểm 10 - Offline Quận 1"`).
  - Trích xuất trường THPT thật từ `student.SchoolName` (ví dụ `"THPT Chuyên Lê Hồng Phong - TP.HCM"`).
  - Tự động suy luận ngành đại học mục tiêu theo điểm số nguyện vọng thật `TargetScore` (ví dụ: `>= 900` trỏ tới Khoa học Máy tính - ĐH Bách Khoa).
- **Liên Thông gRPC Danh Tính Giảng Viên Thật ([`IIdentityGrpcClient.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/IIdentityGrpcClient.cs), [`IdentityGrpcClient.cs`](./V-Eval-Practice_Service.Infrastructure/GrpcClients/IdentityGrpcClient.cs), [`identity.proto`](./V-Eval-Practice_Service.Infrastructure/Protos/identity.proto))**:
  - Khai báo RPC `GetTeacherProfile` và DTO phản hồi trong `identity.proto`.
  - Từ `enrolledClass.TeacherId`, gọi gRPC sang Identity Service (cổng 5156) lấy họ tên (`FullName`) và số điện thoại liên hệ (`Phone`) thật của giảng viên phụ trách.
  - Mở rộng `GetParentStudentsAsync` nhận diện thêm `SchoolName` và `TargetScore`.
- **Tối Ưu Repository Ghi Danh Lớp ([`ClassEnrollmentRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/ClassEnrollmentRepository.cs))**:
  - Ưu tiên bản ghi có trạng thái `Status == "ENROLLED"` và sắp xếp giảm dần theo thời gian ghi danh.
- **Kiểm Thử & Nghiệm Thu**:
  - `dotnet test`: Toàn bộ **20/20 unit tests Passed 100%** (0 Warning, 0 Error).
