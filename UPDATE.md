# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [30/09/2026] - Triển Khai Hoàn Thiện APIs 12, 13, 14: Điểm Danh Chuyên Cần, Thời Khóa Biểu Giảng Dạy & Cập Nhật Video Ghi Hình Buổi Live

- **API 12: Giáo Viên Điểm Danh Chuyên Cần Cho Học Sinh (`POST /api/v1/practice/live-sessions/{sessionId}/attendance`)**:
  - Khởi tạo DTOs [`TeacherAttendanceDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/TeacherAttendanceDtos.cs): `StudentAttendanceItemDto`, `TeacherAttendanceRequestDto`, `TeacherAttendanceResponseDto`.
  - Xây dựng FluentValidation `TeacherAttendanceCommandValidator` kiểm tra tính toàn vẹn dữ liệu.
  - Triển khai `TeacherAttendanceCommand` và [`TeacherAttendanceCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/TeacherAttendance/TeacherAttendanceCommandHandler.cs): cập nhật bản ghi `LiveSessionAttendance` với trạng thái chính thức `ATTENDED` (kèm `JoinedAt = UtcNow`) hoặc `ABSENT`.
- **API 13: Lấy Thời Khóa Biểu Giảng Dạy Của Giáo Viên (`GET /api/v1/practice/live-sessions/teacher-schedule`)**:
  - Bổ sung phương thức `GetSessionsForTeacherAsync` và `GetEnrolledStudentCountByClassIdAsync` vào [`ILiveSessionRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILiveSessionRepository.cs) và [`LiveSessionRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LiveSessionRepository.cs).
  - Khởi tạo DTOs [`TeacherScheduleDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/TeacherScheduleDtos.cs), `GetTeacherScheduleQuery.cs` và [`GetTeacherScheduleQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Queries/GetTeacherSchedule/GetTeacherScheduleQueryHandler.cs).
  - Nghiệp vụ: Lấy toàn bộ các buổi học Live Q&A của giáo viên phụ trách, sĩ số học sinh ghi danh của lớp, số học sinh đã tham gia (`ATTENDED`), số vắng mặt (`ABSENT`), link phòng họp và link video ghi hình.
- **API 14: Cập Nhật Video Ghi Hình Buổi Live Q&A (`PUT /api/v1/practice/live-sessions/{sessionId}/recording`)**:
  - Khởi tạo DTOs [`UpdateLiveSessionRecordingDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/UpdateLiveSessionRecordingDtos.cs), `UpdateLiveSessionRecordingCommand.cs`, `UpdateLiveSessionRecordingCommandValidator.cs` và [`UpdateLiveSessionRecordingCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/UpdateRecording/UpdateLiveSessionRecordingCommandHandler.cs).
  - Nghiệp vụ: Kiểm tra URL hợp lệ, lưu `RecordingUrl`, đánh dấu `IsRecorded = true` và chuyển trạng thái sang `COMPLETED` để học sinh xem lại bài giảng.
- **Tầng API Controller ([`LiveSessionsController.cs`](./V-Eval-Practice_Service.API/Controllers/LiveSessionsController.cs))**:
  - Khởi tạo 3 endpoints: `[HttpPost("{sessionId:guid}/attendance")]`, `[HttpGet("teacher-schedule")]`, `[HttpPut("{sessionId:guid}/recording")]`.
- **Kiểm Thử Vận Hành Trực Tiếp (Live End-to-End Test)**:
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kịch bản API 12: Điểm danh 2 học sinh (`1111...` ATTENDED, `2222...` ABSENT) cho Session `2a196c82...` -> `200 OK`, `totalAttended: 1`, `totalAbsent: 1`.
  - Kịch bản API 13: Tra cứu lịch dạy của giáo viên `99999999-9999-9999-9999-999999999999` -> `200 OK`, trả về 5 buổi Live đầy đủ số liệu sĩ số lớp, số tham gia, số vắng mặt.
  - Kịch bản API 14: Cập nhật URL ghi hình -> `200 OK`, `isRecorded: true`, `status: "COMPLETED"`.
  - Kịch bản xác thực chéo API 10: Học sinh `1111...` tra cứu lịch thấy ngay trạng thái `ATTENDED`, link video recording và trạng thái `COMPLETED`.
