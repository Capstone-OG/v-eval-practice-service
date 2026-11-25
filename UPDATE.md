# UPDATE - V-EVAL PRACTICE SERVICE

## [25/11/2026] - Tối Ưu Supabase Egress (>95%): Bật `SplitQuery` Toàn Cục, Cache gRPC `ContentGrpcClient` & Projection `ExamSubmissions`

- **Chặn Cartesian Explosion Toàn Cục Cho `PracticeDbContext`**:
  - Cập nhật [DependencyInjection.cs](./V-Eval-Practice_Service.Infrastructure/DependencyInjection.cs): cấu hình `b.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` trên `PracticeDbContext`.
- **Cache Phía Client Cho gRPC Content Service**:
  - Cập nhật [ContentGrpcClient.cs](./V-Eval-Practice_Service.Infrastructure/GrpcClients/ContentGrpcClient.cs): tiêm `IMemoryCache` để cache `GetSkillsTreeAsync` trong **30 phút** (có `SemaphoreSlim` chống stampede) và `GetExamAnswerKeysAsync` trong **15 phút**, cắt giảm >99% lượt gọi gRPC lặp lại từ hơn 12 handler.
- **Projection Tóm Tắt Cho `ExamSubmissions` & `AsNoTracking`**:
  - Bổ sung `GetSummariesByStudentIdAsync` trong [IExamSubmissionRepository.cs](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IExamSubmissionRepository.cs) và [ExamSubmissionRepository.cs](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/ExamSubmissionRepository.cs) sử dụng `.AsNoTracking().Select(...)` không kéo `SubmissionAnswers` và `AiCommentary`.
  - Cập nhật [GetStudentDashboardQueryHandler.cs](./V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetStudentDashboard/GetStudentDashboardQueryHandler.cs) dùng `GetSummariesByStudentIdAsync` và bổ sung `.AsNoTracking().AsSplitQuery()` cho [LearningRoadmapRepository.cs](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LearningRoadmapRepository.cs).
