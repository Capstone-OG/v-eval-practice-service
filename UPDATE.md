# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [31/10/2026] - Khắc Phục Lỗi Serialization `snake_case` Trong `AiTutorClient` & Làm Giàu Ngữ Cảnh Socratic Fallback

- **Chuẩn Hóa Hợp Đồng JSON `snake_case` (`V-Eval-Practice_Service.Infrastructure/HttpClients/AiTutorClient.cs`)**:
  - Bổ sung `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` vào `JsonOptions` của `AiTutorClient` để các trường (`student_id`, `question_id`, `question_latex`, `options`, `student_selected_option`, `correct_option`, `skill_id`) khớp 100% với model Pydantic `SocraticAskRequest` của Python FastAPI RAG (`:8000`), khắc phục triệt để lỗi `422 Unprocessable Entity`.
  - Cải tiến `GenerateStreamFallback(payload)` lồng ghép trực tiếp nội dung đề bài và phương án học sinh đã chọn từ `payload.Options`.
- **Kiểm Thử & Xác Thực**:
  - Chạy `dotnet test` thành công 100% (4/4 unit tests pass).
