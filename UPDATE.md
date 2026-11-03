# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [03/11/2026] - Triển Khai Phân Hệ Thanh Toán PayOS (VietQR), Quản Lý Đăng Ký Khóa Học & Kích Hoạt Lộ Trình PLAR Nguyên Khối (ACID Transaction)

- **Mở Rộng Domain Entities & Ánh Xạ EF Core (`v_eval_enrollment`)**:
  - Bổ sung 4 thực thể cốt lõi thuộc Bounded Context `v_eval_enrollment`:
    - `Payment`: Quản lý giao dịch nộp học phí, hỗ trợ tích hợp PayOS (`order_code`, `checkout_url`, `payment_link_id`, thông tin tài khoản đối ứng `counter_account_*`).
    - `PaymentTransaction`: Nhật ký kiểm toán tài chính và lưu vết Webhook IPN (`raw_payload`, `signature`, `ip_address`).
    - `CourseRegistration`: Đăng ký khóa học của học sinh, quản lý trạng thái (`PENDING_PAYMENT`, `ACTIVE`, `COMPLETED`), số lần học lại (`attempt_no`).
    - `Course`: Danh mục khóa học của cơ sở đào tạo (học phí, thời lượng, sĩ số tối đa).
  - Cập nhật `RoadmapNode` bổ sung liên kết `daily_exam_id` (đề thi luyện tập hàng ngày PLAR) và `ClassEnrollment` bổ sung `registration_id`.
  - Cấu hình hoàn chỉnh mapping EF Core trong `PracticeDbContext.cs` với Schema `v_eval_enrollment` và các chỉ mục toàn vẹn duy nhất (`uq_courseregistrations`, `order_code`).

- **Xây Dựng Tầng Application (CQRS & Transaction Abstraction)**:
  - Thiết kế `IExecutionTransaction` trừu tượng hóa giao dịch CSDL (ACID), bảo toàn nguyên lý Clean Architecture không phụ thuộc EF Core trực tiếp tại tầng Use Case.
  - Định nghĩa hợp đồng repository `IPaymentRepository` và dịch vụ cổng thanh toán `IPayOsPaymentService`.
  - `CreatePaymentLinkCommand`: Kiểm tra điều kiện khóa học, sinh `orderCode` nguyên dương an toàn JavaScript (`Random.Shared.NextInt64(1000000000, 9007199254740991)`), rút gọn mô tả <= 25 ký tự không dấu, gọi PayOS API và trả về link thanh toán VietQR.
  - `ProcessPayOsWebhookCommand`:
    - Xác thực chữ ký số HMAC-SHA512.
    - Thực thi khối Database Transaction nguyên khối (ACID): Kiểm tra chống trùng (Idempotency Guard) ➔ Cập nhật `Payment` (`SUCCESS`) ➔ Kích hoạt `CourseRegistration` (`ACTIVE`) ➔ Tự động xếp lớp `ClassEnrollment` ➔ Tự động sinh `LearningRoadmap` chu trình 4 bước PLAR và mở khóa chặng 1 (`IN_PROGRESS`) ➔ Ghi vết kiểm toán `PaymentTransactions` ➔ Commit đồng bộ.
  - Truy vấn `GetPaymentStatusQuery` (tự động đồng bộ trạng thái trực tiếp với PayOS khi Webhook trễ) và `GetPaymentTransactionsQuery`.

- **Triển Khai Tầng Infrastructure & API**:
  - Hiện thực `PayOsPaymentService.cs` tích hợp thư viện `payOS` 2.1.0, cấu hình HttpClient an toàn và xử lý Webhook.
  - Hiện thực `PaymentRepository.cs` với `EfExecutionTransaction`.
  - Đăng ký DI trong `DependencyInjection.cs`.
  - Viết `PaymentsController.cs` tại đường dẫn `/api/practice/payments`:
    - `POST /create-link` [Authorize]
    - `POST /payos-webhook` [AllowAnonymous] (Trả về `{ success = true/false }` chuẩn hóa cho PayOS Webhook Registry)
    - `GET /status/{orderCode}` [Authorize]
    - `GET /transactions/{paymentId}` [Authorize]
  - Cập nhật cấu hình PayOS trong `appsettings.json`.

- **Kiểm Thử Đơn Vị (Unit Testing)**:
  - Bổ sung 8 bài kiểm thử chuyên sâu trong `Commands/Payments/`: `CreatePaymentLinkCommandHandlerTests` (3 tests) và `ProcessPayOsWebhookCommandHandlerTests` (5 tests - bao gồm Happy Path, Idempotency, Tampered Signature, Database Rollback on Exception).
  - Kết quả kiểm thử: **12/12 tests PASS 100%**, `dotnet build` đạt 0 Warnings, 0 Errors.
