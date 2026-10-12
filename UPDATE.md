# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [12/10/2026] - Khởi Động Core Flow 3 (Module 3): Nền Tảng CSDL Giám Sát & API 1 Khởi Tạo Bài Thi Thử Chuẩn Hóa (StartMockExam)

- **Mở Rộng Mô Hình CSDL & Thực Thể Giám Sát Liêm Chính Học Thuật**:
  - Mở rộng thực thể [`ExamSubmission.cs`](./V-Eval-Practice_Service.Domain/Entities/ExamSubmission.cs): Tái sử dụng bảng `ExamSubmissions` (`ExamType = "MOCK_EXAM"`), bổ sung 4 trường giám sát liêm chính: `TabDepartureCount`, `IntegrityStatus`, `MentorVerdict`, `MentorNotes` và navigation collection `ProctoringSnapshots`.
  - Tạo thực thể [`ProctoringSnapshot.cs`](./V-Eval-Practice_Service.Domain/Entities/ProctoringSnapshot.cs): Lưu trữ ảnh chụp định kỳ từ webcam phục vụ hậu kiểm danh tính người thi.
  - Cấu hình Fluent API trong [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs) và cập nhật DDL bootstrapping tự động trong [`Program.cs`](./V-Eval-Practice_Service.API/Program.cs).
  - Triển khai repository [`IMockExamRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IMockExamRepository.cs) và [`MockExamRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/MockExamRepository.cs).
- **Hiện Thực API 1: Khởi Tạo Phiên Thi Thử Chuẩn Hóa 120 Câu (StartMockExam)**:
  - Khởi tạo chuỗi CQRS: [`StartMockExamDtos.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/DTOs/StartMockExamDtos.cs), `StartMockExamCommand.cs`, `StartMockExamCommandValidator.cs` và [`StartMockExamCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/MockExams/Commands/StartMockExam/StartMockExamCommandHandler.cs).
  - Tích hợp Content Service qua gRPC lấy 120 câu hỏi bảo mật (ẩn đáp án đúng), kích hoạt đồng hồ đếm ngược 150 phút (9000 giây) và thiết lập cơ chế khôi phục phiên thi đang dang dở.
  - Tạo Controller [`MockExamsController.cs`](./V-Eval-Practice_Service.API/Controllers/MockExamsController.cs) với endpoint `POST /api/practice/mock-exams/{examId}/start`.
- **Kiểm Thử & Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
