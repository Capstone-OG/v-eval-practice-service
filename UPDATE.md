# [24/11/2026]

- Raised gRPC client `MaxReceiveMessageSize` / `MaxSendMessageSize` to `32 MB` in `ContentGrpcClient.cs` so `GetSkillsTreeAsync` no longer drops skill names to `"Kỹ năng chuyên đề"`.
- Added `EnableRetryOnFailure(3, 3s)` on `PracticeDbContext` (`DependencyInjection.cs`) and `.AsSplitQuery()` on `GetStudentTimetableAsync` / `GetTeacherScheduleAsync` (`ScheduleService.cs`) to eliminate transient 500 errors.
- Linked `CourseRegistrations` (`ACTIVE`/`COMPLETED`/`PAID`) in `GetStudentTimetableAsync` so enrolled classes/sessions inherit the student's purchased course and active term metadata, and scoped `availableTerms` to the student's actual terms.
- Upgraded roadmap approval workflow (`RoadmapApprovalWorkflow.cs`, `RoadmapsController.cs`) to a 3-tier model (`DRAFT_PENDING_TEACHER` -> `PENDING_MANAGER_APPROVAL` -> `PENDING_DIRECTOR_APPROVAL` -> `ACTIVE`) with `campusId` scoping for Academic Manager and `EscalateToDirector` support.
- Verified with `dotnet build` (0 warnings, 0 errors).
