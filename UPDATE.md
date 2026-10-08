# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [08/10/2026] - Chuyển Đổi Mô Hình Giảng Dạy Offline: Gỡ Bỏ Toàn Bộ 7/7 APIs Phân Hệ Live Streaming (LiveSessions)

- **Gỡ Bỏ Hoàn Toàn Tầng API Controller (`LiveSessionsController.cs`)**:
  - Xóa bỏ `LiveSessionsController.cs` và 7/7 endpoints phục vụ giảng dạy trực tuyến (`POST /live-sessions`, `GET /my-schedule`, `POST /{sessionId}/join`, `POST /{sessionId}/attendance`, `GET /teacher-schedule`, `PUT /{sessionId}/recording`, `PUT /{sessionId}/cancel`).
  - Hệ thống chuyển đổi trọng tâm sang hình thức giảng dạy trực tiếp tại cơ sở (offline), không còn duy trì phân hệ live stream trực tuyến.
- **Dọn Sạch Triệt Để Tầng Application (CQRS LiveSessions)**:
  - Xóa sạch 100% thư mục `Features/LiveSessions` bao gồm toàn bộ Commands, Queries, Handlers, Validators và DTOs liên quan đến LiveSession.
- **Tái Cấu Trúc Repository Phân Công Giáo Viên (`AssignTeacher`)**:
  - Bổ sung `GetClassByIdAsync` và `SaveChangesAsync` vào `IClassEnrollmentRepository` và triển khai tại `ClassEnrollmentRepository`.
  - Cập nhật `AssignTeacherCommandHandler` sang inject `IClassEnrollmentRepository`, giải phóng hoàn toàn sự phụ thuộc vào `ILiveSessionRepository`.
  - Gỡ bỏ hoàn toàn `ILiveSessionRepository.cs`, `LiveSessionRepository.cs` và đăng ký Scoped trong `DependencyInjection.cs`.
- **Đồng Bộ Kiến Trúc & Kiểm Thử Vận Hành**:
  - Cập nhật nhật ký tiến độ [`docs/daily.md`](./docs/daily.md), bảng theo dõi [`docs/process.md`](./docs/process.md) và báo cáo nghiệm thu [`docs/architecture_acceptance.md`](./docs/architecture_acceptance.md).
  - Toàn bộ Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
