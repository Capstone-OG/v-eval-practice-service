# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [08/10/2026] - Triển Khai Hoàn Tất Core Flow 3: API 2 - Nộp Bài Quick Check & Chuyển Bước LEARN (SubmitPreview)

- **Hiện Thực API 2: Nộp Bài Khởi Động Quick Check Bước PREVIEW (`POST /api/practice/stages/{stageProgressId}/preview-submit`)**:
  - Tạo bộ DTOs [`SubmitPreviewDtos.cs`](./V-Eval-Practice_Service.Application/Features/Stages/DTOs/SubmitPreviewDtos.cs) (`SubmitPreviewRequestDto`, `PreviewAnswerItemDto`, `SubmitPreviewResponseDto`).
  - Tạo Command `SubmitPreviewCommand.cs` và Validator `SubmitPreviewCommandValidator.cs` kiểm tra `StageProgressId`, `StudentId`, và danh sách câu trả lời `Answers`.
  - Tạo Handler [`SubmitPreviewCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Commands/SubmitPreview/SubmitPreviewCommandHandler.cs):
    1. Kiểm tra tồn tại `StageProgress` và xác thực quyền sở hữu của `StudentId`.
    2. Kiểm tra State Machine: Chặn nộp lại nếu `CurrentStep` không phải là `PREVIEW`.
    3. Chấm điểm 3 câu hỏi Quick Check (so khớp đáp án từ Content Service hoặc bộ đáp án kiểm thử nội bộ).
    4. Cập nhật chuyển bước State Machine: `stage.AdvanceToLearn()`, chuyển `CurrentStep = "LEARN"`.
    5. Lưu cập nhật trạng thái qua `IStageProgressRepository.UpdateAsync`.
    6. Trả về kết quả chấm điểm, tổng số câu đúng và thông điệp hướng dẫn sư phạm (Pedagogical Feedback).
  - Bổ sung Action endpoint vào [`StagesController.cs`](./V-Eval-Practice_Service.API/Controllers/StagesController.cs): `[HttpPost("{stageProgressId:guid}/preview-submit")]`.
- **Đồng Bộ Kiến Trúc & Kiểm Thử Vận Hành**:
  - Cập nhật tài liệu nghiệm thu kiến trúc [`docs/architecture_acceptance.md`](./docs/architecture_acceptance.md) (Mục 7.3).
  - Cập nhật nhật ký tiến độ [`docs/daily.md`](./docs/daily.md) và bảng theo dõi [`docs/process.md`](./docs/process.md) (STT 48).
  - Kiểm thử trực tiếp qua PowerShell: Endpoint trả về mã `200 OK`, chuyển `currentStep: "LEARN"` thành công 100%.
  - Biên dịch toàn bộ Solution `V-Eval-Practice_Service.sln` sạch 100% (**0 Warning, 0 Error**).
