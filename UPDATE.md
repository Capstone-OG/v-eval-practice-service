# V-Eval Practice Service — Cập Nhật Tiến Độ

## [23/11/2026] - Cố Định Thứ Tự Chặng Tuần Tự (Immutable StepOrder) & Luồng Phê Duyệt Lộ Trình 2 Cấp

- **Cố Định `StepOrder` Trong `GetMyRoadmapQueryHandler.cs`**:
  - Loại bỏ cơ chế tự sắp xếp lại và ghi đè `StepOrder` theo `MasteryPlt` / `COMPLETED` trên mỗi yêu cầu `GET /api/practice/roadmaps/my-roadmap`, khắc phục triệt để lỗi chặng đang học (Preview -> Learn) bị đẩy xuống cuối và chặng kế tiếp bị đánh số lại thành Chặng #1 sau khi refresh.
- **Triển Khai Luồng Phê Duyệt Lộ Trình 2 Cấp (Không Đổi Schema DB)**:
  - `SubmitDiagnosticCommandHandler.cs`: Tự động sinh bản nháp lộ trình (`DRAFT_PENDING_TEACHER`) lưu xuống DB ngay sau khi học sinh hoàn thành bài thi đầu vào 30 câu.
  - `StartStageCommandHandler.cs`: Chặn bắt đầu học P-L-A-R nếu lộ trình chưa được phê duyệt kích hoạt (`Status != ACTIVE`).
  - `RoadmapApprovalWorkflow.cs` & `RoadmapsController.cs`: Bổ sung 4 API (`GET /review-queue`, `GET /{roadmapId}`, `PUT /{roadmapId}/teacher-review`, `POST /{roadmapId}/director-decision`) phục vụ Giảng viên tinh chỉnh bản nháp và Giám đốc / Quản lý học thuật phê duyệt kích hoạt `ACTIVE`.
  - `ILearningRoadmapRepository.cs` & `LearningRoadmapRepository.cs`: Bổ sung các phương thức truy vấn lộ trình mới nhất, hàng đợi theo trạng thái và thêm node kỹ năng.
