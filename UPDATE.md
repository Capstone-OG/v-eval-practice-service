# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [09/10/2026] - Triển Khai Hoàn Hảo Nhánh Cứu Trợ Phụ Đạo (Remedial Node - BR-03) Động Theo Ngân Hàng Đề Content Service

- **Hiện Thực Nhánh Cứu Trợ Phụ Đạo Động (Remedial Node) Khép Kín Vòng Lặp Sư Phạm P-L-A-R**:
  - Bổ sung 2 endpoints chuyên trách trong [`StagesController.cs`](./V-Eval-Practice_Service.API/Controllers/StagesController.cs):
    1. `GET /api/practice/stages/{stageProgressId}/remedial`: Lấy gói can thiệp cứu trợ gồm video phụ đạo, tóm tắt phương pháp cốt lõi và 3 câu hỏi cơ bản ($b < 0.0$) truy vấn động từ Ngân hàng đề thông qua gRPC `IContentGrpcClient`.
    2. `POST /api/practice/stages/{stageProgressId}/remedial-submit`: Nộp bài gói cứu trợ, chấm điểm, reset bộ đếm sai (`ConsecutiveIncorrect = 0`), tự động khôi phục trạng thái `Status = IN_PROGRESS` để học sinh tự tin tiếp tục luyện tập thích ứng tại bước `APPLY`.
- **Loại Bỏ Hoàn Toàn Hardcode & Đồng Bộ gRPC Client**:
  - Xây dựng cơ chế tra cứu động dựa trên `SkillId` và `QuizExamId` của chặng học để bốc câu hỏi thật từ Ngân hàng đề `Content Service`.
  - Hỗ trợ cơ chế dự phòng an toàn (Fallback Graceful Degradation) khi tạm ngắt kết nối gRPC.
  - Chấm điểm và tra cứu đáp án thật thông qua `GetExamAnswerKeysAsync`.
- **Kiểm Thử & Vận Hành**:
  - `V-Eval-Practice_Service.Application` và toàn bộ solution biên dịch thành công 100% (**0 Warning, 0 Error**).
