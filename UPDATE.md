# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [06/11/2026] - Triển Khai Background Worker Quét Cảnh Báo Sớm BR-04 Tự Động, Bộ Điều Phối Thông Báo Đa Kênh Email SMTP / Mobile Push FCM (Khớp 100% SDD Figure 56 & 57, Mục 07), Loại Bỏ Dữ Liệu Học Sinh Mẫu & Bảo Vệ SSE Socratic

- **Triển Khai Background Worker Quét Tự Động Định Kỳ Ngầm ([`EarlyWarningSweepWorker.cs`](./V-Eval-Practice_Service.Infrastructure/BackgroundJobs/EarlyWarningSweepWorker.cs))**:
  - Kế thừa `BackgroundService` chạy ngầm định kỳ 30 phút/lần (với độ trễ khởi động 25s chống nghẽn dịch vụ).
  - Tự động quét toàn bộ học sinh đang hoạt động, nạp nhật ký tự học và chỉ số sẵn sàng để đánh giá nghiêm ngặt quy tắc cảnh báo sớm 3 cấp **BR-04** (`UNREALISTIC_GOAL`, `RED`, `YELLOW`).
  - Kiểm tra chống sinh trùng lặp trong 24 giờ, ghi nhận cảnh báo vào bảng `v_eval_practice."SystemAlerts"` và kích hoạt bắn thông báo ra bên ngoài.
- **Triển Khai Bộ Điều Phối Bắn Thông Báo Đa Kênh Ra Bên Ngoài ([`INotificationDispatcher.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Services/INotificationDispatcher.cs), [`NotificationDispatcher.cs`](./V-Eval-Practice_Service.Infrastructure/Services/NotificationDispatcher.cs))**:
  - **Email Kèm Giao Diện HTML Chuẩn V-Eval (MailKit / MimeKit 4.17.0)**: Soạn thảo email HTML thương hiệu responsive, hiển thị màu phân cấp rủi ro (Đỏ/Vàng/Tím), thông số điểm mục tiêu/dự báo/sẵn sàng, lời khuyên sư phạm và nút CTA dẫn thẳng vào Cổng Phụ Huynh / Học Viên. Gửi đồng thời tới Email học sinh và Email tất cả phụ huynh liên kết.
  - **Mobile Push (Firebase Cloud Messaging - FCM)**: Chuẩn bị payload thông báo di động chuẩn gửi tới topic `/topics/student_{id}` kèm deep link `/parent/dashboard`.
  - **Cơ Chế Phục Hồi Cao (Resilient Fallback Audit)**: Tự động ghi log kiểm toán có cấu trúc `[NOTIFICATION DISPATCHER - EMAIL AUDIT]` & `[NOTIFICATION DISPATCHER - FCM AUDIT]` khi chưa cấu hình máy chủ SMTP hoặc khóa FCM, triệt tiêu 100% nguy cơ crash worker.
- **Endpoint API Kích Hoạt Quét Cảnh Báo Sớm Theo Yêu Cầu ([`EarlyWarningsController.cs`](./V-Eval-Practice_Service.API/Controllers/EarlyWarningsController.cs))**:
  - Bổ sung `POST /api/practice/early-warnings/sweep?classId={guid}` cho phép Quản trị viên/Mentor kích hoạt quy trình quét tức thì.
- **Loại Bỏ 100% Mock Học Sinh Mẫu Trong Cổng Phụ Huynh ([`GetParentDashboardQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQueryHandler.cs))**:
  - Xóa bỏ hoàn toàn khối lệnh tự động tiêm học sinh mẫu `"Phạm Minh Hoàng"` (`3fa85f64-5717-4562-b3fc-2c963f66afa6`) khi phụ huynh chưa liên kết với học sinh trong CSDL.
  - Trả về danh sách `children: []` rỗng thực tế khi chưa có quan hệ liên kết trong bảng `v_eval_identity."ParentStudentRelations"`, kích hoạt giao diện chờ liên kết 3 cách sạch sẽ trên Frontend.
  - Chuẩn hóa các chỉ số tự học về `0.0` thay cho các số liệu giả định cũ (`82.4`, `14.5` giờ, `12` ngày streak).
- **Khắc Phục Lỗi Ngắt Quãng SSE Bằng Cơ Chế Kênh Bất Đồng Bộ System.Threading.Channels ([`AiTutorClient.cs`](./V-Eval-Practice_Service.Infrastructure/HttpClients/AiTutorClient.cs))**:
  - Tái cấu trúc phương thức `StreamSocraticGuidanceAsync` sử dụng mô hình Producer-Consumer với `Channel.CreateUnbounded<string>()`.
  - Tự động bổ sung `GenerateStreamFallback` khi mất kết nối trước khi nhận token, triệt tiêu 100% tình trạng treo dấu ba chấm `...` trên Web Client.
- **Kiểm Thử & Vận Hành**:
  - Biên dịch toàn bộ solution `V-Eval-Practice_Service.sln` (.NET 9 SDK) đạt chuẩn 100% sạch (0 Warning, 0 Error).
  - Toàn bộ 12/12 kiểm thử tự động xUnit đạt Passed 100%.
  - Xác nhận vận hành thành công trên tiến trình thực tế: Worker khởi chạy ngầm, quét dữ liệu học sinh, lưu `SystemAlert` và bắn thông báo kiểm toán an toàn.
