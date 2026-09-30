# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [30/09/2026] - Triển Khai Hoàn Thiện APIs 12, 13, 14, 15: Điểm Danh Chuyên Cần, Thời Khóa Biểu Giảng Dạy, Video Ghi Hình & Hủy Buổi Học Trực Tuyến

- **API 12: Giáo Viên Điểm Danh Chuyên Cần Cho Học Sinh (`POST /api/v1/practice/live-sessions/{sessionId}/attendance`)**:
  - Khởi tạo DTOs [`TeacherAttendanceDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/TeacherAttendanceDtos.cs): `StudentAttendanceItemDto`, `TeacherAttendanceRequestDto`, `TeacherAttendanceResponseDto`.
  - Xây dựng FluentValidation `TeacherAttendanceCommandValidator` kiểm tra tính toàn vẹn dữ liệu.
  - Triển khai `TeacherAttendanceCommand` và [`TeacherAttendanceCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/TeacherAttendance/TeacherAttendanceCommandHandler.cs): cập nhật bản ghi `LiveSessionAttendance` với trạng thái chính thức `ATTENDED` hoặc `ABSENT`. Bảo lưu `JoinedAt` do học sinh ghi nhận, chặn điểm danh buổi học đã bị hủy `CANCELLED` (`400 Bad Request`).
- **API 13: Lấy Thời Khóa Biểu Giảng Dạy Của Giáo Viên (`GET /api/v1/practice/live-sessions/teacher-schedule`)**:
  - Bổ sung phương thức `GetSessionsForTeacherAsync`, `TeacherExistsAsync` và `GetEnrolledStudentCountByClassIdAsync` vào [`ILiveSessionRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILiveSessionRepository.cs) và [`LiveSessionRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LiveSessionRepository.cs).
  - Khởi tạo DTOs [`TeacherScheduleDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/TeacherScheduleDtos.cs), `GetTeacherScheduleQuery.cs`, `GetTeacherScheduleQueryValidator.cs` và [`GetTeacherScheduleQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Queries/GetTeacherSchedule/GetTeacherScheduleQueryHandler.cs).
  - Nghiệp vụ: Xác thực giáo viên tồn tại (`404 Not Found` `TeacherNotFound`), lấy toàn bộ buổi Live Q&A phân công, thống kê sĩ số học sinh ghi danh, số tham gia (`ATTENDED`), số vắng mặt (`ABSENT`), link phòng họp và link video ghi hình.
- **API 14: Cập Nhật Video Ghi Hình Buổi Live Q&A (`PUT /api/v1/practice/live-sessions/{sessionId}/recording`)**:
  - Khởi tạo DTOs [`UpdateLiveSessionRecordingDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/UpdateLiveSessionRecordingDtos.cs), `UpdateLiveSessionRecordingCommand.cs`, `UpdateLiveSessionRecordingCommandValidator.cs` và [`UpdateLiveSessionRecordingCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/UpdateRecording/UpdateLiveSessionRecordingCommandHandler.cs).
  - Nghiệp vụ: Kiểm tra URL hợp lệ, chặn cập nhật khi buổi học đã bị hủy `CANCELLED` (`400 Bad Request`), lưu `RecordingUrl`, đánh dấu `IsRecorded = true` và chuyển trạng thái sang `COMPLETED` để học sinh xem lại bài giảng.
- **API 15: Giáo Viên / Giáo Vụ Hủy Buổi Học Trực Tuyến Khi Bận Đột Xuất (`PUT /api/v1/practice/live-sessions/{sessionId}/cancel`)**:
  - Khởi tạo DTOs [`CancelLiveSessionDtos.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/CancelLiveSessionDtos.cs), `CancelLiveSessionCommand.cs`, `CancelLiveSessionCommandValidator.cs` và [`CancelLiveSessionCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/CancelLiveSession/CancelLiveSessionCommandHandler.cs).
  - Nghiệp vụ: Chặn xóa vật lý bản ghi (buổi học do Academic Manager tạo, bảo lưu lịch sử đào tạo). Kiểm tra trạng thái đã `COMPLETED` hoặc đã `CANCELLED` (`400 Bad Request`), cập nhật trạng thái sang `CANCELLED` kèm lý do hủy `reason`. Khi đã hủy, ngăn chặn toàn bộ thao tác Join phòng (API 11), điểm danh (API 12) và gắn video ghi hình (API 14).
- **Chuẩn Hóa Đồng Bộ Route API (`api/practice/...`) Khớp Với API Gateway**:
  - Loại bỏ tiền tố `v1` khỏi toàn bộ các Controller trong Practice Service ([`ClassesController.cs`](./V-Eval-Practice_Service.API/Controllers/ClassesController.cs), [`DiagnosticSubmissionsController.cs`](./V-Eval-Practice_Service.API/Controllers/DiagnosticSubmissionsController.cs), [`LiveSessionsController.cs`](./V-Eval-Practice_Service.API/Controllers/LiveSessionsController.cs), [`RoadmapsController.cs`](./V-Eval-Practice_Service.API/Controllers/RoadmapsController.cs)).
  - Đồng bộ 100% với cấu hình YARP Reverse Proxy của API Gateway (`/api/practice/{**catch-all}`).
- **Tầng API Controller ([`LiveSessionsController.cs`](./V-Eval-Practice_Service.API/Controllers/LiveSessionsController.cs))**:
  - Khởi tạo 4 endpoints: `[HttpPost("{sessionId:guid}/attendance")]`, `[HttpGet("teacher-schedule")]`, `[HttpPut("{sessionId:guid}/recording")]`, `[HttpPut("{sessionId:guid}/cancel")]`.
- **Kiểm Thử Vận Hành Trực Tiếp (Live End-to-End Test)**:
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kịch bản API 12: Điểm danh 2 học sinh (`1111...` ATTENDED, `2222...` ABSENT) cho Session `2a196c82...` -> `200 OK`, `totalAttended: 1`, `totalAbsent: 1`. Chặn điểm danh session đã hủy -> `400 Bad Request`.
  - Kịch bản API 13: Tra cứu lịch dạy của giáo viên `99999999-9999-9999-9999-999999999999` -> `200 OK`, trả về 5 buổi Live đầy đủ số liệu sĩ số lớp, số tham gia, số vắng mặt. Tra cứu giáo viên không tồn tại -> `404 Not Found` (`TeacherNotFound`).
  - Kịch bản API 14: Cập nhật URL ghi hình -> `200 OK`, `isRecorded: true`, `status: "COMPLETED"`. Chặn cập nhật session đã hủy -> `400 Bad Request`.
  - Kịch bản API 15: Giáo viên hủy buổi học `8ebfe3ee...` vì bận công tác -> `200 OK`, trạng thái chuyển sang `CANCELLED`. Bấm hủy lại -> `400 Bad Request`. Học sinh gọi API 11 Join -> `400 Bad Request` ("Buổi học này đã bị hủy bỏ").
  - Kịch bản xác thực chéo API 10: Học sinh `1111...` tra cứu lịch thấy ngay trạng thái `ATTENDED`, link video recording và trạng thái `COMPLETED`.
