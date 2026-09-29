# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [29/09/2026] - Triển Khai Hoàn Thiện Giai Đoạn 3: Quản Lý Buổi Học Live Q&A, Phân Công Giáo Viên, Thời Khóa Biểu & Điểm Danh Trực Tuyến (APIs 8, 9, 10, 11)

- **API 8: Tạo Lịch Buổi Học Live Q&A Cho Lớp Học Cơ Sở (`POST /api/v1/practice/live-sessions`)**:
  - Khởi tạo Repository [`ILiveSessionRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILiveSessionRepository.cs) và [`LiveSessionRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LiveSessionRepository.cs) quản lý thực thể `LiveSessions` và `LiveSessionAttendance`. Đăng ký Scoped trong `DependencyInjection.cs`.
  - Khởi tạo CQRS: [`CreateLiveSessionDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/CreateLiveSessionDtos.cs), `CreateLiveSessionCommand.cs`, `CreateLiveSessionCommandValidator.cs`, [`CreateLiveSessionCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/CreateLiveSession/CreateLiveSessionCommandHandler.cs).
  - Nghiệp vụ: Xác thực lớp học tồn tại, tự động kế thừa `TeacherId` của lớp nếu không truyền, sinh link phòng học `meeting_url` nếu chưa có, lưu bản ghi trạng thái `SCHEDULED`.
- **API 9: Phân Công Hoặc Điều Chuyển Giáo Viên Phụ Trách Lớp Học Cơ Sở (`PUT /api/v1/practice/classes/{classId}/assign-teacher`)**:
  - Khởi tạo CQRS: [`AssignTeacherDtos.cs`](./V-Eval-Practice_Service.Application/Features/Classes/DTOs/AssignTeacherDtos.cs), `AssignTeacherCommand.cs`, `AssignTeacherCommandValidator.cs`, [`AssignTeacherCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Classes/Commands/AssignTeacher/AssignTeacherCommandHandler.cs).
  - Tầng API: [`ClassesController.cs`](./V-Eval-Practice_Service.API/Controllers/ClassesController.cs) endpoint `[HttpPut("{classId:guid}/assign-teacher")]`.
  - Nghiệp vụ: Cập nhật `TeacherId`, `AssignedBy` (từ Header `X-User-Id`), `AssignedAt = UtcNow` cho thực thể `Class`.
- **API 10: Lấy Thời Khóa Biểu Các Buổi Live Q&A Của Lớp Cơ Sở Học Sinh Ghi Danh (`GET /api/v1/practice/live-sessions/my-schedule`)**:
  - Khởi tạo CQRS: [`GetMyLiveScheduleDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/GetMyLiveScheduleDtos.cs), `GetMyLiveScheduleQuery.cs`, [`GetMyLiveScheduleQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Queries/GetMyLiveSchedule/GetMyLiveScheduleQueryHandler.cs).
  - Tầng API: [`LiveSessionsController.cs`](./V-Eval-Practice_Service.API/Controllers/LiveSessionsController.cs) endpoint `[HttpGet("my-schedule")]`.
  - Nghiệp vụ: Truy vấn thông tin lớp học học sinh đang ghi danh (`ClassEnrollments`), nạp danh sách các buổi học Live Q&A của lớp, bóc tách trạng thái điểm danh cá nhân (`ATTENDED`, `ABSENT`, `NOT_ATTENDED`), link video ghi hình và cờ `IsMakeupQuizPassed`.
- **API 11: Tham Gia Buổi Học Trực Tuyến Live Q&A & Ghi Nhận Dấu Vết Vào Lớp (`POST /api/v1/practice/live-sessions/{sessionId}/join`)**:
  - Khởi tạo CQRS: [`JoinLiveSessionDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/JoinLiveSessionDtos.cs), `JoinLiveSessionCommand.cs`, `JoinLiveSessionCommandValidator.cs`, [`JoinLiveSessionCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/JoinLiveSession/JoinLiveSessionCommandHandler.cs).
  - Tầng API: [`LiveSessionsController.cs`](./V-Eval-Practice_Service.API/Controllers/LiveSessionsController.cs) endpoint `[HttpPost("{sessionId:guid}/join")]`.
  - Nghiệp vụ: Cung cấp đường dẫn phòng học trực tuyến (`MeetingUrl`), ghi nhận thời điểm vào lớp `JoinedAt = UtcNow`. Bảo lưu độc quyền điểm danh chuyên cần (`ATTENDED` hoặc `ABSENT`) cho Giảng viên tại API 12 (không tự ý ghi đè trạng thái điểm danh khi học sinh chỉ mới click vào link phòng học).
- **Kiểm Thử Vận Hành Trực Tiếp (Live End-to-End Test)**:
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kịch bản API 9: Phân công giáo viên `99999999-9999-9999-9999-999999999999` cho lớp `33333333-3333-3333-3333-333333333333` -> `200 OK`.
  - Kịch bản API 8: Tạo buổi Live Q&A chuyên sâu -> `201 Created` tự động kế thừa `TeacherId`.
  - Kịch bản API 10: Tra cứu thời khóa biểu -> `200 OK` hiển thị đầy đủ danh sách các buổi Live và trạng thái điểm danh.
  - Kịch bản API 11: Học sinh `11111111-1111-1111-1111-111111111111` tham gia buổi Live -> `200 OK`, trả về URL phòng học, lưu vết thời điểm `JoinedAt`, bảo lưu trạng thái chờ Giảng viên đánh giá chuyên cần tại API 12.
