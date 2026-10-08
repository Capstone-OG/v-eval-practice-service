# NHẬT KÝ KIỂM TRA TIẾN ĐỘ VẬN HÀNH (DAILY CHECK LOG) - PRACTICE SERVICE

## [08/10/2026] - Khởi Động Core Flow 3 (Bước 0 & API 1): Mô Hình Thực Thể P-L-A-R & API Khởi Tạo Chặng Học (StartStage)
- **Mở Rộng Domain Entities Core Flow 3**:
  - Tạo thực thể [`StageProgress.cs`](../V-Eval-Practice_Service.Domain/Entities/StageProgress.cs): Quản lý tiến trình 4 bước P-L-A-R (`CurrentStep`: `PREVIEW`, `LEARN`, `APPLY`, `REFLECT`), `VideoWatchPercentage`, xác suất thành thạo BKT `BktMasteryPlt` (mặc định 0.1000), đếm câu đúng liên tiếp $b \ge 0.50$ `ConsecutiveAdvancedCorrect`, đếm câu sai liên tiếp `ConsecutiveIncorrect`, trạng thái chặng `Status` (`IN_PROGRESS`, `REMEDIAL_REQUIRED`, `COMPLETED`).
  - Tạo thực thể [`AdaptiveQuizAttempt.cs`](../V-Eval-Practice_Service.Domain/Entities/AdaptiveQuizAttempt.cs): Lưu vết từng câu trả lời thích ứng ở bước Apply (`QuestionId`, `PatternId`, `SelectedOption`, `IsCorrect`, `TimeSpentSeconds`, `ItemDifficultyB`, `ItemDiscriminationA`, `IsLuckyGuess`, `PriorPlt`, `PosteriorPlt`).
- **Cấu Hình Persistence & Repositories**:
  - Cập nhật [`PracticeDbContext.cs`](../V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs): Đăng ký `DbSet<StageProgress>` và `DbSet<AdaptiveQuizAttempt>`, cấu hình Fluent API, quan hệ Cascade với `RoadmapNode` và `AdaptiveAttempts`.
  - Tạo Interface [`IStageProgressRepository.cs`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IStageProgressRepository.cs) và Repository [`StageProgressRepository.cs`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/StageProgressRepository.cs). Đăng ký vào DI container `DependencyInjection.cs`.
- **Hiện Thực Core Flow 3 - API 1: Khởi Tạo Chặng Học Thích Ứng (POST /api/practice/stages/{roadmapNodeId}/start)**:
  - Khởi tạo chuỗi DTOs [`StartStageDtos.cs`](../V-Eval-Practice_Service.Application/Features/Stages/DTOs/StartStageDtos.cs) (`StartStageRequestDto`, `PreviewQuestionDto`, `StartStageResponseDto`).
  - Khởi tạo Command `StartStageCommand.cs` và Validator `StartStageCommandValidator.cs`.
  - Xây dựng Handler [`StartStageCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Stages/Commands/StartStage/StartStageCommandHandler.cs):
    1. Kiểm tra tồn tại của chặng học `RoadmapNode` từ repository.
    2. Tìm hoặc khởi tạo mới bản ghi `StageProgress` ở bước `PREVIEW` (`BktMasteryPlt = 0.1000`, `Status = IN_PROGRESS`).
    3. Nạp 3 câu hỏi Quick Check khởi động từ Content Service qua gRPC (`GetMilestoneQuizAsync` với `questionCount = 3`) hoặc fallback 3 câu mẫu kiểm thử an toàn.
  - Xây dựng Controller mới [`StagesController.cs`](../V-Eval-Practice_Service.API/Controllers/StagesController.cs) với route chuẩn không có `v1`: `[Route("api/practice/stages")]` và endpoint `[HttpPost("{roadmapNodeId:guid}/start")]`.
- **Hiện Thực Core Flow 3 - API 2: Nộp Bài Khởi Động Preview & Chuyển Sang Bước Learn (POST /api/practice/stages/{stageProgressId}/preview-submit)**:
  - Khởi tạo DTOs [`SubmitPreviewDtos.cs`](../V-Eval-Practice_Service.Application/Features/Stages/DTOs/SubmitPreviewDtos.cs) (`PreviewAnswerSubmissionDto`, `SubmitPreviewRequestDto`, `SubmitPreviewResponseDto`).
  - Khởi tạo Command `SubmitPreviewCommand.cs` và Validator `SubmitPreviewCommandValidator.cs`.
  - Xây dựng Handler [`SubmitPreviewCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Stages/Commands/SubmitPreview/SubmitPreviewCommandHandler.cs):
    1. Tra cứu tiến trình `StageProgress` theo ID.
    2. Ghi nhận và chấm điểm sơ bộ 3 câu khởi động (không tính vào BKT theo đúng thiết kế sư phạm).
    3. Cập nhật State Machine chuyển `CurrentStep` từ `PREVIEW` sang `LEARN`.
    4. Trả về thông điệp hướng dẫn học sinh xem video phương pháp cùng đường dẫn video bài giảng.
  - Bổ sung endpoint `[HttpPost("{stageProgressId:guid}/preview-submit")]` vào [`StagesController.cs`](../V-Eval-Practice_Service.API/Controllers/StagesController.cs).
- **Hiện Thực Core Flow 3 - API 3: Ghi Nhận Tiến Độ Xem Video & Chuyển Sang Bước Apply (POST /api/practice/stages/{stageProgressId}/track-video)**:
  - Khởi tạo DTOs [`TrackVideoDtos.cs`](../V-Eval-Practice_Service.Application/Features/Stages/DTOs/TrackVideoDtos.cs) (`TrackVideoRequestDto`, `TrackVideoResponseDto`).
  - Khởi tạo Command `TrackVideoCommand.cs` và Validator `TrackVideoCommandValidator.cs`.
  - Xây dựng Handler [`TrackVideoCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Stages/Commands/TrackVideo/TrackVideoCommandHandler.cs):
    1. Tra cứu tiến trình `StageProgress` theo ID và kiểm tra quyền sở hữu `StudentId`.
    2. Tính phần trăm thời lượng đã xem lũy tiến (`VideoWatchPercentage`), không giảm khi tua lại.
    3. Kiểm tra điều kiện mở khóa bước APPLY: Nếu `VideoWatchPercentage >= 80%` và đang ở `LEARN` -> Chuyển `CurrentStep = "APPLY"`.
    4. Tự động đồng bộ trạng thái sang `RoadmapNode` (`IsVideoCompleted = true`, `VideoWatchedSeconds`, `VideoTotalSeconds`).
    5. Trả về thông điệp và cờ điều hướng `nextAction = "START_ADAPTIVE_PRACTICE"`.
  - Bổ sung endpoint `[HttpPost("{stageProgressId:guid}/track-video")]` vào [`StagesController.cs`](../V-Eval-Practice_Service.API/Controllers/StagesController.cs).
- **Hiện Thực Core Flow 3 - API 4: Lấy Câu Hỏi Thích Ứng Tiếp Theo Trong Vùng ZPD (GET /api/practice/stages/{stageProgressId}/next-question)**:
  - Xây dựng động cơ thích ứng IRT 2PL [`ZpdQuestionSelector.cs`](../V-Eval-Practice_Service.Application/Common/Adaptive/ZpdQuestionSelector.cs):
    1. Quy đổi xác suất thành thạo BKT $P(L_t) \in [0.05, 0.95]$ sang thang logit năng lực $\theta \in [-2.5, +2.5]$.
    2. Tính xác suất làm đúng theo mô hình IRT 2PL $P(\theta, a, b) = 1 / (1 + e^{-1.7 \cdot a \cdot (\theta - b)})$.
    3. Bộ lọc ZPD 3 tầng: Tầng 1 (vùng ZPD lý tưởng $P \in [0.60, 0.75]$), Tầng 2 (vùng ZPD nới lỏng $P \in [0.50, 0.85]$), Tầng 3 (câu có xác suất tiệm cận tâm ZPD 0.675 nhất).
    4. Tự động loại trừ các câu hỏi đã trả lời trong phiên `AdaptiveAttempts`.
  - Khởi tạo DTOs [`NextQuestionDtos.cs`](../V-Eval-Practice_Service.Application/Features/Stages/DTOs/NextQuestionDtos.cs) ẩn toàn bộ đáp án đúng để bảo mật.
  - Khởi tạo Query `GetNextQuestionQuery.cs` và Handler `GetNextQuestionQueryHandler.cs`.
  - Kiểm tra trạng thái máy: Chặn nếu chưa mở khóa `APPLY`, tự động thông báo dừng nếu đã hoàn thành chặng hoặc bị phong tỏa bởi quy tắc phụ đạo BR-03 (`REMEDIAL_REQUIRED`).
  - Bổ sung endpoint `[HttpGet("{stageProgressId:guid}/next-question")]` vào [`StagesController.cs`](../V-Eval-Practice_Service.API/Controllers/StagesController.cs).
- **Hiện Thực Core Flow 3 - API 5: Nộp Câu Trả Lời Thích Ứng & Động Cơ BKT (POST /api/practice/stages/{stageProgressId}/submit-answer)**:
  - Xây dựng động cơ Bayesian Knowledge Tracing [`BktEngine.cs`](../V-Eval-Practice_Service.Application/Common/Adaptive/BktEngine.cs):
    1. Cơ chế phạt đoán mò (Lucky Guess Penalty): Khi học sinh trả lời đúng nhưng thời gian làm $t < 5$s đối với câu hỏi vận dụng $b \ge 0.50$, tăng $P(G) = 0.60$ và gắn cờ `IsLuckyGuess = true`.
    2. Cập nhật Bayesian Posterior $P(L_t \mid obs)$ và bước chuyển dịch tri thức $P(L_t) = P(L_t \mid obs) + (1 - P(L_t \mid obs)) \cdot P(T)$.
    3. Quy tắc sư phạm BR-01: Khi $P(L_t) \ge 0.85$ và đúng liên tiếp 2 câu nâng cao ($b \ge 0.50$) $\implies$ Đạt độ thành thạo mục tiêu, tự động chuyển `CurrentStep = "REFLECT"`.
    4. Quy tắc sư phạm BR-03: Khi sai liên tiếp 3 câu $\implies$ Phong tỏa trạng thái `Status = "REMEDIAL_REQUIRED"`, yêu cầu xem clip phụ đạo trước khi tiếp tục.
    5. Lưu toàn bộ micro-telemetry vào bảng `AdaptiveQuizAttempts`.
  - Khởi tạo DTOs [`SubmitAnswerDtos.cs`](../V-Eval-Practice_Service.Application/Features/Stages/DTOs/SubmitAnswerDtos.cs), Command và Handler.
  - Bổ sung endpoint `[HttpPost("{stageProgressId:guid}/submit-answer")]` vào [`StagesController.cs`](../V-Eval-Practice_Service.API/Controllers/StagesController.cs).
- **Hiện Thực Core Flow 3 - API 6: Phản Tư Cá Nhân & Hoàn Thành Chặng Học (POST /api/practice/stages/{stageProgressId}/reflect-complete)**:
  - Khởi tạo DTOs [`ReflectCompleteDtos.cs`](../V-Eval-Practice_Service.Application/Features/Stages/DTOs/ReflectCompleteDtos.cs), Command, Validator và Handler.
  - Xử lý hoàn tất chặng học:
    1. Ghi nhận đánh giá độ tự tin (Confidence Rating từ 1 đến 5 sao) và ghi chú bài học rút ra.
    2. Đánh dấu `StageProgress.Status = "COMPLETED"`.
    3. Đồng bộ trạng thái sang `RoadmapNode` (`Status = "COMPLETED"`, `IsQuizPassed = true`, `QuizScore = P(Lt) * 10.0`, `CompletedAt = UtcNow`).
    4. Tự động tìm và mở khóa chặng học kế tiếp trên lộ trình (`RoadmapNode` tiếp theo chuyển từ `LOCKED` sang `IN_PROGRESS`).
  - Bổ sung endpoint `[HttpPost("{stageProgressId:guid}/reflect-complete")]` vào [`StagesController.cs`](../V-Eval-Practice_Service.API/Controllers/StagesController.cs).
- **Tinh Gọn & Chuẩn Hóa Giao Diện Swagger UI & Phân Định Ranh Giới Kiến Trúc**:
  - Tinh gọn XML `<summary>` của toàn bộ các API trong [`StagesController.cs`](../V-Eval-Practice_Service.API/Controllers/StagesController.cs): Mỗi API chỉ có 1 dòng tiêu đề ngắn gọn theo từng bước (Bước 1 đến Bước 6), chuyển toàn bộ nội dung diễn giải dài dòng vào `<remarks>` (ẩn trong dropdown) giúp giao diện Swagger cực kỳ thoáng mắt, đẹp và không bị tràn viền.
  - Tái cấu trúc [`RoadmapsController.cs`](../V-Eval-Practice_Service.API/Controllers/RoadmapsController.cs): Loại bỏ các endpoint làm bài tĩnh trùng lặp (`track-video`, `quiz`, `submit-quiz`, `submit-makeup-quiz`). Định vị rõ vai trò của Roadmap là **Quản lý lộ trình vĩ mô cá nhân hóa** (`generate`, `my-roadmap`, `nodes/{nodeId}`), còn toàn bộ việc học tập vi mô, video, luyện tập thích ứng được quy hoạch tập trung 100% tại `StagesController` (P-L-A-R).
- **Kiểm Thử Vận Hành**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử chuỗi toàn diện Module 1 từ API 1 đến API 6 thành công 100% (200 OK).





## [01/10/2026] - Nâng Cấp Core Flow 2 (Bước 1): Mở Rộng Mô Hình Thực Thể Class & Di Trú CSDL Phục Vụ Lớp Học Chuyên Đề (Thematic Cohort)
- **Mở Rộng Mô Hình Thực Thể [`Class.cs`](../V-Eval-Practice_Service.Domain/Entities/Class.cs)**:
  - Bổ sung 4 trường dữ liệu trọng yếu cho bài toán phân cụm lớp chuyên đề:
    - `ClassType` (`int`, mặc định `0` = Lớp hành chính phổ thông theo năng lực tổng thể $\theta_0$, `1` = Lớp chuyên đề theo cụm lỗ hổng K-Means).
    - `DomainId` (`Guid?`): Định danh miền kiến thức chuyên đề (Toán, Ngôn ngữ, KHTN, KHXH).
    - `DomainCode` (`string?`, max length 50): Mã định danh chuẩn (`DOM_LANG`, `DOM_MATH`, `DOM_NAT_SCI`, `DOM_SOC_SCI`).
    - `ClusterIndex` (`int?`): Chỉ số cụm tương ứng sinh ra bởi thuật toán phân cụm K-Means.
- **Cấu Hình Fluent API & DbContext ([`PracticeDbContext.cs`](../V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs))**:
  - Ánh xạ rõ ràng các cột `class_type`, `domain_id`, `domain_code`, `cluster_index` vào bảng `Classes` thuộc schema `v_eval_practice`.
- **Di Trú CSDL (Database Migration & Verification)**:
  - Khởi tạo migration `20260930184119_AddThematicCohortFields`.
  - Tinh chỉnh migration để chỉ tác động thêm 4 cột mới vào bảng `Classes`, đảm bảo tương thích 100% với các bảng đã có trong PostgreSQL (`LearningRoadmaps`, `LiveSessions`, `RoadmapNodes`, `LiveSessionAttendance`).
  - Thực thi `dotnet ef database update` thành công, lưu bản ghi migration vào `practice."__EFMigrationsHistory"`.
  - Xác thực trực tiếp qua kiểm tra schema CSDL Supabase PostgreSQL: 4 cột `class_type (integer)`, `cluster_index (integer)`, `domain_code (character varying)`, `domain_id (uuid)` đã sẵn sàng vận hành.
- **Kiểm Thử Biên Dịch Bước 1**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
- **Nâng Cấp Core Flow 2 (Bước 2): Đồng Bộ DomainCode Qua gRPC & Chuỗi DTO Lộ Trình**:
  - Đồng bộ hợp đồng [`content.proto`](../V-Eval-Practice_Service.Infrastructure/Protos/content.proto) khớp với Content Service với trường `string domain_code = 8;` trong `SkillNode`.
  - Mở rộng [`IContentGrpcClient.cs`](../V-Eval-Practice_Service.Application/Common/Interfaces/IContentGrpcClient.cs) (`SkillTreeNodeDto`) và [`ContentGrpcClient.cs`](../V-Eval-Practice_Service.Infrastructure/GrpcClients/ContentGrpcClient.cs) ánh xạ deserialization trường `DomainCode`.
  - Bổ sung `DomainCode` vào các DTO lộ trình: [`RoadmapNodeSummaryDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapNodeSummaryDto.cs), [`RoadmapStageDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapStageDto.cs), [`RoadmapNodeDetailDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapNodeDetailDto.cs).
  - Bổ sung `PlacementClass` vào [`GenerateRoadmapResponseDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/GenerateRoadmapResponseDto.cs) giúp Frontend định danh chính xác tier năng lực (`FOUNDATION` / `ACCELERATION` / `BREAKTHROUGH`).
  - Cập nhật [`GenerateRoadmapCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/GenerateRoadmap/GenerateRoadmapCommandHandler.cs), [`GetMyRoadmapQueryHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetMyRoadmap/GetMyRoadmapQueryHandler.cs) và [`GetRoadmapNodeDetailQueryHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetRoadmapNodeDetail/GetRoadmapNodeDetailQueryHandler.cs) trích xuất và ánh xạ hoàn chỉnh chuỗi `DomainCode` và `PlacementClass`.
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).
- **Nâng Cấp Core Flow 2 (Bước 3): Viết Thuật Toán K-Means Student Clustering (K-Means++ & Elbow Method)**:
  - Khởi tạo thuật toán phân cụm chuẩn mực [`StudentKMeansClusterer.cs`](../V-Eval-Practice_Service.Application/Common/Graph/StudentKMeansClusterer.cs) và interface `IStudentKMeansClusterer` trong thư mục `Common/Graph`.
  - Hỗ trợ số lượng học sinh $N$ động ($N \ge 2$), tự động thích ứng giới hạn số cụm $K_{\max} = \min(8, \max(2, \lfloor N / 3 \rfloor))$.
  - Thuật toán gồm 3 thành phần chính:
    1. **K-Means++ Initialization**: Lấy mẫu xác suất theo bình phương khoảng cách Euclidean $D(x)^2$ giúp các tâm cụm ban đầu phân bố đều trên không gian 4 chiều, chống local minima.
    2. **Lloyd's Algorithm**: Vòng lặp gán học sinh vào tâm cụm gần nhất và cập nhật toạ độ tâm cụm theo vector trung bình đến khi hội tụ (hỗ trợ phục hồi cụm rỗng).
    3. **Elbow Method (Chord Method)**: Tính tổng bình phương khoảng cách cụm (WCSS) cho dải $K \in [2, K_{\max}]$, tìm điểm gập khuỷu tay hình học tối ưu dựa trên khoảng cách vuông góc cực đại đến dây cung nối 2 đầu.
  - Phân tích sư phạm Centroid tự động: Tự động phát hiện miền kiến thức yếu nổi trội (< 0.60), đặt tên lớp chuyên đề gợi ý (ví dụ: *"Chuyên đề: Trọng điểm Toán - Logic"*, *"Chuyên đề: Tăng cường Ngôn ngữ & KHTN"*), và gán mã miền mục tiêu `TargetDomainCode`.
  - Đăng ký `IStudentKMeansClusterer` vào DI container ([`DependencyInjection.cs`](../V-Eval-Practice_Service.Application/DependencyInjection.cs)).
- **Nâng Cấp Core Flow 2 (Bước 4): Hiện Thực API Tự Động Phân Cụm Lớp Chuyên Đề (POST /api/practice/classes/auto-cluster)**:
  - Khởi tạo các DTOs [`AutoClusterThematicClassesDtos.cs`](../V-Eval-Practice_Service.Application/Features/Classes/DTOs/AutoClusterThematicClassesDtos.cs):
    - `AutoClusterThematicClassesRequestDto`: `CampusId`, `Grade` (mặc định 12), `MaxCohortCapacity` (mặc định 30).
    - `ThematicClassCreatedDto`: `ClassId`, `ClassName`, `DomainId`, `DomainCode`, `ClusterIndex`, `DominantWeakDomain`, `EnrolledStudentCount`, `StudentIds`.
    - `AutoClusterThematicClassesResponseDto`: `CampusId`, `TotalStudentsProcessed`, `OptimalK`, `ClassesCreated`.
  - Mở rộng Repository [`ILearningProfileRepository.cs`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILearningProfileRepository.cs) và [`LearningProfileRepository.cs`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LearningProfileRepository.cs) với phương thức `GetByStudentIdsAsync(IEnumerable<Guid> studentIds)`.
  - Mở rộng Repository [`IClassEnrollmentRepository.cs`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IClassEnrollmentRepository.cs) và [`ClassEnrollmentRepository.cs`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/ClassEnrollmentRepository.cs) với `GetEnrolledStudentIdsByCampusIdAsync(Guid campusId)` và `CreateThematicClassWithEnrollmentsAsync(...)`.
  - Xây dựng CQRS:
    - Command `AutoClusterThematicClassesCommand.cs` và `AutoClusterThematicClassesCommandValidator.cs`.
    - Handler [`AutoClusterThematicClassesCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Classes/Commands/AutoClusterThematicClasses/AutoClusterThematicClassesCommandHandler.cs):
      1. Truy vấn danh sách học sinh thuộc cơ sở đào tạo qua `GetEnrolledStudentIdsByCampusIdAsync`.
      2. Truy vấn dữ liệu hồ sơ năng lực vi mô `LearningProfiles` của toàn bộ học sinh.
      3. Lấy Skill Tree từ Content Service qua gRPC `IContentGrpcClient` để ánh xạ `SkillId -> DomainCode`.
      4. Tổng hợp vector lỗ hổng 4 miền $[\text{DOM\_LANG}, \text{DOM\_MATH}, \text{DOM\_NAT\_SCI}, \text{DOM\_SOC\_SCI}]$ cho $N$ học sinh.
      5. Thực thi phân cụm K-Means Elbow Method thông qua `IStudentKMeansClusterer`.
      6. Khởi tạo các lớp chuyên đề `Class` (`ClassType = 1`, `DomainId`, `DomainCode`, `ClusterIndex`) kèm phân chia sĩ số phù hợp `MaxCohortCapacity`.
      7. Tự động ghi danh học sinh vào lớp chuyên đề trong `ClassEnrollments`.
  - Bổ sung endpoint `[HttpPost("auto-cluster")]` vào [`ClassesController.cs`](../V-Eval-Practice_Service.API/Controllers/ClassesController.cs).
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).
- **Nâng Cấp Core Flow 2 (Bước 5): Liên Kết Buổi Học LiveSession Cho Từng Chặng Lộ Trình Theo Đúng Miền Chuyên Đề (Thematic Cohort Binding)**:
  - Mở rộng Repository [`ILearningRoadmapRepository.cs`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILearningRoadmapRepository.cs) và [`LearningRoadmapRepository.cs`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LearningRoadmapRepository.cs) với phương thức `GetUpcomingThematicLiveSessionsAsync(Guid studentId, Guid? administrativeClassId, CancellationToken ct)`.
  - Cơ chế truy vấn 3 tầng tối ưu:
    1. **Tầng 1 (Cá nhân hóa chuyên đề)**: Quét danh sách lớp chuyên đề (`ClassType = 1`) mà học sinh đã ghi danh (`ClassEnrollments`), nạp các buổi LiveSession sắp diễn ra (`SCHEDULED`) map theo `DomainCode` (`DOM_LANG`, `DOM_MATH`, `DOM_NAT_SCI`, `DOM_SOC_SCI`).
    2. **Tầng 2 (Bổ khuyết theo Campus)**: Nếu học sinh chưa ghi danh đủ 4 miền, tự động tìm kiếm buổi LiveSession chuyên đề tương ứng tại cùng `CampusId`.
    3. **Tầng 3 (Fallback hành chính)**: Nếu miền kiến thức chưa có lớp chuyên đề nào mở Live, fallback về buổi LiveSession chung của lớp hành chính (`submission.EnrolledClassId`).
  - Cập nhật [`GenerateRoadmapCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/GenerateRoadmap/GenerateRoadmapCommandHandler.cs) tại Bước 6:
    - Di chuyển từ điển kỹ năng `skillMap` lên trước Bước 6 để xác định chính xác `DomainCode` cho từng chặng học.
    - Ánh xạ `LiveSessionId` cho từng `SkillResourceBinding`: Chặng Toán gắn Live của lớp Chuyên đề Toán, Chặng Văn gắn Live lớp Chuyên đề Ngôn ngữ... thay vì gắn chung 1 buổi Live cho toàn bộ lộ trình.
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).
- **Nâng Cấp Core Flow 2 (Bước 6): Hỗ Trợ Đa Ghi Danh (Multi-Class Enrollment) Trong API Thời Khóa Biểu (GET /api/practice/live-sessions/my-schedule)**:
  - Cập nhật [`LiveSessionRepository.cs`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LiveSessionRepository.cs) tại phương thức `GetUpcomingSessionsForStudentAsync`:
    - Thay vì chỉ lấy 1 lớp học gần nhất (`FirstOrDefaultAsync`), chuyển sang lấy toàn bộ danh sách `classIds` mà học sinh đang ghi danh (`Status = "ENROLLED"`), bao gồm cả lớp hành chính và tất cả các lớp chuyên đề K-Means.
    - Truy vấn toàn bộ các buổi LiveSession sắp diễn ra của tất cả các lớp mà học sinh theo học qua mệnh đề `classIds.Contains(s.ClassId)`.
  - Mở rộng DTO [`GetMyLiveScheduleDtos.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/GetMyLiveScheduleDtos.cs) bổ sung các trường nhận diện lớp và môn học:
    - `ClassId`: Định danh lớp học tổ chức buổi Live.
    - `ClassName`: Tên lớp học (ví dụ: *"Chuyên đề: Trọng điểm Toán - Logic"*).
    - `DomainCode`: Mã môn học/miền năng lực chuyên đề (`DOM_LANG`, `DOM_MATH`, `DOM_NAT_SCI`, `DOM_SOC_SCI`).
  - Cập nhật [`GetMyLiveScheduleQueryHandler.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/Queries/GetMyLiveSchedule/GetMyLiveScheduleQueryHandler.cs) map đầy đủ các trường thông tin lớp học và miền chuyên đề vào danh sách buổi học trả về.
  - Kiểm thử trực tiếp qua API: Học sinh xem được trọn vẹn buổi học của lớp chuyên đề Toán kèm trạng thái điểm danh cá nhân.
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).

## [30/09/2026] - Triển Khai Hoàn Thiện APIs 12, 13, 14, 15: Điểm Danh Chuyên Cần, Thời Khóa Biểu Giảng Dạy, Video Ghi Hình & Hủy Buổi Học Trực Tuyến
- **API 12: Giáo Viên Điểm Danh Chuyên Cần Cho Học Sinh (POST /api/v1/practice/live-sessions/{sessionId}/attendance)**:
  - Khởi tạo DTOs [`TeacherAttendanceDtos.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/TeacherAttendanceDtos.cs): `StudentAttendanceItemDto`, `TeacherAttendanceRequestDto`, `TeacherAttendanceResponseDto`.
  - Xây dựng FluentValidation `TeacherAttendanceCommandValidator` kiểm tra ràng buộc `SessionId`, danh sách học sinh và giá trị trạng thái (`ATTENDED` hoặc `ABSENT`).
  - Triển khai `TeacherAttendanceCommand` và [`TeacherAttendanceCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/TeacherAttendance/TeacherAttendanceCommandHandler.cs):
    1. Kiểm tra tồn tại buổi Live (`404 Not Found`).
    2. Chặn thao tác điểm danh khi buổi học đã bị hủy `session.Status == "CANCELLED"` (`400 Bad Request`).
    3. Duyệt từng học sinh, cập nhật hoặc tạo mới bản ghi `LiveSessionAttendance` với trạng thái `ATTENDED` hoặc `ABSENT`. Nếu học sinh chưa từng ấn vào phòng qua web, ghi nhận `JoinedAt = null`. Nếu đã vào phòng, bảo lưu nguyên vẹn thời gian `JoinedAt` thực tế.
    4. Thống kê tổng số học sinh đã điểm danh, số tham gia, số vắng mặt.
- **API 13: Lấy Thời Khóa Biểu Giảng Dạy Của Giáo Viên (GET /api/v1/practice/live-sessions/teacher-schedule)**:
  - Bổ sung phương thức `GetSessionsForTeacherAsync`, `TeacherExistsAsync` và `GetEnrolledStudentCountByClassIdAsync` vào `ILiveSessionRepository` và `LiveSessionRepository`.
  - Khởi tạo DTOs [`TeacherScheduleDtos.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/TeacherScheduleDtos.cs), `GetTeacherScheduleQuery.cs`, `GetTeacherScheduleQueryValidator.cs` và [`GetTeacherScheduleQueryHandler.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/Queries/GetTeacherSchedule/GetTeacherScheduleQueryHandler.cs).
  - Nghiệp vụ & Ngoại lệ: Kiểm tra `TeacherId` rỗng (`400 Bad Request`), kiểm tra giáo viên tồn tại trong hệ thống đào tạo qua `TeacherExistsAsync` (`404 Not Found` `TeacherNotFound`). Truy vấn danh sách buổi Live được giao cho giáo viên (`TeacherId` trực tiếp hoặc giáo viên phụ trách lớp `Class.TeacherId`), thống kê sĩ số lớp, số tham gia (`ATTENDED`), số vắng mặt (`ABSENT`), link phòng họp và link video ghi hình.
- **API 14: Cập Nhật Video Ghi Hình Buổi Live Q&A (PUT /api/v1/practice/live-sessions/{sessionId}/recording)**:
  - Khởi tạo DTOs [`UpdateLiveSessionRecordingDtos.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/UpdateLiveSessionRecordingDtos.cs), `UpdateLiveSessionRecordingCommand.cs`, `UpdateLiveSessionRecordingCommandValidator.cs` và [`UpdateLiveSessionRecordingCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/UpdateRecording/UpdateLiveSessionRecordingCommandHandler.cs).
  - Nghiệp vụ & Ngoại lệ: Kiểm tra tính hợp lệ của URL (`http`/`https`), chặn cập nhật khi buổi học đã bị hủy `session.Status == "CANCELLED"` (`400 Bad Request`), cập nhật `RecordingUrl`, đánh dấu `IsRecorded = true` và chuyển trạng thái buổi học sang `COMPLETED` để học sinh vắng mặt xem lại bài giảng.
- **API 15: Giáo Viên / Giáo Vụ Hủy Buổi Học Trực Tuyến Khi Bận Đột Xuất (PUT /api/v1/practice/live-sessions/{sessionId}/cancel)**:
  - Khởi tạo DTOs [`CancelLiveSessionDtos.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/CancelLiveSessionDtos.cs): `CancelLiveSessionRequestDto`, `CancelLiveSessionResponseDto`.
  - Xây dựng FluentValidation `CancelLiveSessionCommandValidator` kiểm tra ràng buộc `SessionId` và `Reason` (không quá 500 ký tự).
  - Triển khai `CancelLiveSessionCommand` và [`CancelLiveSessionCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/CancelLiveSession/CancelLiveSessionCommandHandler.cs):
    1. Kiểm tra tồn tại buổi Live (`404 Not Found`).
    2. Chặn hủy khi buổi học đã hoàn thành `session.Status == "COMPLETED"` (`400 Bad Request`).
    3. Chặn hủy lặp lại khi buổi học đã ở trạng thái `CANCELLED` (`400 Bad Request`).
    4. Không xóa vật lý bản ghi (do Academic Manager tạo, bảo lưu lịch sử đào tạo). Cập nhật `Status = "CANCELLED"` và đính kèm lý do hủy vào `Description`.
- **Chuẩn Hóa Đồng Bộ Route API (`api/practice/...`) Khớp Với API Gateway**:
  - Gỡ bỏ hoàn toàn tiền tố `v1` khỏi các Controller trong Practice Service ([`ClassesController.cs`](../V-Eval-Practice_Service.API/Controllers/ClassesController.cs), [`DiagnosticSubmissionsController.cs`](../V-Eval-Practice_Service.API/Controllers/DiagnosticSubmissionsController.cs), [`LiveSessionsController.cs`](../V-Eval-Practice_Service.API/Controllers/LiveSessionsController.cs), [`RoadmapsController.cs`](../V-Eval-Practice_Service.API/Controllers/RoadmapsController.cs)).
  - Đồng bộ 100% với cấu hình định tuyến của YARP API Gateway (`/api/practice/{**catch-all}`).
- **Tầng API Controller (`LiveSessionsController.cs`)**:
  - Bổ sung 4 endpoint: `[HttpPost("{sessionId:guid}/attendance")]`, `[HttpGet("teacher-schedule")]`, `[HttpPut("{sessionId:guid}/recording")]`, `[HttpPut("{sessionId:guid}/cancel")]`.
- **Kiểm Thử Vận Hành Trực Tiếp (Live End-to-End Test)**:
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kịch bản API 12: Giáo viên điểm danh 2 học sinh (`1111...` ATTENDED, `2222...` ABSENT) cho Session `2a196c82...` -> `200 OK`, `totalAttended: 1`, `totalAbsent: 1`. Chặn điểm danh session đã hủy -> `400 Bad Request`.
  - Kịch bản API 13: Tra cứu lịch dạy của giáo viên `99999999-9999-9999-9999-999999999999` -> `200 OK`, trả về 5 buổi Live đầy đủ số liệu sĩ số lớp, số tham gia, số vắng mặt. Tra cứu giáo viên không tồn tại -> `404 Not Found` (`TeacherNotFound`).
  - Kịch bản API 14: Cập nhật URL ghi hình -> `200 OK`, `isRecorded: true`, `status: "COMPLETED"`.
  - Kịch bản API 15: Giáo viên hủy buổi học `8ebfe3ee...` vì bận công tác -> `200 OK`, trạng thái chuyển sang `CANCELLED`. Bấm hủy lại -> `400 Bad Request`. Học sinh gọi API 11 Join -> `400 Bad Request` ("Buổi học này đã bị hủy bỏ").
  - Kịch bản xác thực chéo API 10: Học sinh `1111...` tra cứu lịch thấy ngay trạng thái `ATTENDED`, link video recording và trạng thái `COMPLETED`.

---

## [29/09/2026] - Triển Khai Hoàn Thiện Giai Đoạn 3: Quản Lý Buổi Học Live Q&A, Phân Công Giáo Viên, Thời Khóa Biểu & Điểm Danh Trực Tuyến (APIs 8, 9, 10, 11)
- **API 8: Tạo Lịch Buổi Học Live Q&A Cho Lớp Học Cơ Sở (POST /api/v1/practice/live-sessions)**:
  - Khởi tạo Repository [`ILiveSessionRepository.cs`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILiveSessionRepository.cs) và [`LiveSessionRepository.cs`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LiveSessionRepository.cs) quản lý thực thể `LiveSessions` và `LiveSessionAttendance`. Đăng ký Scoped trong `DependencyInjection.cs`.
  - Khởi tạo CQRS: [`CreateLiveSessionDtos.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/CreateLiveSessionDtos.cs), `CreateLiveSessionCommand.cs`, `CreateLiveSessionCommandValidator.cs`, [`CreateLiveSessionCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/CreateLiveSession/CreateLiveSessionCommandHandler.cs).
  - Nghiệp vụ: Xác thực lớp học tồn tại, tự động kế thừa `TeacherId` của lớp nếu không truyền, sinh link phòng học `meeting_url` nếu chưa có, lưu bản ghi trạng thái `SCHEDULED`.
- **API 9: Phân Công Hoặc Điều Chuyển Giáo Viên Phụ Trách Lớp Học Cơ Sở (PUT /api/v1/practice/classes/{classId}/assign-teacher)**:
  - Khởi tạo CQRS: [`AssignTeacherDtos.cs`](../V-Eval-Practice_Service.Application/Features/Classes/DTOs/AssignTeacherDtos.cs), `AssignTeacherCommand.cs`, `AssignTeacherCommandValidator.cs`, [`AssignTeacherCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Classes/Commands/AssignTeacher/AssignTeacherCommandHandler.cs).
  - Tầng API: [`ClassesController.cs`](../V-Eval-Practice_Service.API/Controllers/ClassesController.cs) endpoint `[HttpPut("{classId:guid}/assign-teacher")]`.
  - Nghiệp vụ: Cập nhật `TeacherId`, `AssignedBy` (từ Header `X-User-Id`), `AssignedAt = UtcNow` cho thực thể `Class`.
- **API 10: Lấy Thời Khóa Biểu Các Buổi Live Q&A Của Lớp Cơ Sở Học Sinh Ghi Danh (GET /api/v1/practice/live-sessions/my-schedule)**:
  - Khởi tạo CQRS: [`GetMyLiveScheduleDtos.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/GetMyLiveScheduleDtos.cs), `GetMyLiveScheduleQuery.cs`, [`GetMyLiveScheduleQueryHandler.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/Queries/GetMyLiveSchedule/GetMyLiveScheduleQueryHandler.cs).
  - Tầng API: [`LiveSessionsController.cs`](../V-Eval-Practice_Service.API/Controllers/LiveSessionsController.cs) endpoint `[HttpGet("my-schedule")]`.
  - Nghiệp vụ: Truy vấn thông tin lớp học học sinh đang ghi danh (`ClassEnrollments`), nạp danh sách các buổi học Live Q&A của lớp, bóc tách trạng thái điểm danh cá nhân (`ATTENDED`, `ABSENT`, `NOT_ATTENDED`), link video ghi hình và cờ `IsMakeupQuizPassed`.
- **API 11: Tham Gia Buổi Học Trực Tuyến Live Q&A & Ghi Nhận Dấu Vết Vào Lớp (POST /api/v1/practice/live-sessions/{sessionId}/join)**:
  - Khởi tạo CQRS: [`JoinLiveSessionDtos.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/DTOs/JoinLiveSessionDtos.cs), `JoinLiveSessionCommand.cs`, `JoinLiveSessionCommandValidator.cs`, [`JoinLiveSessionCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/LiveSessions/Commands/JoinLiveSession/JoinLiveSessionCommandHandler.cs).
  - Tầng API: [`LiveSessionsController.cs`](../V-Eval-Practice_Service.API/Controllers/LiveSessionsController.cs) endpoint `[HttpPost("{sessionId:guid}/join")]`.
  - Nghiệp vụ: Cung cấp đường dẫn phòng học trực tuyến (`MeetingUrl`), ghi nhận thời điểm vào lớp `JoinedAt = UtcNow`. Bảo lưu độc quyền điểm danh chuyên cần (`ATTENDED` hoặc `ABSENT`) cho Giảng viên tại API 12 (không tự ý ghi đè trạng thái điểm danh khi học sinh chỉ mới click vào link phòng học).
- **Kiểm Thử Vận Hành Trực Tiếp (Live End-to-End Test)**:
  - Solution biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kịch bản API 9: Phân công giáo viên `99999999-9999-9999-9999-999999999999` cho lớp `33333333-3333-3333-3333-333333333333` -> `200 OK`.
  - Kịch bản API 8: Tạo buổi Live Q&A chuyên sâu -> `201 Created` tự động kế thừa `TeacherId`.
  - Kịch bản API 10: Tra cứu thời khóa biểu -> `200 OK` hiển thị đầy đủ danh sách các buổi Live và trạng thái điểm danh.
  - Kịch bản API 11: Học sinh `11111111-1111-1111-1111-111111111111` tham gia buổi Live -> `200 OK`, trả về URL phòng học, lưu vết thời điểm `JoinedAt`, bảo lưu trạng thái chờ Giảng viên đánh giá chuyên cần tại API 12.

---

## [29/09/2026] - Triển Khai Hoàn Thiện API 7: Nộp Bài Quiz Bù Cho Học Sinh Vắng Mặt Buổi Live Q&A (POST /api/v1/practice/roadmaps/nodes/{nodeId}/submit-makeup-quiz)
- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Commands/SubmitMakeupQuiz)**:
  - Khởi tạo DTOs [`SubmitMakeupQuizDtos.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/SubmitMakeupQuizDtos.cs): `SubmitMakeupQuizRequestDto`, `SubmitMakeupQuizResponseDto`.
  - Xây dựng FluentValidation `SubmitMakeupQuizCommandValidator` kiểm tra ràng buộc đầu vào.
  - Triển khai `SubmitMakeupQuizCommand` và [`SubmitMakeupQuizCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/SubmitMakeupQuiz/SubmitMakeupQuizCommandHandler.cs):
    1. Kiểm tra tồn tại chặng học (`404 Not Found`) và phân quyền sở hữu học sinh (`403 Forbidden`).
    2. Kiểm tra State Machine: Chặn nếu chặng học bị khóa (`LOCKED`) hoặc đã cắt tỉa (`SKIPPED_PRUNED`).
    3. Kiểm tra điều kiện tiên quyết xem video: Bắt buộc `node.IsVideoCompleted == true` (xem $\ge 80\%$ video bài giảng lý thuyết).
    4. Kiểm tra buổi Live Q&A và trạng thái điểm danh: Bắt buộc chặng học có liên kết buổi Live (`node.LiveSessionId != null`) và học sinh có trạng thái điểm danh là `ABSENT` trong `LiveSessionAttendance` (chặn `400 BadRequest` nếu không thuộc diện vắng mặt).
    5. Tự động khởi tạo hoặc nạp đề Quiz bù từ Content Service qua gRPC `GetMilestoneQuizAsync` (nếu chưa gán).
    6. Lấy bảng đáp án gốc bảo mật từ Content Service qua gRPC `GetExamAnswerKeysAsync(attendance.MakeupQuizId)`.
    7. Chấm điểm chi tiết từng câu hỏi, lưu bản ghi làm bài vào `ExamSubmissions` (`ExamType = "MAKEUP_QUIZ"`).
    8. Cập nhật `attendance.IsMakeupQuizPassed = isPassed` ($\ge 60\%$).
    9. **Kích Hoạt Máy Trạng Thái Hữu Hạn (FSM)**:
       - Nếu vượt qua bài Quiz bù VÀ học sinh đã vượt qua cả bài Quiz củng cố chuyên đề (`node.IsQuizPassed == true`): Hệ thống chính thức gỡ bỏ điều kiện phong tỏa do vắng mặt, đánh dấu chặng `Status = "COMPLETED"`, tăng `roadmap.CompletedMilestones++` và tự động mở khóa chặng `LOCKED` kế tiếp thành `IN_PROGRESS` (`UnlockedAt = UtcNow`).
       - Nếu trượt bài Quiz bù ($< 60\%$): Chặng tiếp tục bị giữ ở `IN_PROGRESS`, nhắc học sinh xem lại video ghi hình buổi Live (`recording_url`) và làm lại bài Quiz bù.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpPost("nodes/{nodeId:guid}/submit-makeup-quiz")]` kèm bóc tách `X-User-Id` header xác thực phân quyền.
- **Kiểm Thử Vận Hành Trực Tiếp (Live End-to-End Test)**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp 5 kịch bản:
    1. Chặn khi chưa xem đủ 80% video lý thuyết: Trả về `400 BadRequest` chuẩn xác.
    2. Chặn khi học sinh không thuộc diện `ABSENT`: Trả về `400 BadRequest` chuẩn xác.
    3. Nộp bài Quiz củng cố khi đang bị `ABSENT`: Ghi nhận 100% điểm quiz củng cố nhưng State Machine chặn không cho hoàn thành chặng (chờ Quiz bù).
    4. Nộp bài Quiz bù điểm dưới 60%: Trả về `scorePercentage: 0%`, `isPassed: false`, chặng học giữ `IN_PROGRESS`.
    5. Nộp bài Quiz bù đạt chuẩn $\ge 60\%$ (100%): Gỡ bỏ hoàn toàn phong tỏa chặng, Node 2 ("Đại số, Hàm số & Giải tích") chuyển thành `COMPLETED`, tự động mở khóa Node 3 ("Ngữ pháp & Logic câu Tiếng Việt") thành `IN_PROGRESS`, `CompletedMilestones` tăng lên 2/437!

---

## [29/09/2026] - Triển Khai Hoàn Thiện API 6: Nộp Bài Quiz Củng Cố & Kích Hoạt Máy Trạng Thái Mở Khóa Chặng (POST /api/v1/practice/roadmaps/nodes/{nodeId}/submit-quiz)
- **Cơ Sở Dữ Liệu PostgreSQL & Entity Framework Core**:
  - Bổ sung 2 cột lưu vết điểm số vào bảng `v_eval_practice."RoadmapNodes"`: `quiz_score DOUBLE PRECISION DEFAULT 0.0`, `is_quiz_passed BOOLEAN DEFAULT FALSE`.
  - Cập nhật entity [`RoadmapNode.cs`](../V-Eval-Practice_Service.Domain/Entities/RoadmapNode.cs) và mapping trong `PracticeDbContext.cs`.
  - Bổ sung phương thức `GetNextLockedNodeAsync(Guid roadmapId, int currentStepOrder)` vào `ILearningRoadmapRepository` và hiện thực trong `LearningRoadmapRepository.cs` phục vụ tìm kiếm mốc học tập kế tiếp để mở khóa.
- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Commands/SubmitMilestoneQuiz)**:
  - Khởi tạo DTOs [`SubmitMilestoneQuizDtos.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/SubmitMilestoneQuizDtos.cs): `SubmitMilestoneQuizRequestDto`, `MilestoneQuizAnswerItemDto`, `SubmitMilestoneQuizResponseDto`, `MilestoneQuizQuestionResultDto`.
  - Xây dựng FluentValidation `SubmitMilestoneQuizCommandValidator` kiểm tra ràng buộc dữ liệu đầu vào.
  - Triển khai `SubmitMilestoneQuizCommand` và [`SubmitMilestoneQuizCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/SubmitMilestoneQuiz/SubmitMilestoneQuizCommandHandler.cs):
    1. Kiểm tra tồn tại chặng học (`404 Not Found`) và kiểm soát phân quyền học sinh (`403 Forbidden`).
    2. Kiểm tra State Machine: Chặn nếu chặng đang bị khóa (`LOCKED` -> `400 BadRequest`) hoặc đã cắt tỉa (`SKIPPED_PRUNED` -> `400 BadRequest`).
    3. Kiểm tra điều kiện tiên quyết xem video: Bắt buộc `node.IsVideoCompleted == true` (xem $\ge 80\%$ video) mới được phép nộp bài Quiz.
    4. Tự động lấy bảng đáp án gốc bảo mật từ Content Service qua gRPC `GetExamAnswerKeysAsync(node.QuizExamId)`.
    5. Chấm điểm chi tiết từng câu hỏi, tính `ScorePercentage = Math.Round((totalCorrect / totalQuestions) * 100.0, 2)`.
    6. Lưu bản ghi nộp bài vào `ExamSubmissions` (`ExamType = "QUIZ_MILESTONE"`) và `SubmissionAnswers` chi tiết từng câu.
    7. **Kích hoạt Finite State Machine (FSM)**:
       - Nếu điểm $\ge 60\%$: Đánh dấu chặng hiện tại `Status = "COMPLETED"`, cập nhật `CompletedAt = UtcNow`, tăng `roadmap.CompletedMilestones++` (nếu hoàn thành hết thì `roadmap.Status = "COMPLETED"`). Tự động tìm chặng `LOCKED` kế tiếp qua `GetNextLockedNodeAsync` và chuyển thành `Status = "IN_PROGRESS"` (`UnlockedAt = UtcNow`).
       - Nếu điểm $< 60\%$: Giữ chặng hiện tại ở `IN_PROGRESS`, không mở khóa chặng sau, trả về thông báo sư phạm yêu cầu xem lại video và làm lại bài Quiz.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpPost("nodes/{nodeId:guid}/submit-quiz")]` kèm bóc tách `X-User-Id` header xác thực phân quyền.
- **Kiểm Thử Vận Hành Trực Tiếp (Live End-to-End Test)**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp 2 kịch bản chính:
    1. **Kịch bản điểm dưới 60% (Failing)**: Trả về `scorePercentage: 0%`, `isPassed: false`, chặng học giữ nguyên `IN_PROGRESS`, không mở khóa chặng sau.
    2. **Kịch bản điểm đạt chuẩn >= 60% (Passing 100%)**: Trả về `scorePercentage: 100%`, `isPassed: true`, chặng 1 chuyển thành `COMPLETED`, chặng 2 ("Đại số, Hàm số & Giải tích") tự động được mở khóa thành `IN_PROGRESS`, `CompletedMilestones` tăng từ 0 lên 1, lưu vết bản ghi chấm thi đầy đủ vào CSDL Supabase.

---

## [29/09/2026] - Triển Khai Hoàn Thiện API 5: Lấy Đề Thi Quiz Củng Cố Của Chặng Học (GET /api/v1/practice/roadmaps/nodes/{nodeId}/quiz)
- **Hợp Đồng Giao Thức gRPC Liên Dịch Vụ (`content.proto`)**:
  - Bổ sung RPC `GetMilestoneQuiz (GetMilestoneQuizRequest) returns (GetMilestoneQuizResponse)` vào cả 3 vị trí hợp đồng (`grpc/content.proto`, Content Service và Practice Service).
  - Triển khai `GetMilestoneQuiz` trong `ContentGrpcService.cs` (Content Service): Nạp câu hỏi theo `ExamId` hoặc `SkillId`, tự động gán đề thi `MockExam` và liên kết `ExamQuestions`, đồng thời **ẩn hoàn toàn đáp án đúng** (`is_correct`, `correct_option`) để bảo mật đề thi tuyệt đối khi gửi về cho học sinh.
- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Queries/GetMilestoneQuiz)**:
  - Khởi tạo DTO [`MilestoneQuizDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/MilestoneQuizDto.cs), `MilestoneQuizQuestionItemDto`, `MilestoneQuizOptionDto`.
  - Mở rộng `IContentGrpcClient` và `ContentGrpcClient` hiện thực `GetMilestoneQuizAsync`.
  - Triển khai `GetMilestoneQuizQuery` và [`GetMilestoneQuizQueryHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetMilestoneQuiz/GetMilestoneQuizQueryHandler.cs):
    1. Kiểm tra tồn tại chặng học: Trả về `404 Not Found` nếu không tìm thấy `NodeId`.
    2. Kiểm soát phân quyền: Chặn học sinh truy cập bài kiểm tra chặng học của người khác (`403 Forbidden`).
    3. Kiểm tra State Machine: Chặn nếu chặng đang bị khóa (`LOCKED` -> `400 BadRequest`) hoặc đã cắt tỉa (`SKIPPED_PRUNED` -> `400 BadRequest`).
    4. Kiểm tra điều kiện tiên quyết xem video lý thuyết (Prerequisite Check): Bắt buộc học sinh xem $\ge 80\%$ thời lượng bài giảng trước (`IsVideoCompleted = true`), chặn nếu chưa xem đủ (`400 BadRequest` - `RoadmapNode.VideoNotCompleted`).
    5. Gọi gRPC Content Service nạp danh sách 5 câu hỏi củng cố (ẩn đáp án đúng).
    6. Tự động liên kết `QuizExamId` vào `RoadmapNode` và lưu trữ nguyên tử vào CSDL Supabase PostgreSQL.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpGet("nodes/{nodeId:guid}/quiz")]` kèm bóc tách `X-User-Id` header xác thực phân quyền.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Cả 2 solution `V-Eval-Content_Service.sln` và `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp 3 kịch bản:
    1. Lấy đề thi Quiz cho chặng đủ điều kiện: Trả về `200 OK` đầy đủ 5 câu hỏi kèm lựa chọn A, B, C, D (ẩn đáp án đúng), lưu vết `QuizExamId` vào DB.
    2. Chặn chặng đang bị khóa (`LOCKED`): Trả về `400 BadRequest` chuẩn xác.
    3. Chặn học sinh khác truy cập trái phép: Trả về `403 Forbidden` chuẩn xác.

---

## [29/09/2026] - Triển Khai Hoàn Thiện API 4: Ghi Nhận Tiến Độ Xem Video Lý Thuyết (POST /api/v1/practice/roadmaps/nodes/{nodeId}/track-video)
- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Commands/TrackVideo)**:
  - Khởi tạo DTOs [`TrackVideoRequestDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/TrackVideoRequestDto.cs) và [`TrackVideoResponseDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/TrackVideoResponseDto.cs):
    - Nhận: `WatchedDurationSeconds` (thời gian đã xem), `TotalDurationSeconds` (tổng thời lượng video).
    - Trả về: `NodeId`, `WatchedDurationSeconds`, `TotalDurationSeconds`, `WatchPercentage`, `IsQuizEligible`, `Status`, `Message`.
  - Khởi tạo FluentValidation `TrackVideoCommandValidator`:
    - Ràng buộc: `NodeId` không rỗng, `WatchedDurationSeconds >= 0`, `TotalDurationSeconds > 0`, `WatchedDurationSeconds <= TotalDurationSeconds`.
  - Triển khai `TrackVideoCommand` và [`TrackVideoCommandHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/TrackVideo/TrackVideoCommandHandler.cs):
    1. Truy vấn `ILearningRoadmapRepository.GetNodeByIdAsync`. Trả về `404 Not Found` nếu không tìm thấy chặng học.
    2. Kiểm soát phân quyền: chặn học sinh cập nhật tiến độ chặng học của học sinh khác (`403 Forbidden`).
    3. Kiểm tra State Machine: Chặn nếu chặng học đang bị khóa (`LOCKED` -> `400 BadRequest`) hoặc đã được cắt tỉa (`SKIPPED_PRUNED` -> `400 BadRequest`).
    4. Cập nhật tiến độ xem lũy tiến (giữ giá trị xem cao nhất: `Math.Max(node.VideoWatchedSeconds, watched)`).
    5. Tính toán tỷ lệ phần trăm xem bài giảng (`WatchPercentage`).
    6. Áp dụng quy tắc mở khóa bài Quiz củng cố: Yêu cầu xem đạt tối thiểu 80% thời lượng bài giảng lý thuyết (`watchPercentage >= 80.0` -> `IsVideoCompleted = true`, `IsQuizEligible = true`).
- **Cơ Sở Dữ Liệu PostgreSQL & Entity Framework Core**:
  - Bổ sung 3 trường vào bảng `v_eval_practice."RoadmapNodes"`: `video_watched_seconds INT DEFAULT 0`, `video_total_seconds INT DEFAULT 0`, `is_video_completed BOOLEAN DEFAULT FALSE`.
  - Cập nhật entity [`RoadmapNode.cs`](../V-Eval-Practice_Service.Domain/Entities/RoadmapNode.cs) và Fluent API cấu hình trong `PracticeDbContext.cs`.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpPost("nodes/{nodeId:guid}/track-video")]` kèm trích xuất `X-User-Id` header xác thực phân quyền.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp 4 kịch bản:
    1. Xem 40% (< 80%): Trả về `watchPercentage: 40%`, `isQuizEligible: false`.
    2. Xem 85% (>= 80%): Trả về `watchPercentage: 85%`, `isQuizEligible: true`, cập nhật `is_video_completed = true`.
    3. Học sinh khác can thiệp: Trả về `403 Forbidden` chuẩn xác.
    4. Cố tình ghi nhận chặng bị khóa (`LOCKED`): Trả về `400 BadRequest` chuẩn xác.

---

## [29/09/2026] - Triển Khai Hoàn Thiện API 3: Lấy Thông Tin Chi Tiết Chặng Học (GET /api/v1/practice/roadmaps/nodes/{nodeId})
- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Queries/GetRoadmapNodeDetail)**:
  - Khởi tạo DTO [`RoadmapNodeDetailDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapNodeDetailDto.cs) và `LiveSessionDetailDto` thể hiện đầy đủ 3 thành phần tích hợp:
    1. Bài giảng lý thuyết video (`material_id`).
    2. Bài Quiz củng cố 5-10 câu (`quiz_exam_id`).
    3. Buổi học Live Q&A cơ sở (`live_session_id`, `MeetingUrl`, `RecordingUrl`, `AttendanceStatus`, `IsMakeupQuizPassed`).
  - Triển khai `GetRoadmapNodeDetailQuery` và [`GetRoadmapNodeDetailQueryHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetRoadmapNodeDetail/GetRoadmapNodeDetailQueryHandler.cs):
    1. Truy vấn `ILearningRoadmapRepository.GetNodeByIdAsync` nạp thông tin Node, Roadmap và LiveSession.
    2. Kiểm tra quyền sở hữu bảo mật: ngăn chặn truy cập chặng học của học sinh khác (`403 Forbidden`).
    3. Nạp thông tin kỹ năng và miền năng lực từ Content Service qua gRPC (`GetSkillsTreeAsync`).
    4. Trích xuất chi tiết điểm danh và trạng thái bài Quiz bù (`MakeupQuizId`, `IsMakeupQuizPassed`) phục vụ Unhappy Case 3.
- **Hạ Tầng Repository Layer**:
  - Bổ sung `GetNodeByIdAsync` và `GetAttendanceAsync` vào `ILearningRoadmapRepository` và `LearningRoadmapRepository`.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpGet("nodes/{nodeId:guid}")]` kèm bóc tách `X-User-Id` header kiểm tra phân quyền.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp `GET /api/v1/practice/roadmaps/nodes/{nodeId}` thành công trả về `200 OK` đầy đủ 3 thành phần chặng học và kiểm thử chặn 404 chuẩn xác.

---

## [29/09/2026] - Triển Khai Hoàn Thiện API 2: Tra Cứu Lộ Trình Học Tập Cá Nhân Hóa (GET /api/v1/practice/roadmaps/my-roadmap)
- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Queries/GetMyRoadmap)**:
  - Khởi tạo DTO [`RoadmapTimelineDto.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapTimelineDto.cs) thể hiện toàn diện dòng thời gian học tập: `RoadmapId`, `StudentId`, `TargetScore`, `TotalMilestones`, `CompletedMilestones`, `ProgressPercentage`, `IsPruned`, `PrunedReason`, `Stages` (gom nhóm theo Miền năng lực) và `Nodes` (tuần tự stepOrder).
  - Triển khai `GetMyRoadmapQuery` và [`GetMyRoadmapQueryHandler.cs`](../V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetMyRoadmap/GetMyRoadmapQueryHandler.cs):
    1. Truy vấn lộ trình `ACTIVE` qua `ILearningRoadmapRepository.GetActiveByStudentIdAsync`. Trả về `404 Not Found` chuẩn nếu học sinh chưa khởi tạo lộ trình.
    2. Nạp metadata cây kỹ năng từ Content Service qua gRPC (`GetSkillsTreeAsync`) kèm cơ chế fallback an toàn nếu mất kết nối gRPC.
    3. Tính toán tiến độ phần trăm học tập hoàn thành thực tế (`ProgressPercentage`).
    4. Gom nhóm các chặng học thành các `Stages` theo từng Miền Năng Lực / Môn học (Ngôn ngữ, Toán - Logic, KHTN, KHXH).
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpGet("my-roadmap")]` hỗ trợ linh hoạt bóc tách `StudentId` từ Gateway Header (`X-User-Id`) hoặc query parameter `studentId`.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp `GET /api/v1/practice/roadmaps/my-roadmap?studentId=...` thành công trả về `200 OK` với đầy đủ timeline, thống kê tiến độ và phân nhóm 5 Stages.

---

## [29/09/2026] - Triển Khai Hoàn Thiện API 1: Khởi Tạo Lộ Trình Học Tập Cá Nhân Hóa (POST /api/v1/practice/roadmaps/generate) & Phân Nhóm Chặng Theo Miền Năng Lực (Group by Domain)
- **Kiến Trúc Clean Architecture & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps)**:
  - Khởi tạo DTOs: `GenerateRoadmapRequestDto`, `GenerateRoadmapResponseDto`, `RoadmapStageDto`, `RoadmapNodeSummaryDto`.
  - Bổ sung cấu trúc **Phân nhóm Chặng theo Miền Năng Lực (Group by Competency Domains / Stages)**: DTO đầu ra phân tách rõ ràng theo từng môn/miền (`DomainId`, `DomainName`, `TotalNodes`, `CompletedNodes`, danh sách `Nodes` giữ nguyên thứ tự `stepOrder` tối ưu).
  - Khởi tạo bộ xác thực FluentValidation: `GenerateRoadmapCommandValidator` (kiểm tra `StudentId`, `DiagnosticSubmissionId`, `ExamDate > UtcNow`, `StudyHoursPerDay` từ 0.5 đến 12h).
  - Triển khai `GenerateRoadmapCommand` và `GenerateRoadmapCommandHandler` hoàn tất quy trình 7 bước nghiệp vụ:
    1. Trích xuất hồ sơ Flow 1 (`theta_0`, `TargetScore`, `EnrolledClassId`, `P(L0)` từ `LearningProfiles`, cờ `IsWeak`).
    2. Nạp Cây khung năng lực kỹ năng chuẩn, cung tiên quyết và thông tin miền năng lực từ Content Service qua gRPC (`GetSkillsTreeAsync`).
    3. Kiểm tra chu trình kín với `TarjanCycleDetector` (chặn đứng vòng lặp phụ thuộc).
    4. Phân tích quỹ thời gian & cắt tỉa 3 tầng với `PathPruner`.
    5. Sắp xếp thứ tự học sư phạm đa tiêu chí với `TopologicalSorter`.
    6. Gắn kết 3 tài nguyên (`MaterialId`, `QuizExamId`, `LiveSessionId`) và khởi tạo State Machine (`MilestoneBinder`).
    7. Lưu vết các lộ trình cũ sang `ARCHIVED`, lưu trữ nguyên tử lộ trình mới và các chặng học vào PostgreSQL.
- **Hạ Tầng Repository & gRPC Client**:
  - Khởi tạo `ILearningRoadmapRepository` trong Application Layer và `LearningRoadmapRepository` trong Infrastructure Layer, đăng ký Scoped vào DI.
  - Mở rộng `content.proto` (bổ sung `double weight = 5`, `string domain_id = 6`, `string domain_name = 7`), cập nhật `IContentGrpcClient` và `ContentGrpcClient` hiện thực `GetSkillsTreeAsync`.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Cung cấp endpoint `POST /api/v1/practice/roadmaps/generate` kế thừa `ApiControllerBase`.
  - Hỗ trợ linh hoạt bóc tách `StudentId` từ Gateway Header (`X-User-Id`) hoặc payload trực tiếp.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp sinh lộ trình thành công trả về 5 Stages phân nhóm môn (Ngôn ngữ, Toán & Tư duy định lượng, Tổng hợp, Tự nhiên, Xã hội).

---

## [28/09/2026] - Triển Khai Giai Đoạn 2 Core Flow 2: Graph Engine 4 Thuật Toán Đồ Thị & Toán Học
- **Thuật Toán 1: `TarjanCycleDetector.cs` (Tarjan SCC)**:
  - Cài đặt thuật toán Tarjan tìm SCC phát hiện chu trình kín trong đồ thị tiên quyết kỹ năng.
- **Thuật Toán 2: `PathPruner.cs` (Path Pruning Engine)**:
  - Cài đặt chiến lược cắt tỉa 3 tầng: Trọng số < 5%, `P(L0) >= 85%`, dồn trọng tâm điểm rơi.
- **Thuật Toán 3: `TopologicalSorter.cs` (Kahn + Priority Queue)**:
  - Cài đặt thuật toán Kahn Topological Sort với hàm ưu tiên sư phạm đa tiêu chí.
- **Thuật Toán 4: `MilestoneBinder.cs` (Milestone Binding Engine)**:
  - Chuyển đổi danh sách kỹ năng Topo thành `RoadmapNode`, gắn kết 3 thành phần và khởi tạo State Machine.
- **Kiểm Thử Biên Dịch**: Solution biên dịch sạch 100% (**0 Warning, 0 Error**) sau mỗi thuật toán.

---

## [28/09/2026] - Khởi Tạo Thực Thể LearningRoadmaps, RoadmapNodes, LiveSessions & LiveSessionAttendance (Core Flow 2 - Giai Đoạn 1)
- **Thực Thể Lộ Trình Cá Nhân Hóa & Chặng Học (`LearningRoadmap.cs` & `RoadmapNode.cs`)**:
  - Khởi tạo entity [`LearningRoadmap.cs`](../V-Eval-Practice_Service.Domain/Entities/LearningRoadmap.cs) quản lý lộ trình học tập cá nhân hóa, liên kết với kết quả bài thi chẩn đoán `ExamSubmissions`, lưu trữ trạng thái cắt tỉa (`IsPruned`, `PrunedReason`) và tổng số mốc học.
  - Khởi tạo entity [`RoadmapNode.cs`](../V-Eval-Practice_Service.Domain/Entities/RoadmapNode.cs) đại diện cho từng chặng học (Milestone) tích hợp chặt chẽ 3 thành phần: Bài giảng lý thuyết (`material_id`), Bài Quiz củng cố (`quiz_exam_id`), và Buổi Live Q&A (`live_session_id`).
- **Thực Thể Lịch Học Trực Tuyến & Điểm Danh (`LiveSession.cs` & `LiveSessionAttendance.cs`)**:
  - Khởi tạo entity [`LiveSession.cs`](../V-Eval-Practice_Service.Domain/Entities/LiveSession.cs) quản lý các buổi học Live Q&A của lớp cơ sở, bổ sung trường `RecordingUrl` và `IsRecorded` phục vụ Unhappy Case 3 (xem lại video khi vắng mặt).
  - Khởi tạo entity [`LiveSessionAttendance.cs`](../V-Eval-Practice_Service.Domain/Entities/LiveSessionAttendance.cs) quản lý điểm danh và điều kiện vượt qua bài Quiz bù (`MakeupQuizId`, `IsMakeupQuizPassed`).
- **Cấu Hình CSDL EF Core 9 (`PracticeDbContext.cs`)**:
  - Đăng ký 4 `DbSet` mới: `LearningRoadmaps`, `RoadmapNodes`, `LiveSessions`, `LiveSessionAttendances` thuộc schema `v_eval_practice`.
  - Cấu hình khóa ngoại, quan hệ 1-N cascade và cascade delete hợp lý giữa Roadmaps - Nodes và LiveSessions - Attendances.
- **Kiểm Thử Biên Dịch & CSDL Supabase**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Khởi tạo thành công các bảng và trường tương ứng trên PostgreSQL Supabase.

---

## [28/09/2026] - Triển Khai Xử Lý Kịch Bản Ngoại Lệ (Unhappy Case 2): Khóa Bài Thi Quá Hạn 24h & Chống Gian Lận Nộp Lại
- **Kiểm Định Hết Hạn 24 Giờ & Tự Động Khóa Bài Thi (`SubmitDiagnosticCommandHandler.cs`)**:
  - Triển khai cơ chế kiểm tra thời gian làm bài: Nếu học sinh bỏ dở bài kiểm tra quá 24 giờ (`(DateTime.UtcNow - StartedAt).TotalHours > 24` hoặc tổng `TimeSpentSeconds > 86400`), hệ thống tự động đánh dấu phiên làm bài với `Status = "EXPIRED"`.
  - Tự động lưu bản ghi hết hạn vào bảng `ExamSubmissions` với điểm số bằng 0, không tính toán tham số năng lực $\theta_0$ nhằm bảo vệ độ tin cậy của mô hình psychometrics.
  - Trả về mã lỗi chuẩn RFC 7807 `Exam.Expired`, hướng dẫn học sinh làm lại bài chẩn đoán ngẫu nhiên mới.
- **Chống Gian Lận Nộp Lại Đề Thi Đã Khóa / Đã Hoàn Thành**:
  - Tự động kiểm tra lịch sử nộp bài: Nếu đề thi đã từng bị đánh dấu `EXPIRED`, hệ thống lập tức từ chối và trả về lỗi `Exam.Locked`.
  - Nếu đề thi đã được hoàn thành trước đó (`COMPLETED`), hệ thống từ chối nộp lại với mã `Exam.AlreadyCompleted` (HTTP 409 Conflict).
- **Mở Rộng DTO Phản Hồi (`DiagnosticSubmissionDtos.cs`)**:
  - Bổ sung trường `Status` vào `SubmitDiagnosticResponseDto` và `DiagnosticSubmissionSummaryDto`, đồng bộ trạng thái bài thi (`COMPLETED`, `EXPIRED`) trên toàn bộ các endpoint tra cứu.
- **Kiểm Thử Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).

---

## [27/09/2026] - Phát Hành Giao Diện Làm Thử Đề Thi Chẩn Đoán (30 Câu), AI Exam Studio (Custom Prompt, Bloom 6 Cấp, Lưu DB Chờ Duyệt) & Trực Quan Hóa Radar Chart
- **Phát Hành Giao Diện Web Khảo Sát Năng Lực Đầu Vào (`wwwroot/view-diagnostic.html`)**:
  - Xây dựng giao diện web độc lập phong cách Glassmorphism hiện đại (Inter, Outfit, KaTeX, Chart.js) hỗ trợ kiểm thử thực tế và mô phỏng luồng Core Flow 1.
  - Tích hợp 3 Tab hoàn chỉnh:
    1. **Tab 1: AI Exam Studio**: Giáo viên nhập prompt tùy biến, chọn 5 lĩnh vực môn học hoặc nhận diện tự động từ prompt, thiết lập mức độ Bloom (6 cấp) và số lượng câu hỏi.
    2. **Tab 2: Phòng Thi Học Sinh**: Làm bài thi, nộp bài hoặc dùng Demo Solver tự động điền.
    3. **Tab 3: Báo Cáo Năng Lực & Radar Chart**: Điểm IRT 2PL `\theta_0`, xếp lớp, radar chart, phân tích Bloom và BKT.
- **Nút Lưu CSDL Chờ Duyệt & Quy Trình Phê Duyệt**:
  - Bổ sung nút **`💾 Lưu Vào Database (Chờ Duyệt)`**: Lưu đề thi vào Supabase PostgreSQL qua `POST http://localhost:5249/api/v1/content/exams/import` với trạng thái mặc định **`IsPublished = false` (Chờ duyệt / Pending Approval)**.
  - Nút **`✅ ACCEPT: Phê Duyệt & Chuyển Sang Phòng Thi Học Sinh ➔`**: Tự động gọi `PATCH /api/v1/content/exams/{id}/publish` cập nhật trạng thái thành `IsPublished = true` (Đã duyệt) và chuyển đề thi sang phòng thi học sinh.
- **Chuẩn Hóa Thang Đo Tư Duy Bloom 6 Mức Độ (Revised Bloom's Taxonomy)**:
  - Khởi tạo hằng số `BloomTaxonomy.cs` định nghĩa 6 mức độ tư duy: 1. Nhận biết (Remembering), 2. Thông hiểu (Understanding), 3. Vận dụng (Applying), 4. Phân tích (Analyzing), 5. Đánh giá (Evaluating), 6. Sáng tạo (Creating).
  - Cập nhật `SubmitDiagnosticCommandHandler.cs` và `GetDiagnosticSubmissionById.cs` sử dụng nhãn chuẩn hóa Bloom.
- **Kích Hoạt Static Files Trong Pipeline Kestrel (`Program.cs`)**:
  - Đã thêm `app.UseStaticFiles()` giúp phục vụ trực tiếp `http://localhost:5261/view-diagnostic.html`.
- **Kiểm Thử Biên Dịch**: `dotnet build` đạt **0 Error(s), 0 Warning(s)**.

---

## [22/09/2026] - Hoàn Tất Core Flow 1 (Bước 4 & 5): Tích Hợp AI Diagnostic, Lưu Trữ BKT Priors & Tự Động Xếp Lớp Tại Campus
- **Triển Khai HTTP Client Kết Nối AI Subsystem (`IAiDiagnosticClient` & `AiDiagnosticClient`)**:
  - Xây dựng HTTP Client kết nối endpoint `POST /api/v1/diagnostic/analyze` của AI Engine (`http://localhost:8000`).
  - Gửi gói dữ liệu 30 câu hỏi kèm thời gian phản hồi (`time_spent_seconds`), độ khó và danh mục miền năng lực.
  - Tích hợp cơ chế **Resilient Local Fallback**: Nếu AI Engine tạm thời gián đoạn hoặc offline, hệ thống tự động kích hoạt bộ tính toán dự phòng cục bộ (ước lượng `\theta_0`, BKT Sigmoid, phân lớp và nhận xét chuẩn mực), đảm bảo bài nộp của học sinh không bao giờ bị nghẽn (Zero-Blocking SLA).
- **Mở Rộng Domain Entities & CSDL Supabase**:
  - `ExamSubmission`: Bổ sung các trường lưu trữ kết quả chẩn đoán: `Theta0` (IRT ability), `PlacementClass` (FOUNDATION / ACCELERATION / BREAKTHROUGH), `AiCommentary` (nhận xét sư phạm Socratic) và `EnrolledClassId` (khóa ngoại lớp học được xếp).
  - `LearningProfile`: Ánh xạ bảng `LearningProfiles` lưu trữ xác suất làm chủ ban đầu `P(L_0) \in [0.05, 0.95]` cho từng kỹ năng của học sinh làm giá trị tiên nghiệm cho mô hình BKT.
  - `Class` & `ClassEnrollment`: Ánh xạ bảng `Classes` và `ClassEnrollments` quản lý việc phân bổ học sinh vào lớp học tại cơ sở (`CampusId`) gắn liền với bài nộp chẩn đoán (`diagnostic_submission_id`).
- **Mở Rộng EF Core Persistence (`PracticeDbContext`)**:
  - Đăng ký `DbSet<LearningProfile>`, `DbSet<Class>`, `DbSet<ClassEnrollment>`.
  - Cấu hình Fluent API ánh xạ tương thích chuẩn xác với schema CSDL PostgreSQL Supabase.
- **Triển Khai Các Repositories Nghiệp Vụ**:
  - `ILearningProfileRepository` / `LearningProfileRepository`: Thực hiện upsert thông minh danh sách `P(L_0)` của học sinh vào bảng `LearningProfiles`.
  - `IClassEnrollmentRepository` / `ClassEnrollmentRepository`: Tìm kiếm lớp học đang hoạt động (`ACTIVE`) phù hợp với cấp độ phân lớp (`Nền tảng`, `Tăng tốc`, `Bứt phá`) tại cơ sở đã chọn (`CampusId`). Tự động khởi tạo lớp học nếu cơ sở chưa có lớp tương ứng và tạo bản ghi ghi danh (`ENROLLED`).
- **Nâng Cấp Use Case `SubmitDiagnosticCommandHandler`**:
  - Kết nối hoàn chỉnh chuỗi xử lý khép kín:
    1. Xác thực học sinh & Campus qua Identity gRPC (Bước 1).
    2. Lấy đáp án và metadata câu hỏi qua Content gRPC (Bước 2).
    3. Chấm điểm thô và ghi nhận vi mô thời gian từng câu (Bước 3).
    4. Gửi sang AI Engine tính toán IRT `\theta_0`, BKT `P(L_0)` và nhận xét sư phạm (Bước 4).
    5. Lưu `P(L_0)` vào `LearningProfiles`, tự động xếp lớp tại cơ sở và ghi nhận `ClassEnrollments` (Bước 5).
    6. Trả về DTO hoàn chỉnh gồm tọa độ biểu đồ Radar đa giác đối chiếu điểm mục tiêu (V-ACT target score) trong `< 2` giây (Happy Case).
- **Di Trú CSDL & Khắc Phục Schema PostgreSQL (`Program.cs`)**:
  - Bổ sung migration tự động trên startup: `ALTER TABLE practice.exam_submissions ADD COLUMN IF NOT EXISTS ...` (`theta_0`, `placement_class`, `ai_commentary`, `enrolled_class_id`).
  - Khởi tạo bảng `LearningProfiles`, `Classes`, `ClassEnrollments` trên schema CSDL Supabase.
- **Trực Quan Hóa Dữ Liệu Phản Hồi (Human-Friendly DTOs)**:
  - Bổ sung `SkillName`, `DomainId`, `DomainName` vào `QuestionResultDto` và `SkillDiagnosticDto`.
  - Mở rộng DTO `WeakSkills` dạng object trực quan (`skillId`, `skillName`, `domainName`, `accuracyPercentage`) thay vì chỉ trả về mảng UUID.
  - Tích hợp tên cơ sở đào tạo thực tế (`CampusName`) vào `SubmitDiagnosticResponseDto` và định dạng tên lớp học (`ClassName: "Lớp Nền tảng (Foundation) - Cơ sở ..."`).
  - Truyền dữ liệu miền năng lực thực tế sang AI Engine giúp Biểu đồ Radar đa giác phân tách đầy đủ các trục môn thi của đề ĐGNL ĐHQG-HCM.
- **Kiểm Thử Vận Hành & End-to-End Trực Tiếp Qua Swagger**:
  - Biên dịch toàn bộ giải pháp `V-Eval-Practice_Service.sln`: **0 Warning(s), 0 Error(s)**.
  - Kiểm thử trực tiếp `POST /api/v1/practice/diagnostic-submissions` trên Swagger UI với 30 câu hỏi thật: Nhận kết quả thành công HTTP 200 OK với đầy đủ `theta_0 = -1.5`, xếp lớp `FOUNDATION`, tự động tạo lớp học tại cơ sở, ghi danh học sinh, lưu trữ 12 BKT Priors và nhận xét sư phạm Socratic.

---

## [20/09/2026] - Triển Khai Hoàn Thiện Clean Architecture 4 Tầng, Core Flow 1 (Bước 3: Chấm Điểm Chẩn Đoán 30 Câu & Tích Hợp gRPC)
- **Triển Khai Chuẩn Kiến Trúc Clean Architecture 4 Tầng**:
  - `Domain Layer`: Xây dựng thực thể `ExamSubmission` (thang điểm 0–30, tổng câu 30, thời gian), `SubmissionAnswer` (ghi nhận chi tiết từng câu: `selected_option`, `is_correct`, `time_spent_seconds`) và `IExamSubmissionRepository`.
  - `Application Layer`: Đồng bộ 100% **Result Pattern** (`Result<T>`, `Error`, `ErrorType`), tích hợp **FluentValidation Pipeline** qua `ValidationBehavior`, CQRS MediatR trọn bộ `SubmitDiagnosticCommand`, `GetDiagnosticSubmissionByIdQuery`, `GetDiagnosticSubmissionsByStudentQuery`.
  - `Infrastructure Layer`: Cấu hình EF Core Npgsql trên CSDL Supabase PostgreSQL schema `practice` (`exam_submissions`, `submission_answers`), triển khai gRPC Clients kết nối Identity Service (port 5156) và Content Service (port 5250).
  - `API Layer`: Xây dựng `ApiControllerBase` chuẩn hóa RFC 7807 ProblemDetails, `DiagnosticSubmissionsController` (`[Route("api/v1/practice/diagnostic-submissions")]`), `GlobalExceptionHandlerMiddleware`, Swagger UI tại `http://localhost:5261/swagger`.
- **Hoàn Tất Core Flow 1 (Bước 3)**:
  - Tiếp nhận bài nộp 30 câu hỏi khảo sát chẩn đoán năng lực ban đầu.
  - Tự động gọi gRPC xác thực điều kiện học sinh và cơ sở đào tạo (`CampusId`) từ Identity Service.
  - Tự động gọi gRPC lấy bảng đáp án bảo mật, độ khó câu hỏi và mã kỹ năng từ Content Service.
  - Tự động chấm điểm thô (thang 30), ghi nhận chi tiết thời gian phản hồi (`time_spent_seconds`) từng câu.
  - Chẩn đoán phân tích năng lực: Thống kê tỷ lệ đúng theo Kỹ năng (`SkillBreakdown`), tự động gắn cờ kỹ năng yếu (`WeakSkillIds` có tỷ lệ đúng < 60%), và thống kê theo 4 mức độ khó (Dễ, Trung bình, Khó, Rất khó).
  - Lưu trữ toàn bộ kết quả vào Supabase schema `practice`.
- **Tối Ưu Hóa Trải Nghiệm API (DTO Separation Pattern)**:
  - Tách bạch DTO tóm tắt `DiagnosticSubmissionSummaryDto` cho API lấy lịch sử học sinh (`GET /student/{studentId}`): Chỉ trả về thông số tổng quan (Điểm số, số câu đúng/sai, % chính xác, tổng thời gian, ngày nộp), loại bỏ mảng câu hỏi cồng kềnh giúp tối ưu băng thông và tải trang cực nhanh.
  - Giữ trọn vẹn chi tiết đầy đủ 30 câu hỏi kèm đáp án đúng/sai, thời gian phản hồi và phân tích kỹ năng/độ khó tại API tra cứu chi tiết (`GET /{id}`).
- **Kiểm Thử Toàn Diện (End-to-End Test)**:
  - Biên dịch giải pháp `V-Eval-Practice_Service.sln`: **0 Error(s), 0 Warning(s)**.
  - Chạy kịch bản tích hợp liên dịch vụ 3 microservices (Identity 5155/5156, Content 5249/5250, Practice 5261): Thành công 100%!
- **📌 Kế Hoạch Phối Hợp Kỹ Thuật Bước 4 (Core Flow 1 - Phân Tích Năng Lực IRT & BKT với AI Subsystem)**:
  - **Trách nhiệm của Practice Service**:
    1. Chuẩn bị hợp đồng giao tiếp (gRPC Client `IAiEngineGrpcClient` hoặc Event Bus): Đóng gói gói dữ liệu bài làm của học sinh gồm: `submission_id`, `student_id`, tổng điểm thô (0–30) và danh sách chi tiết 30 câu hỏi (`question_id`, `skill_id`, `difficulty_level`, `is_correct`, `time_spent_seconds`).
    2. Gọi sang **`V-Eval-Ai_Engine`** để kích hoạt tiến trình phân tích trí tuệ nhân tạo.
    3. Nhận phản hồi từ AI Engine gồm:
       - Chỉ số năng lực tiềm ẩn ban đầu `\theta_0` (Theta IRT).
       - Ma trận xác suất làm chủ ban đầu `P(L_0)` cho từng Kỹ năng thành phần (Knowledge Component - KC).
       - Tọa độ vector biểu đồ Radar (6–8 trục năng lực) đối chiếu với điểm kỳ vọng (`TargetScore` thang 1200).
    4. Lưu trữ vector năng lực vào CSDL `practice` và kích hoạt Bước 5 (So sánh `\theta_0` với ngưỡng để phân lớp tại cơ sở).
  - **Trách nhiệm của AI Engine (`V-Eval-Ai_Engine`)**:
    1. Nhận vector dữ liệu 30 câu hỏi từ Practice Service.
    2. Chạy thuật toán **Item Response Theory (IRT)** (mô hình 2PL/3PL) dựa trên độ khó câu hỏi (`b`), độ phân biệt (`a`) và kết quả đúng/sai kèm thời gian phản hồi để ước lượng `\theta_0`.
    3. Chạy mô hình **Bayesian Knowledge Tracing (BKT)**: Khởi tạo xác suất làm chủ tri thức ban đầu `P(L_0)` cho từng KC theo các tham số định chuẩn khoa học (`P(L_0)`, `P(T)`, `P(S)`, `P(G)`).
    4. Trực quan hóa dữ liệu biểu đồ Radar đa giác năng lực gửi ngược về cho Practice Service / Frontend.

---

## [18/09/2026] - Phát Hành Công Cụ Push Độc Lập `Scripts/push.bat` & Chuẩn Hóa Bộ Docs
- **Khởi Tạo `Scripts/push.bat`**: Đóng gói công cụ push độc lập hỗ trợ 3 chế độ (nhánh hiện tại, danh sách số nhánh có sẵn, tạo nhánh mới).
- **Chuẩn Hóa Bộ Docs Service**: Đồng bộ hệ thống tài liệu theo 3 file chuẩn `daily.md`, `process.md` và `architecture_acceptance.md`.

---

## [15/09/2026] - Dockerize Practice Service (Milestone 2)
- **Tạo `Dockerfile` Multi-stage**: Build và publish .NET 9 API image trên cổng `5002`.
- **Tích hợp Docker Compose**: Khai báo container `v_eval_practice_service` tham gia mạng `veval_network`.

---

## [14/09/2026] - Security & Configuration (Milestone 1)
- **Khởi Tạo `appsettings.example.json`**: Tạo file mẫu chứa đầy đủ `ConnectionStrings` và `JwtSettings` với label mẫu.
- **Bảo Mật Git Security**: Cập nhật `.gitignore` ẩn tất cả file `appsettings.json` chứa thông tin nhạy cảm.
- **Định Tuyến Gateway YARP**: Định tuyến `/api/practice/{**catch-all}` tại Gateway V-Eval trỏ về cổng `:5002`.
