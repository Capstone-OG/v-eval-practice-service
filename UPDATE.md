# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [25/10/2026] - Chuyển Đổi Mô Hình Offline Cohort: Dọn Sạch Toàn Bộ LiveSessions, Đồng Bộ CSDL Supabase 47 Bảng & Chuẩn Hóa ERD Phương Án C (Hybrid Best-Practice)

- **Gỡ Bỏ Hoàn Toàn Tàn Dư Phân Hệ Live Streaming (LiveSessions)**:
  - Xóa 2 thực thể Domain: [`LiveSession.cs`](./V-Eval-Practice_Service.Domain/Entities/LiveSession.cs) và [`LiveSessionAttendance.cs`](./V-Eval-Practice_Service.Domain/Entities/LiveSessionAttendance.cs).
  - Cập nhật [`RoadmapNode.cs`](./V-Eval-Practice_Service.Domain/Entities/RoadmapNode.cs): Xóa bỏ trường `LiveSessionId` và navigation property `LiveSession`. Chuyển sang mô hình chặng học 2 thành phần tinh gọn (Video lý thuyết + Quiz củng cố).
  - Cập nhật [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs): Gỡ bỏ `DbSet<LiveSession>` và `DbSet<LiveSessionAttendance>`, đăng ký ánh xạ chuẩn schema `v_eval_practice` cho `ClassGroups` và `ClassGroupMembers`.
  - Cập nhật Repository [`ILearningRoadmapRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILearningRoadmapRepository.cs) & [`LearningRoadmapRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LearningRoadmapRepository.cs): Gỡ bỏ toàn bộ phương thức tra cứu LiveSession và eager-loading `.Include(n => n.LiveSession)`.
  - Tái cấu trúc CQRS Handlers & DTOs:
    - [`GenerateRoadmapCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/GenerateRoadmap/GenerateRoadmapCommandHandler.cs): Gỡ bỏ logic quét `thematicLiveSessions`, khởi tạo binding chặng học thuần túy 2 thành phần.
    - [`GetMyRoadmapQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetMyRoadmap/GetMyRoadmapQueryHandler.cs) & [`GetRoadmapNodeDetailQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetRoadmapNodeDetail/GetRoadmapNodeDetailQueryHandler.cs): Loại bỏ mapping `LiveSessionId` và `LiveSessionDetailDto`.
    - [`ClassesController.cs`](./V-Eval-Practice_Service.API/Controllers/ClassesController.cs): Gỡ bỏ endpoint can thiệp online `[HttpPost("{classId:guid}/heatmap/trigger-live-session")]`, xóa command `TriggerHeatmapLiveSessionCommand` và DTO tương ứng. Thay thế bằng cơ chế phân nhóm vi mô 3–5 học sinh và gán phiếu bài tập thích ứng trực tiếp trên lớp (`AssignGroupWorksheetCommand`).
- **Đồng Bộ Trực Tiếp CSDL Supabase PostgreSQL (`v_eval_practice`)**:
  - Di chuyển bảng `ClassGroups` và `ClassGroupMembers` từ schema `public` sang đúng schema `v_eval_practice`.
  - Thiết lập khóa ngoại `ClassGroups.class_id` trỏ chuẩn xác đến `v_eval_practice."Classes"("class_id") ON DELETE CASCADE`.
  - Xóa bỏ bảng `v_eval_practice."LiveSessionAttendance"` và `v_eval_practice."LiveSessions"` với `CASCADE`.
  - Xóa bỏ cột `live_session_id` trong `v_eval_practice."RoadmapNodes"`.
  - Xác nhận phân hệ `v_eval_practice` gồm đúng 19 bảng thực thể sạch sẽ.
- **Biên Dịch & Kiểm Thử Solution**:
  - Đồng bộ package `Microsoft.Extensions.Hosting.Abstractions` phiên bản tương thích `9.0.0`.
  - Thực hiện lệnh `dotnet build --no-restore V-Eval-Practice_Service.sln` thành công 100% (**0 Error, 0 Warning**).
- **Chuẩn Hóa Sơ Đồ ERD [Capstone.drawio](../../docs/SQL/Capstone.drawio) Theo Phương Án C (Hybrid Best-Practice)**:
  - Phân định rõ ràng: Nét liền (Solid Line, 42 quan hệ) cho Khóa ngoại vật lý nội bộ Service; Nét đứt (Dashed Line, 32 quan hệ) cho Tham chiếu logic xuyên Microservices qua gRPC.
  - Đi dây theo quy tắc hình sấm sét bậc thang (Stepped Orthogonal / Manhattan Routing): góc bẻ vuông `90°`, không cắt đè bảng, cầu nhảy `jumpStyle=arc`.
  - Hoàn thiện cả 3 trang: Trang-1 (Clustered), Trang-2 (Conceptual), Trang-3 (Flat Full-Detail 47 bảng).
