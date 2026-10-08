# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [08/10/2026] - Triển Khai Hoàn Tất Core Flow 3: API 3 - Ghi Nhận Tiến Độ Video & Mở Khóa Bước APPLY (TrackVideo)

- **Hiện Thực API 3: Ghi Nhận Tiến Độ Video Bài Giảng Bước LEARN (`POST /api/practice/stages/{stageProgressId}/track-video`)**:
  - Tạo bộ DTOs [`TrackVideoDtos.cs`](./V-Eval-Practice_Service.Application/Features/Stages/DTOs/TrackVideoDtos.cs) (`TrackVideoRequestDto`, `TrackVideoResponseDto`).
  - Tạo Command `TrackVideoCommand.cs` và Validator `TrackVideoCommandValidator.cs` kiểm tra `WatchedSeconds >= 0`, `TotalSeconds > 0`.
  - Tạo Handler [`TrackVideoCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Commands/TrackVideo/TrackVideoCommandHandler.cs):
    1. Xác thực `StageProgressId` và kiểm tra quyền sở hữu `StudentId`.
    2. Tính toán tỷ lệ phần trăm xem lũy tiến `VideoWatchPercentage`, bảo toàn tiến độ tối đa không giảm khi tua ngược.
    3. Kiểm tra điều kiện mở khóa sư phạm: Khi `VideoWatchPercentage >= 80%` và `CurrentStep == LEARN`, tự động chuyển State Machine sang `CurrentStep = "APPLY"`.
    4. Tự động đồng bộ sang thực thể `RoadmapNode` (`IsVideoCompleted = true`, `VideoWatchedSeconds`, `VideoTotalSeconds`), đảm bảo liên kết toàn vẹn với lộ trình Core Flow 2.
    5. Lưu cập nhật qua `IStageProgressRepository.UpdateAsync`.
    6. Trả về thông điệp phản hồi và cờ điều hướng `nextAction = "START_ADAPTIVE_PRACTICE"`.
  - Bổ sung Action endpoint vào [`StagesController.cs`](./V-Eval-Practice_Service.API/Controllers/StagesController.cs): `[HttpPost("{stageProgressId:guid}/track-video")]`.
- **Đồng Bộ Kiến Trúc & Kiểm Thử Vận Hành**:
  - Cập nhật tài liệu nghiệm thu kiến trúc [`docs/architecture_acceptance.md`](./docs/architecture_acceptance.md) (Mục 7.4).
  - Cập nhật nhật ký tiến độ [`docs/daily.md`](./docs/daily.md) và bảng theo dõi [`docs/process.md`](./docs/process.md) (STT 49).
  - Kiểm thử trực tiếp qua PowerShell: Endpoint trả về mã `200 OK`, chuyển `currentStep: "APPLY"` thành công 100%.
  - Toàn bộ Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
