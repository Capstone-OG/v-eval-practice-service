# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [11/10/2026] - Triển Khai Giai Đoạn 5 Core Flow 4 GraphRAG AI Tutor: Tích Hợp Practice Service .NET 9 & SSE Streaming Proxy

- **Định Nghĩa Hợp Đồng Giao Tiếp Socratic AI Tutor (`Application/Common/Interfaces/IAiTutorClient.cs`)**:
  - Khởi tạo interface `IAiTutorClient` với phương thức stream thời gian thực `StreamSocraticGuidanceAsync` (`IAsyncEnumerable<string>`) và gọi đồng bộ `AskSocraticGuidanceAsync` (`Task<SocraticAskResponseDto?>`).
  - Thiết kế payload chuẩn `SocraticAskRequestPayload` và `SocraticAskResponseDto`.
- **Hiện Thực Hóa HTTP Client Phục Hồi Cao (`Infrastructure/HttpClients/AiTutorClient.cs`)**:
  - Kết nối FastAPI AI Engine tại `http://localhost:8000` (`AiSettings:ServiceUrl`).
  - Xử lý chuyển tiếp dòng dữ liệu SSE (`text/event-stream`) line-by-line với `StreamReader` và `HttpCompletionOption.ResponseHeadersRead`.
  - Phân tích cú pháp JSON payload của từng sự kiện token (`data: {"type": "token", "token": "..."}`) và yield trả về client.
  - Tích hợp cơ chế Fallback Sư Phạm Kiên Cường (Resilient Socratic Fallback): Tự động sinh lời gợi mở sư phạm chuẩn mực khi AI Engine quá tải hoặc gặp sự cố mạng, bảo đảm học sinh không bao giờ bị nghẽn luồng học tập.
  - Đăng ký `IAiTutorClient` Scoped vào DI container tại `Infrastructure/DependencyInjection.cs`.
- **Cấu Hình Giới Hạn Tần Suất Gọi API Chuẩn .NET 9 (`API/Program.cs`)**:
  - Cấu hình policy `StrictTutorPolicy` bằng `AddFixedWindowLimiter` giới hạn tối đa 10 requests / phút mỗi học sinh, từ chối với HTTP Status `429 Too Many Requests`.
  - Kích hoạt middleware `app.UseRateLimiter()` trong pipeline.
- **Xây Dựng Controller Chuyên Biệt (`API/Controllers/AiTutorController.cs`)**:
  - Ánh xạ route `/api/practice/ai-tutor` và alias `/api/v1/ai-tutor` với `[EnableRateLimiting("StrictTutorPolicy")]`.
  - Trích xuất an toàn `StudentId` từ Gateway Header `X-User-Id` (fallback về request payload).
  - Endpoint `POST /api/practice/ai-tutor/ask-stream`: Xuất dòng dữ liệu SSE chuẩn `text/event-stream; charset=utf-8`, phát `data: {"token": "..."}\n\n` và kết thúc bằng `data: [DONE]\n\n`.
  - Endpoint `POST /api/practice/ai-tutor/ask`: Trả về JSON đồng bộ phục vụ kiểm thử và ứng dụng không stream.
- **Kiểm Thử & Biên Dịch**:
  - Toàn bộ solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
