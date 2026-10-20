# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [20/10/2026] - Hoàn Thiện Toàn Diện Backend Core Flow 5: Khởi Tạo Dữ Liệu Mẫu & Đồng Bộ gRPC

- **Khởi Tạo Dữ Liệu Mẫu Lớp Học & Ma Trận Nhiệt (`Program.cs`)**:
  - Tự động gieo dữ liệu kiểm thử thực tế cho lớp mẫu `11111111-2222-3333-4444-555555555555` tại cơ sở `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee`.
  - Khởi tạo 5 học sinh mẫu liên kết bảng `ClassEnrollments`.
  - Khởi tạo 4 kỹ năng chuẩn thuộc 4 phân môn lớn với ma trận nhiệt thực tế: cố tình tạo tỷ lệ yếu 60% (3/5 học sinh có `` `P(L_t) < 0.50` ``) tại kỹ năng "Toán logic mệnh đề & suy luận kéo theo" (`88888888-9999-aaaa-bbbb-cccccccccccc`) nhằm kích hoạt trực quan cảnh báo đỏ `CRITICAL_RED` trên Teacher Heatmap.
  - Khởi tạo 7 ngày hoạt động tự học liên tiếp (`StudentDailyActivities`) và chỉ số sẵn sàng thi `` `R_{\text{exam}} = 82.4` `` (`READY`) cho học sinh Hoàng (`3fa85f64-5717-4562-b3fc-2c963f66afa6`).
- **Đồng Bộ gRPC Liên Dịch Vụ**:
  - Kết nối hoàn hảo phương thức `GetParentStudents` và `LinkParentStudent` qua gRPC Identity Service phục vụ phân hệ giám sát phụ huynh (Parent Companion Portal).
- **Kiểm Thử & Biên Dịch**:
  - Toàn bộ solution biên dịch thành công 100% (**0 Error, 0 Warning**).
