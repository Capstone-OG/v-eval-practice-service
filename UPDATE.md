# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [05/11/2026] - Khắc Phục Triệt Để Lỗi 400 Bad Request AiTutor, Tối Ưu Payload Lời Chào & Khởi Tạo Thành Công Lộ Trình Thích Ứng DAG

- **Linh Hoạt Hóa Request Model Khắc Phục Lỗi 400 Bad Request ([`AiTutorController.cs`](./V-Eval-Practice_Service.API/Controllers/AiTutorController.cs))**:
  - Chuyển đổi các thuộc tính bắt buộc `QuestionId`, `QuestionLatex`, `StudentWrongOption`, `CorrectOption` trong `AskTutorRequest` sang dạng nullable (`Guid?`, `string?`).
  - Xây dựng cơ chế tự động gán dữ liệu thông minh: khi `QuestionLatex` để trống, hệ thống sử dụng nội dung `StudentQuery`; khi `QuestionId` không truyền, tự động sinh UUID mới.
  - Ngăn chặn triệt để lỗi HTTP 400 ASP.NET Core model validation khi học sinh gửi câu hỏi mở hoặc lời chào.
- **Mở Rộng Nhận Diện Lời Chào Hội Thoại Tại Client Proxy ([`AiTutorClient.cs`](./V-Eval-Practice_Service.Infrastructure/HttpClients/AiTutorClient.cs))**:
  - Bổ sung nhận diện các cụm từ xưng hô tiếng Việt tự nhiên (`"hi gia sư"`, `"chào gia sư"`, `"thầy ơi"`, tiền tố `"hi "`, `"chào "`), đảm bảo phản hồi dự phòng luôn là lời chào sư phạm truyền cảm hứng thay vì công thức bài tập trắc nghiệm.
- **Kích Hoạt & Khởi Tạo Thành Công Lộ Trình Thích Ứng DAG (Core Flow 2)**:
  - Khởi tạo thành công bản ghi lộ trình học tập cá nhân hóa 470 chuyên đề qua `POST /api/practice/roadmaps/generate` từ bài thi khảo sát chẩn đoán đầu vào của học sinh.
  - Khắc phục lỗi 404 khi tra cứu `/api/practice/roadmaps/my-roadmap?studentId={id}`.
- **Kiểm Thử & Vận Hành**:
  - Biên dịch .NET 9 SDK đạt chuẩn 100% sạch (0 Warning, 0 Error).
  - Toàn bộ 12/12 kiểm thử tự động xUnit (`V-Eval-Practice_Service.Tests`) đạt Passed 100% trong 96ms.
