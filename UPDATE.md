# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [29/09/2026] - Triển Khai Hoàn Thiện API 5: Lấy Đề Thi Quiz Củng Cố Của Chặng Học (GET /api/v1/practice/roadmaps/nodes/{nodeId}/quiz)

- **Hợp Đồng Giao Thức gRPC Liên Dịch Vụ (`content.proto`)**:
  - Bổ sung RPC `GetMilestoneQuiz` kết nối Practice Service và Content Service.
  - Phía Content Service: Nạp câu hỏi theo `ExamId` hoặc `SkillId`, tự động gán đề thi `MockExam` và liên kết `ExamQuestions`, đồng thời **ẩn hoàn toàn đáp án đúng** để chống gian lận.
- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Queries/GetMilestoneQuiz)**:
  - Khởi tạo DTO [`MilestoneQuizDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/MilestoneQuizDto.cs) và các DTO câu hỏi/lựa chọn.
  - Mở rộng `IContentGrpcClient` và `ContentGrpcClient` hiện thực `GetMilestoneQuizAsync`.
  - Triển khai `GetMilestoneQuizQuery` và [`GetMilestoneQuizQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetMilestoneQuiz/GetMilestoneQuizQueryHandler.cs):
    1. Kiểm tra tồn tại chặng học (`404 Not Found`).
    2. Kiểm soát phân quyền: Chặn học sinh truy cập bài thi của người khác (`403 Forbidden`).
    3. Kiểm tra State Machine: Chặn nếu chặng đang bị khóa (`LOCKED`) hoặc đã cắt tỉa (`SKIPPED_PRUNED`).
    4. Kiểm tra điều kiện tiên quyết xem video lý thuyết (Prerequisite Check): Bắt buộc học sinh xem $\ge 80\%$ thời lượng bài giảng trước (`IsVideoCompleted = true`), chặn nếu chưa xem đủ (`400 BadRequest`).
    5. Gọi gRPC Content Service nạp danh sách 5 câu hỏi củng cố (ẩn đáp án đúng).
    6. Tự động liên kết `QuizExamId` vào `RoadmapNode` và lưu trữ nguyên tử vào CSDL Supabase PostgreSQL.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpGet("nodes/{nodeId:guid}/quiz")]` kèm bóc tách `X-User-Id` header xác thực phân quyền.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Vận hành kiểm thử thực tế đạt `200 OK`, `400 BadRequest` và `403 Forbidden` chuẩn xác theo các kịch bản.
