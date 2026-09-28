# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [28/09/2026] - Triển Khai Xử Lý Kịch Bản Ngoại Lệ (Unhappy Case 2): Khóa Bài Thi Quá Hạn 24h & Chống Gian Lận Nộp Lại

- **Kiểm Định Hết Hạn 24 Giờ & Tự Động Khóa Bài Thi (`SubmitDiagnosticCommandHandler.cs`)**:
  - Triển khai cơ chế kiểm tra thời gian làm bài: Nếu học sinh bỏ dở bài kiểm tra quá 24 giờ (`(DateTime.UtcNow - StartedAt).TotalHours > 24` hoặc tổng `TimeSpentSeconds > 86400`), hệ thống tự động đánh dấu phiên làm bài với `Status = "EXPIRED"`.
  - Tự động lưu bản ghi hết hạn vào bảng `ExamSubmissions` với điểm số bằng 0, không tính toán tham số năng lực $\theta_0$ nhằm bảo vệ độ tin cậy của mô hình psychometrics.
  - Trả về mã lỗi chuẩn RFC 7807 `Exam.Expired`, hướng dẫn học sinh làm lại bài chẩn đoán ngẫu nhiên mới.
- **Chống Gian Lận Nộp Lại Đề Thi Đã Khóa / Đã Hoàn Thành**:
  - Tự động kiểm tra lịch sử nộp bài: Nếu đề thi đã từng bị đánh dấu `EXPIRED`, hệ thống lập tức từ chối và trả về lỗi `Exam.Locked`.
  - Nếu đề thi đã được hoàn thành trước đó (`COMPLETED`), hệ thống từ chối nộp lại với mã `Exam.AlreadyCompleted` (HTTP 409 Conflict).
- **Mở Rộng DTO Phản Hồi (`DiagnosticSubmissionDtos.cs`)**:
  - Bổ sung trường `Status` vào `SubmitDiagnosticResponseDto` và `DiagnosticSubmissionSummaryDto`, đồng bộ trạng thái bài thi (`COMPLETED`, `EXPIRED`) trên toàn bộ các endpoint tra cứu.
- **Kiểm Thử Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).