# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [09/10/2026] - Triển Khai Hoàn Hảo Module 2: Sổ Tay Lỗi Sai & Thuật Toán Lặp Lại Ngắt Quãng SM-2 (Mistake Notebook & Spaced Repetition)

- **Hiện Thực Trọn Vẹn Module 2 (Core Flow 3 - Interactive Learning)**:
  - Khởi tạo thực thể [`MistakeNotebook.cs`](./V-Eval-Practice_Service.Domain/Entities/MistakeNotebook.cs) và bảng CSDL `v_eval_practice."MistakeNotebooks"` kèm chỉ mục tìm kiếm tối ưu `idx_mistake_notebook_daily_review`.
  - Cấu hình Fluent API trong [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs) với kiểu dữ liệu chuẩn `.HasColumnType("date")` cho `NextReviewDate` nhằm tương thích 100% cơ chế Npgsql PostgreSQL UTC.
  - Tự động lưu vết câu sai (Luật sư phạm BR-15) trong [`SubmitAnswerCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Commands/SubmitAnswer/SubmitAnswerCommandHandler.cs) khi học sinh làm sai ở bước `APPLY`.
  - Xây dựng động cơ thích ứng SuperMemo-2 [`SpacedRepetitionCalculator.cs`](./V-Eval-Practice_Service.Application/Common/Adaptive/SpacedRepetitionCalculator.cs) chuẩn hóa khoảng cách ngày ôn tập: Lần 1 đúng (+1 ngày), Lần 2 đúng (+3 ngày), Lần 3+ đúng (nhân `EaseFactor`), và tự động gắn cờ `IsMastered = true` khi đúng liên tiếp 3 lần.
- **Bổ Sung 4 Endpoints RESTful Chuẩn Sư Phạm Trong [`MistakesController.cs`](./V-Eval-Practice_Service.API/Controllers/MistakesController.cs)**:
  1. `GET /api/practice/mistakes`: Tra cứu Sổ tay lỗi sai cá nhân, phân trang, lọc theo kỹ năng/trạng thái thành thạo, thống kê tổng số lỗi đã làm chủ (`masteredCount`) và chưa làm chủ (`unmasteredCount`).
  2. `GET /api/practice/mistakes/daily-review`: Lấy các câu hỏi ôn tập đến hạn hôm nay, tự động bốc câu hỏi biến thể (*Isomorphic Variant*) cùng dạng bài từ Content Service qua gRPC.
  3. `POST /api/practice/mistakes/{id}/tag-error`: Phản tư nhận thức (*Metacognition*), gắn nhãn lỗi (`CARELESS`, `MISREAD_QUESTION`, `MISSING_CONCEPT`) kèm ghi chú bài học kinh nghiệm.
  4. `POST /api/practice/mistakes/{id}/review-submit`: Nộp bài câu hỏi ôn tập biến thể, chấm điểm và tự động cập nhật lịch lặp lại ngắt quãng SM-2.
- **Kiểm Thử Vận Hành & Biên Dịch**:
  - Đã chạy kiểm thử tự động toàn trình qua PowerShell: Lưu câu sai -> Gắn nhãn nhận thức -> Lấy biến thể đến hạn -> Nộp 3 lần liên tiếp đạt `IsMastered = true` (Interval 1 -> 3 -> 7 ngày).
  - Toàn bộ Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
