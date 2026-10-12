# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [12/10/2026] - Triển Khai Core Flow 3 (Module 3): CSDL Giám Sát, API 1 StartMockExam, API 2 LogTabDeparture, API 3 UploadSnapshot & API 4 SubmitMockExam

- **Mở Rộng Mô Hình CSDL & Thực Thể Giám Sát Liêm Chính Học Thuật**:
  - Mở rộng thực thể [`ExamSubmission.cs`](./V-Eval-Practice_Service.Domain/Entities/ExamSubmission.cs): Tái sử dụng bảng `ExamSubmissions` (`ExamType = "MOCK_EXAM"`), bổ sung 4 trường giám sát liêm chính: `TabDepartureCount`, `IntegrityStatus`, `MentorVerdict`, `MentorNotes` và navigation collection `ProctoringSnapshots`.
  - Tạo thực thể [`ProctoringSnapshot.cs`](./V-Eval-Practice_Service.Domain/Entities/ProctoringSnapshot.cs): Lưu trữ ảnh chụp định kỳ từ webcam phục vụ hậu kiểm danh tính người thi.
  - Cấu hình Fluent API trong [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs) và cập nhật DDL bootstrapping tự động trong [`Program.cs`](./V-Eval-Practice_Service.API/Program.cs).
  - Triển khai repository [`IMockExamRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IMockExamRepository.cs) và [`MockExamRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/MockExamRepository.cs).
- **Hiện Thực API 1: Khởi Tạo Phiên Thi Thử Chuẩn Hóa 120 Câu (StartMockExam)**:
  - Khởi tạo chuỗi CQRS: [`StartMockExamDtos.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/DTOs/StartMockExamDtos.cs), `StartMockExamCommand.cs`, `StartMockExamCommandValidator.cs` và [`StartMockExamCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/Commands/StartMockExam/StartMockExamCommandHandler.cs).
  - Tích hợp Content Service qua gRPC lấy 120 câu hỏi bảo mật (ẩn đáp án đúng), kích hoạt đồng hồ đếm ngược 150 phút (9000 giây) và thiết lập cơ chế khôi phục phiên thi đang dang dở.
  - Endpoint: `POST /api/practice/mock-exams/{examId}/start`.
- **Hiện Thực API 2: Ghi Nhận Rời Tab & Cắm Cờ Gian Lận (LogTabDeparture - BR-20)**:
  - Khởi tạo chuỗi CQRS: [`LogTabDepartureDtos.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/DTOs/LogTabDepartureDtos.cs), `LogTabDepartureCommand.cs`, `LogTabDepartureCommandValidator.cs` và [`LogTabDepartureCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/Commands/LogTabDeparture/LogTabDepartureCommandHandler.cs).
  - Nghiệp vụ: Đếm số lần rời tab/màn hình (`TabDepartureCount += 1`). Khi số lần vi phạm vượt quá 3 lần (`> 3`), hệ thống tự động cắm cờ `IntegrityStatus = "FLAGGED_FOR_REVIEW"` để Mentor thẩm định.
  - Endpoint: `POST /api/practice/mock-exams/{submissionId}/log-tab-departure`.
- **Hiện Thực API 3: Lưu Ảnh Chụp Webcam Giám Sát Ngẫu Nhiên (UploadSnapshot)**:
  - Khởi tạo chuỗi CQRS: [`UploadSnapshotDtos.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/DTOs/UploadSnapshotDtos.cs), `UploadSnapshotCommand.cs`, `UploadSnapshotCommandValidator.cs` và [`UploadSnapshotCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/Commands/UploadSnapshot/UploadSnapshotCommandHandler.cs).
  - Nghiệp vụ: Lưu trữ URL ảnh chụp webcam định kỳ, mốc thời gian UTC và kết quả nhận diện khuôn mặt (`FaceDetected`) vào bảng `ProctoringSnapshots`.
  - Endpoint: `POST /api/practice/mock-exams/{submissionId}/upload-snapshot`.
- **Hiện Thực API 4: Nộp Bài Thi Thử, Chấm Điểm & Phân Luồng Giám Sát Liêm Chính (SubmitMockExam)**:
  - Khởi tạo chuỗi CQRS: [`SubmitMockExamDtos.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/DTOs/SubmitMockExamDtos.cs), `SubmitMockExamCommand.cs`, `SubmitMockExamCommandValidator.cs` và [`SubmitMockExamCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/Commands/SubmitMockExam/SubmitMockExamCommandHandler.cs).
  - Nghiệp vụ: Chấm điểm bài thi 120 câu theo thang điểm chuẩn hóa ĐGNL 1200 điểm, lưu vết từng câu vào `SubmissionAnswers`.
  - Phân luồng liêm chính: Nếu vi phạm rời màn hình (`FLAGGED_FOR_REVIEW` hoặc `TabDepartureCount > 3`), chuyển sang `UNDER_REVIEW` và `MentorVerdict = "PENDING"` chờ Mentor duyệt.
  - Khép kín vòng lặp sư phạm: Tự động nạp toàn bộ các câu trả lời sai vào Sổ tay lỗi sai cá nhân (`MistakeNotebook`) phục vụ chu kỳ lặp lại ngắt quãng SM-2, đồng thời cập nhật năng lực thành phần vào `LearningProfiles` phục vụ phân nhóm bàn học vi mô (Micro-groups 3-5 bạn) tuần tới.
  - Endpoint: `POST /api/practice/mock-exams/{submissionId}/submit`.
- **Kiểm Thử & Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
