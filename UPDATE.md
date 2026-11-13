# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [13/11/2026] - Khắc Phục Ngoại Lệ gRPC NotFound Cho Đề Thi Cũ, Mở Rộng DTO Kèm Chỉ Số Theta0 & Bổ Sung Tuyến Đường Alias Nhận Webhook IPN PayOS

- **Xử Lý Ngoại Lệ `RpcException(StatusCode.NotFound)` Cho Đề Thi Cũ ([`ContentGrpcClient.cs`](./V-Eval-Practice_Service.Infrastructure/GrpcClients/ContentGrpcClient.cs))**:
  - Khi học sinh truy vấn lịch sử làm bài thi chẩn đoán cũ (`ExamSubmissions`), mã đề thi `ExamId` có thể không còn câu hỏi trong bảng `ExamQuestions` của Content Service, khiến Content Service gRPC ném ngoại lệ `NotFound`.
  - Thay vì để ngoại lệ văng ra ngoài gây lỗi `500 Internal Server Error`, `ContentGrpcClient.GetExamAnswerKeysAsync` được bổ sung khối `catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)` và `catch (Exception ex)`, ghi `LogWarning` và trả về `new Dictionary<Guid, ExamQuestionKeyDto>()` rỗng.
  - Bổ sung khối `try/catch` bọc an toàn trong [`GetDiagnosticSubmissionById.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Queries/GetDiagnosticSubmissionById/GetDiagnosticSubmissionById.cs) nhằm đảm bảo kết quả bài làm của học sinh (điểm, thời gian, đáp án chọn) luôn trả về thành công `200 OK`.
- **Mở Rộng DTO Tóm Tắt Kèm `Theta0` & `PlacementClass` ([`DiagnosticSubmissionDtos.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/DTOs/DiagnosticSubmissionDtos.cs), [`GetDiagnosticSubmissionsByStudent.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Queries/GetDiagnosticSubmissionsByStudent/GetDiagnosticSubmissionsByStudent.cs))**:
  - Bổ sung `Theta0` và `PlacementClass` vào `DiagnosticSubmissionSummaryDto`, trích xuất trực tiếp từ cột có sẵn trong `ExamSubmissions` khi truy vấn danh sách bài nộp của học sinh.
  - Giúp Frontend hiển thị biểu đồ xu hướng năng lực IRT tức thì mà không cần ép buộc nạp tuần tự N lượt API chi tiết.
- **Bổ Sung Tuyến Đường Alias Nhận Webhook IPN PayOS ([`PaymentsController.cs`](./V-Eval-Practice_Service.API/Controllers/PaymentsController.cs))**:
  - Bổ sung thuộc tính định tuyến tuyệt đối `[HttpPost("/api/payments/payos-webhook")]` cho phương thức `ProcessPayOsWebhook`, cho phép controller tiếp nhận đồng thời cả 2 định dạng đường dẫn IPN từ PayOS.
- **Kiểm Thử & Nghiệm Thu**:
  - Biên dịch toàn bộ solution `V-Eval-Practice_Service.sln` đạt `0 Warning, 0 Error`.
  - Toàn bộ 14/14 kiểm thử tự động xUnit đạt Passed 100%.
