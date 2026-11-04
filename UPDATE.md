# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [04/11/2026] - Nâng Cấp AiTutor Hỗ Trợ Hội Thoại Tự Do, Tiếp Nhận Tuỳ Chọn Không Có Phương Án ('NONE') & Tích Hợp Chặn Lời Chào Tức Thì

- **Hỗ Trợ Hội Thoại Tự Do & Tiếp Nhận Nullable Options ([`AiTutorController.cs`](./V-Eval-Practice_Service.API/Controllers/AiTutorController.cs))**:
  - Mở rộng `AskTutorRequest` cho phép `StudentWrongOption` và `CorrectOption` là nullable (`string?`), mặc định gán `null`.
  - Chuẩn hoá các giá trị rỗng/khoảng trắng thành `"NONE"` khi gửi sang FastAPI RAG, giúp học sinh thoải mái trò chuyện tự do hoặc chào hỏi mà không bị ép buộc chọn phương án trắc nghiệm.
- **Bộ Chặn Lời Chào Tức Thì Trong Cơ Chế Dự Phòng ([`AiTutorClient.cs`](./V-Eval-Practice_Service.Infrastructure/HttpClients/AiTutorClient.cs))**:
  - Nhận diện lời chào (`hi`, `hello`, `chào`...) trong `GenerateStreamFallback`, phản hồi thông điệp chào mừng sư phạm tự nhiên, ấm áp, mời học sinh nêu rõ vấn đề bài tập cần giải đáp.
- **Kiểm Thử & Đóng Gói Vận Hành**:
  - `dotnet test` đạt 4/4 tests passed (100%), biên dịch sạch sẽ.
