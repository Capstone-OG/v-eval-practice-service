# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [15/11/2026] - Nâng Cấp Pipeline gRPC Làm Giàu Dữ Liệu Khảo Thí Tự Động & Khắc Phục Triệt Để Placeholder Trong Lịch Sử Làm Bài

- **Mở Rộng DTO & Hợp Đồng gRPC Client ([`IContentGrpcClient.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/IContentGrpcClient.cs), [`ContentGrpcClient.cs`](./V-Eval-Practice_Service.Infrastructure/GrpcClients/ContentGrpcClient.cs), [`content.proto`](./V-Eval-Practice_Service.Infrastructure/Protos/content.proto))**:
  - Mở rộng `ExamQuestionKeyDto` tiếp nhận 5 trường dữ liệu từ Content Service: `Content`, `Options`, `Explanation`, `PassageContent`, `PassageTitle`.
  - Cập nhật client gRPC `GetExamAnswerKeysAsync` ánh xạ liền mạch dữ liệu nhị phân từ Content Service sang DTO nội bộ.
- **Làm Giàu Chi Tiết Dữ Liệu Bài Nộp Khảo Thí ([`DiagnosticSubmissionDtos.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/DTOs/DiagnosticSubmissionDtos.cs), [`GetDiagnosticSubmissionById.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Queries/GetDiagnosticSubmissionById/GetDiagnosticSubmissionById.cs), [`SubmitDiagnosticCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Commands/SubmitDiagnostic/SubmitDiagnosticCommandHandler.cs))**:
  - Bổ sung `Content`, `Options`, `Explanation`, `PassageContent`, `PassageTitle` vào `QuestionResultDto`.
  - Trong `GetDiagnosticSubmissionByIdHandler`:
    - Tự động tra cứu đáp án và nội dung câu hỏi qua gRPC bằng `submission.ExamId`.
    - Bổ sung cơ chế tra cứu chuẩn hóa qua Default Diagnostic Exam ID (`11111111-1111-1111-1111-111111111111`) nếu đề thi cũ trong quá khứ không còn trong ngân hàng câu hỏi.
    - Hỗ trợ fallback thông minh ghép theo thứ tự mảng (`orderedKeys[idx]`) bảo đảm thứ tự câu luôn đạt chuẩn (`idx + 1`), chấm dứt hoàn toàn lỗi `"Câu 0:"` và `"Bloom 0/6"`.
  - Trong `SubmitDiagnosticCommandHandler`: Ánh xạ trực tiếp đầy đủ nội dung câu hỏi và các phương án vào phản hồi nộp bài ngay sau khi học sinh bấm nộp bài.
- **Kiểm Thử & Nghiệm Thu**:
  - Biên dịch toàn bộ solution `V-Eval-Practice_Service.sln` đạt kết quả sạch: **0 Warning, 0 Error**.
  - Kiểm thử trực tiếp API endpoint `GET /api/practice/diagnostic-submissions/{id}`: Phản hồi HTTP 200 kèm trọn vẹn 30 câu hỏi với LaTeX, 4 phương án A/B/C/D, ngữ cảnh bài đọc và kỹ năng liên kết.
