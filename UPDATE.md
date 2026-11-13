# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [13/11/2026] - Khắc Phục Ngoại Lệ gRPC NotFound Cho Đề Thi Cũ Trong ContentGrpcClient & Mở Rộng DTO Tóm Tắt Kèm Chỉ Số Năng Lực Theta0

- **Xử Lý Ngoại Lệ `RpcException(StatusCode.NotFound)` Cho Đề Thi Cũ ([`ContentGrpcClient.cs`](./V-Eval-Practice_Service.Infrastructure/GrpcClients/ContentGrpcClient.cs))**:
  - Khi học sinh truy vấn lịch sử làm bài thi chẩn đoán cũ (`ExamSubmissions`), mã đề thi `ExamId` có thể không còn câu hỏi trong bảng `ExamQuestions` của Content Service, khiến Content Service gRPC ném ngoại lệ `NotFound`.
  - Thay vì để ngoại lệ văng ra ngoài gây lỗi `500 Internal Server Error`, `ContentGrpcClient.GetExamAnswerKeysAsync` được bổ sung khối `catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)` và `catch (Exception ex)`, ghi `LogWarning` và trả về `new Dictionary<Guid, ExamQuestionKeyDto>()` rỗng.
  - Bổ sung khối `try/catch` bọc an toàn trong [`GetDiagnosticSubmissionById.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Queries/GetDiagnosticSubmissionById/GetDiagnosticSubmissionById.cs) nhằm đảm bảo kết quả bài làm của học sinh (điểm, thời gian, đáp án chọn) luôn trả về thành công `200 OK`.
- **Mở Rộng DTO Tóm Tắt Kèm `Theta0` & `PlacementClass` ([`DiagnosticSubmissionDtos.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/DTOs/DiagnosticSubmissionDtos.cs), [`GetDiagnosticSubmissionsByStudent.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Queries/GetDiagnosticSubmissionsByStudent/GetDiagnosticSubmissionsByStudent.cs))**:
  - Bổ sung `Theta0` và `PlacementClass` vào `DiagnosticSubmissionSummaryDto`, trích xuất trực tiếp từ cột có sẵn trong `ExamSubmissions` khi truy vấn danh sách bài nộp của học sinh.
  - Giúp Frontend hiển thị biểu đồ xu hướng năng lực IRT tức thì mà không cần ép buộc nạp tuần tự N lượt API chi tiết.
- **Kiểm Thử & Nghiệm Thu**:
  - Biên dịch toàn bộ solution `V-Eval-Practice_Service.sln` đạt `0 Warning, 0 Error`.
  - Toàn bộ 14/14 kiểm thử tự động xUnit đạt Passed 100%.
  - Kiểm thử trực tiếp lệnh curl với bài nộp cũ `2ad7e238-fe88-4a1b-9431-7dc70e71aec8` (đề `7936bf7a...` không tồn tại ở Content Service) phản hồi `200 OK` mượt mà, triệt tiêu hoàn toàn lỗi 500.
