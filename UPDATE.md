# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [08/10/2026] - Khởi Động Core Flow 3 (Bước 0 & API 1): Mô Hình Thực Thể P-L-A-R & API Khởi Tạo Chặng Học (StartStage)

- **Mở Rộng Domain Entities Core Flow 3**:
  - Tạo thực thể [`StageProgress.cs`](./V-Eval-Practice_Service.Domain/Entities/StageProgress.cs): Quản lý tiến trình 4 bước P-L-A-R (`CurrentStep`: `PREVIEW`, `LEARN`, `APPLY`, `REFLECT`), `VideoWatchPercentage`, xác suất thành thạo BKT `BktMasteryPlt` (mặc định 0.1000), đếm câu đúng liên tiếp $b \ge 0.50$ `ConsecutiveAdvancedCorrect`, đếm câu sai liên tiếp `ConsecutiveIncorrect`, trạng thái chặng `Status` (`IN_PROGRESS`, `REMEDIAL_REQUIRED`, `COMPLETED`).
  - Tạo thực thể [`AdaptiveQuizAttempt.cs`](./V-Eval-Practice_Service.Domain/Entities/AdaptiveQuizAttempt.cs): Lưu vết từng câu trả lời thích ứng ở bước Apply (`QuestionId`, `PatternId`, `SelectedOption`, `IsCorrect`, `TimeSpentSeconds`, `ItemDifficultyB`, `ItemDiscriminationA`, `IsLuckyGuess`, `PriorPlt`, `PosteriorPlt`).
- **Cấu Hình Persistence & Repositories**:
  - Cập nhật [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs): Đăng ký `DbSet<StageProgress>` và `DbSet<AdaptiveQuizAttempt>`, cấu hình Fluent API, quan hệ Cascade với `RoadmapNode` và `AdaptiveAttempts`.
  - Tạo Interface [`IStageProgressRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IStageProgressRepository.cs) và Repository [`StageProgressRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/StageProgressRepository.cs). Đăng ký vào DI container `DependencyInjection.cs`.
- **Hiện Thực Core Flow 3 - API 1: Khởi Tạo Chặng Học Thích Ứng (POST /api/practice/stages/{roadmapNodeId}/start)**:
  - Khởi tạo chuỗi DTOs [`StartStageDtos.cs`](./V-Eval-Practice_Service.Application/Features/Stages/DTOs/StartStageDtos.cs) (`StartStageRequestDto`, `PreviewQuestionDto`, `StartStageResponseDto`).
  - Khởi tạo Command `StartStageCommand.cs` và Validator `StartStageCommandValidator.cs`.
  - Xây dựng Handler [`StartStageCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Commands/StartStage/StartStageCommandHandler.cs):
    1. Kiểm tra tồn tại của chặng học `RoadmapNode` từ repository.
    2. Tìm hoặc khởi tạo mới bản ghi `StageProgress` ở bước `PREVIEW` (`BktMasteryPlt = 0.1000`, `Status = IN_PROGRESS`).
    3. Nạp 3 câu hỏi Quick Check khởi động từ Content Service qua gRPC (`GetMilestoneQuizAsync` với `questionCount = 3`) hoặc fallback 3 câu mẫu kiểm thử an toàn.
  - Xây dựng Controller mới [`StagesController.cs`](./V-Eval-Practice_Service.API/Controllers/StagesController.cs) với route chuẩn không có `v1`: `[Route("api/practice/stages")]` và endpoint `[HttpPost("{roadmapNodeId:guid}/start")]`.
- **Kiểm Thử Vận Hành**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
