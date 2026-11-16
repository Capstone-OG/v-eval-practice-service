# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [16/11/2026] - Mở Rộng Hợp Đồng API Gia Sư Socratic Tiếp Nhận & Chuyển Tiếp Lịch Sử Hội Thoại Đa Lượt Sang AI Engine & Đồng Bộ Phân Hệ Lịch Học, Học Phí Thực Tế

- **Mở Rộng DTO & Hợp Đồng Client AI Tutor ([`IAiTutorClient.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/IAiTutorClient.cs))**:
  - Khai báo record DTO mới: `ConversationTurnDto(string Role, string? Text, string? Option = null)`.
  - Bổ sung thuộc tính `List<ConversationTurnDto>? ConversationHistory = null` vào `SocraticAskRequestPayload`.
  - Cho phép serialization tự động với chuẩn snake_case `conversation_history` sang FastAPI RAG Service.
- **Tiếp Nhận & Chuyển Tiếp Lịch Sử Hội Thoại Trong Controller ([`AiTutorController.cs`](./V-Eval-Practice_Service.API/Controllers/AiTutorController.cs))**:
  - Mở rộng model `AskTutorRequest` tiếp nhận trường `List<ConversationTurnDto>? ConversationHistory = null`.
  - Trong phương thức stream `AskTutorStream` (`POST /api/practice/ai-tutor/ask-stream`), ánh xạ trực tiếp `ConversationHistory: request.ConversationHistory` vào lời gọi `_aiTutorClient.GetSocraticStreamAsync`.
  - Trong phương thức đồng bộ `AskTutor` (`POST /api/practice/ai-tutor/ask`), ánh xạ trực tiếp `ConversationHistory: request.ConversationHistory` vào `_aiTutorClient.AskSocraticJsonAsync`.
- **Đồng Bộ Phân Hệ Lịch Học, Điểm Danh, Học Phí & Radar Chart / Skill Priors**:
  - Tích hợp đầy đủ các aggregate roots `AcademicTerms`, `Courses`, `Classes`, `Schedules` và tra cứu học phí thực tế từ PayOS.
  - Bổ sung `RadarChart` và `SkillPriors` tính toán trực tiếp từ dữ liệu bài nộp cho `GetDiagnosticSubmissionById` và `SubmitDiagnosticCommandHandler`.
- **Kiểm Thử & Nghiệm Thu**:
  - Biên dịch toàn bộ solution `V-Eval-Practice_Service.sln` (.NET 9 SDK) sạch **0 Warning, 0 Error**.
