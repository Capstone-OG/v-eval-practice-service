# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [29/09/2026] - Triển Khai Hoàn Thiện API 4: Ghi Nhận Tiến Độ Xem Video Lý Thuyết (POST /api/v1/practice/roadmaps/nodes/{nodeId}/track-video)

- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Commands/TrackVideo)**:
  - Khởi tạo DTO [`TrackVideoRequestDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/TrackVideoRequestDto.cs) và [`TrackVideoResponseDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/TrackVideoResponseDto.cs).
  - Triển khai `TrackVideoCommand` và [`TrackVideoCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/TrackVideo/TrackVideoCommandHandler.cs) xử lý:
    1. Kiểm soát phân quyền học sinh bảo mật (`403 Forbidden` khi cập nhật chặng học của người khác).
    2. Kiểm tra State Machine: Chặn ghi nhận nếu chặng học đang bị khóa (`LOCKED`) hoặc đã được cắt tỉa (`SKIPPED_PRUNED`).
    3. Cập nhật thời gian xem lũy tiến (`Math.Max(node.VideoWatchedSeconds, watched)`).
    4. Tính toán tỷ lệ xem bài giảng (`WatchPercentage`).
    5. Áp dụng quy tắc mở khóa bài Quiz củng cố: Yêu cầu xem đạt tối thiểu 80% thời lượng bài giảng lý thuyết (`watchPercentage >= 80.0` -> `IsVideoCompleted = true`, `IsQuizEligible = true`).
- **Cơ Sở Dữ Liệu PostgreSQL & Entity Framework Core**:
  - Bổ sung 3 trường vào bảng `v_eval_practice."RoadmapNodes"`: `video_watched_seconds INT DEFAULT 0`, `video_total_seconds INT DEFAULT 0`, `is_video_completed BOOLEAN DEFAULT FALSE`.
  - Cập nhật entity [`RoadmapNode.cs`](./V-Eval-Practice_Service.Domain/Entities/RoadmapNode.cs) và cấu hình Fluent API trong `PracticeDbContext.cs`.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpPost("nodes/{nodeId:guid}/track-video")]` kèm xác thực phân quyền qua `X-User-Id` header.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp 4 kịch bản vận hành đạt `200 OK`, `400 BadRequest` và `403 Forbidden` chuẩn xác.
