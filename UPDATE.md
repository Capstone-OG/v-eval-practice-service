# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [11/11/2026] - Bổ Sung Tuyến Đường Alias Nhận Webhook IPN PayOS Trực Tiếp (`/api/payments/payos-webhook`) & Khắc Phục Lỗi 404 Gateway

- **Bổ Sung Tuyến Đường Alias Nhận Webhook IPN PayOS ([`PaymentsController.cs`](./V-Eval-Practice_Service.API/Controllers/PaymentsController.cs))**:
  - Cổng thanh toán trực tuyến PayOS theo chuẩn tài liệu sẽ gửi Webhook IPN vào đường dẫn gốc `/api/payments/payos-webhook`.
  - Trước đây, controller chỉ cấu hình route tương đối `[HttpPost("payos-webhook")]` dưới tiền tố lớp `[Route("api/practice/payments")]`.
  - Bổ sung thuộc tính định tuyến tuyệt đối `[HttpPost("/api/payments/payos-webhook")]` cho phương thức `ProcessPayOsWebhook`, cho phép controller tiếp nhận đồng thời cả 2 định dạng đường dẫn IPN từ PayOS.
  - Phối hợp cùng API Gateway route `practice-payments-route` (`/api/payments/{**catch-all}`), giải quyết triệt để lỗi `HTTP 404 Not Found` khi PayOS gửi kết quả giao dịch về hệ thống.

- **Kiểm Thử & Biên Dịch**:
  - `POST /api/payments/payos-webhook` qua API Gateway xác thực chữ ký PayOS chuẩn xác (`{"success":false,"message":"Invalid signature"}`).
  - Toàn bộ solution `V-Eval-Practice_Service.sln` (.NET 9 SDK) biên dịch sạch `0 Error`.
