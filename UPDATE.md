# UPDATE LOG — V-Eval Practice Service

# UPDATE LOG — V-Eval Practice Service

## [24/11/2026] Fix GUID DAG, Lưu trạng thái Preview vào DB, Non-Blocking Roadmap Approval & 3-Tier Scoping
- **Mục 1, 2 & 6**:
  - `SubmitDiagnosticCommandHandler.cs`: Map tên kỹ năng & tên miền từ `GetSkillsTreeAsync` (bỏ GUID thô), làm sạch nhận xét fallback.
  - `SubmitAnswerCommandHandler.cs` & `SubmitPreviewCommandHandler.cs`: Loại bỏ mã nội bộ (`BR-03`, `BR-08`, `BKT P(Lt)`, `SM-2`) khỏi phản hồi học sinh.
  - `GetMockExamDetailQueryHandler.cs` & `GetMockExamDetailDtos.cs`: Enrich đầy đủ đáp án đúng, nội dung, 4 phương án, lời giải và tên kỹ năng từ Content gRPC.
- **Lưu bền vững trạng thái Pha Preview**:
  - Bổ sung cột `preview_submission_json` (`PreviewSubmissionJson`) vào `StageProgress.cs`, `PracticeDbContext.cs`, `Program.cs` (auto `ALTER TABLE`), `SubmitPreviewCommandHandler.cs`, `StartStageDtos.cs` và `StartStageCommandHandler.cs`.
- **Non-Blocking Teacher Roadmap Approval & 3-Tier Workflow**:
  - Bổ sung 4 trường `IsTeacherApproved`, `ApprovedByTeacherId`, `ApprovedAt`, `TeacherKickoffNote` vào `LearningRoadmap.cs`, `PracticeDbContext.cs`, `Program.cs` (auto `ALTER TABLE`) và EF Migration `20261124060000_AddRoadmapTeacherApprovalFields.cs`.
  - Đổi trạng thái mặc định sang `ACTIVE`, gỡ chặn khởi động chặng, bổ sung `PUT /api/v1/roadmaps/{roadmapId}/approve` + `ApproveRoadmapCommandHandler`, và trả kèm 4 trường trong `RoadmapTimelineDto` & `ChildAnalyticsDto`.
  - Đồng bộ với `develop`: nâng gRPC `32 MB` (`ContentGrpcClient.cs`), `EnableRetryOnFailure(3, 3s)` + `.AsSplitQuery()` (`ScheduleService.cs`), liên kết `CourseRegistrations` cho lịch học, và luồng phê duyệt 3 cấp có `campusId` scoping & `EscalateToDirector`.
