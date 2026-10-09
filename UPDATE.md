# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [09/10/2026] - Tích Hợp Động Trực Tiếp Content Service Cho Nhánh Cứu Trợ (Remedial Node) & Hoàn Tất Module 2

- **Loại Bỏ Hoàn Toàn Hardcode & Tích Hợp Động Content Service**:
  - Tích hợp 100% với `Content Service` qua gRPC:
    - Trong [`GetRemedialPackageQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Queries/GetRemedialPackage/GetRemedialPackageQueryHandler.cs): Lấy danh sách câu hỏi cơ bản và biến thể thật sự từ Ngân hàng đề của Content Service (`GetMilestoneQuizAsync`), tự động loại trừ các câu hỏi học sinh đã từng làm ở các đợt trước (`attemptedQuestionIds`) nhằm triệt tiêu hiện tượng học vẹt / nhớ vị trí đáp án cũ.
    - Trong [`SubmitRemedialCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Commands/SubmitRemedial/SubmitRemedialCommandHandler.cs): Xóa bỏ hoàn toàn hàm hardcode Guid cố định (`ResolveFallbackAnswer`), chuyển sang gọi trực tiếp `GetQuestionDetailAsync` sang Content Service qua gRPC để lấy đúng phương án chính xác (`IsCorrect`) và lời giải chi tiết (`Explanation`) thật từ CSDL của Content Service.
- **Khắc Phục Lỗi Concurrency Trong EF Core Repository**:
  - Cập nhật [`StageProgressRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/StageProgressRepository.cs): Gán tường minh `_context.Entry(progress).State = EntityState.Modified` khi thực hiện `UpdateAsync`, tránh việc EF Core duyệt graph navigation `AdaptiveAttempts` và sinh lệnh `UPDATE` nhầm trên các attempt mới thay vì `INSERT`.
  - Trong `SubmitRemedialCommandHandler`: Sử dụng `await _stageProgressRepository.AddAttemptAsync(...)` để ghi nhận từng attempt vào CSDL một cách an toàn và chuẩn xác.
- **Hiện Thực Module 2: Sổ Tay Lỗi Sai & Thuật Toán Lặp Lại Ngắt Quãng SM-2 (Mistake Notebook & Spaced Repetition)**:
  - Khởi tạo thực thể [`MistakeNotebook.cs`](./V-Eval-Practice_Service.Domain/Entities/MistakeNotebook.cs) và bảng CSDL `v_eval_practice."MistakeNotebooks"` kèm chỉ mục tối ưu `idx_mistake_notebook_daily_review`.
  - Cấu hình tự động lưu câu sai (Luật sư phạm BR-15) trong [`SubmitAnswerCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Commands/SubmitAnswer/SubmitAnswerCommandHandler.cs) khi học sinh làm sai ở bước `APPLY`.
  - Động cơ thích ứng SuperMemo-2 [`SpacedRepetitionCalculator.cs`](./V-Eval-Practice_Service.Application/Common/Adaptive/SpacedRepetitionCalculator.cs) chuẩn hóa khoảng cách ngày ôn tập: Lần 1 đúng (+1 ngày), Lần 2 đúng (+3 ngày), Lần 3+ đúng (nhân `EaseFactor`), và tự động gắn cờ `IsMastered = true` khi đúng liên tiếp 3 lần.
  - 4 API Endpoints trong [`MistakesController.cs`](./V-Eval-Practice_Service.API/Controllers/MistakesController.cs): `GET /mistakes`, `GET /mistakes/daily-review`, `POST /mistakes/{id}/tag-error`, `POST /mistakes/{id}/review-submit`.
- **Kiểm Thử Toàn Trình & Vận Hành Trực Tiếp (Live Services)**:
  - Đã khởi chạy đồng thời Content Service (gRPC 5250) và Practice Service (REST & Swagger 5261).
  - Kiểm thử toàn diện qua PowerShell: Khởi tạo chặng học -> Gọi cứu trợ bốc câu hỏi từ Content Service -> Nộp bài chấm điểm trực tiếp từ Content Service -> Nhận bộ đề biến thể mới ở đợt kế tiếp.
  - Toàn bộ Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
