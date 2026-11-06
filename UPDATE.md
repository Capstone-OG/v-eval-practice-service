# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [06/11/2026] - Loại Bỏ Triệt Để Dữ Liệu Học Sinh Mẫu Mặc Định (Phạm Minh Hoàng), Trả Về Dữ Liệu Rỗng Thực Tế Cho Cổng Phụ Huynh & Khắc Phục Ngắt Đoạn SSE Socratic

- **Loại Bỏ 100% Mock Học Sinh Mẫu Trong Cổng Phụ Huynh ([`GetParentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQueryHandler.cs))**:
  - Xóa bỏ hoàn toàn khối lệnh tự động tiêm học sinh mẫu `"Phạm Minh Hoàng"` (`3fa85f64-5717-4562-b3fc-2c963f66afa6`) khi phụ huynh chưa liên kết với học sinh trong CSDL.
  - Trả về danh sách `children: []` rỗng thực tế khi chưa có quan hệ liên kết trong bảng `v_eval_identity."ParentStudentRelations"`, kích hoạt giao diện chờ liên kết 3 cách sạch sẽ trên Frontend.
  - Chuẩn hóa các chỉ số tự học về `0.0` thay cho các số liệu giả định cũ (`82.4`, `14.5` giờ, `12` ngày streak).
- **Khắc Phục Lỗi Ngắt Quãng SSE Bằng Cơ Chế Kênh Bất Đồng Bộ System.Threading.Channels ([`AiTutorClient.cs`](./V-Eval-Practice_Service.Infrastructure/HttpClients/AiTutorClient.cs))**:
  - Tái cấu trúc phương thức `StreamSocraticGuidanceAsync` sử dụng mô hình Producer-Consumer với `Channel.CreateUnbounded<string>()`.
  - Tự động bổ sung `GenerateStreamFallback` khi mất kết nối trước khi nhận token, triệt tiêu 100% tình trạng treo dấu ba chấm `...` trên Web Client.
- **Kiểm Thử & Vận Hành**:
  - Biên dịch toàn bộ solution `V-Eval-Practice_Service.sln` (.NET 9 SDK) đạt chuẩn 100% sạch (0 Error).
