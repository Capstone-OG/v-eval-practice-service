# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [12/10/2026] - Triển Khai Giai Đoạn 6 Core Flow 4 GraphRAG AI Tutor: Báo Cáo Học Sinh & Tích Hợp Mentor Resolution

- **Mở Rộng Giao Thức AI Tutor Client (`Application/Common/Interfaces/IAiTutorClient.cs`)**:
  - Bổ sung phương thức `ReportInteractionAsync(Guid interactionId, string? reason, Guid? studentId)` cho phép học sinh phản hồi khi AI gợi mở sai hoặc gặp ảo giác (hallucination).
  - Bổ sung phương thức `GetInteractionsAsync(bool reportedOnly, int limit, int offset)` phục vụ kiểm duyệt tương tác của Mentor.
  - Định nghĩa các DTO chuẩn: `ReportInteractionRequestPayload`, `ReportInteractionResponseDto`, `TutorInteractionItemDto`, `TutorInteractionTranscriptItemDto`.
- **Hiện Thực Hóa HTTP Client Phục Hồi Cao (`Infrastructure/HttpClients/AiTutorClient.cs`)**:
  - Triển khai gọi `POST /api/v1/socratic/interactions/{id}/report` và `GET /api/v1/socratic/interactions` kết nối tới AI Engine.
  - Tích hợp cơ chế Fallback xử lý cục bộ kiên cường khi dịch vụ AI Engine gặp sự cố mạng hoặc downtime tạm thời, bảo đảm trải nghiệm của học sinh không bị gián đoạn.
- **Xây Dựng Endpoints Báo Cáo & Kiểm Duyệt Tương Tác (`API/Controllers/AiTutorController.cs`)**:
  - `POST /api/practice/ai-tutor/report` (alias `/api/v1/ai-tutor/report`): Tiếp nhận báo cáo từ học sinh, tự động giải mã danh tính `StudentId` từ Gateway Header `X-User-Id`.
  - `GET /api/practice/ai-tutor/interactions`: Cung cấp danh sách tương tác kèm bộ lọc `reportedOnly` phục vụ thẩm định sư phạm của Mentor.
- **Hợp Nhất Toàn Diện Với Nhánh Develop (Core Flow 3 & Offline Micro-Groups)**:
  - Đồng bộ thành công cấu trúc `PracticeDbContext.cs` tích hợp đồng thời `ClassGroups`, `ClassGroupMembers` (Nhóm vi mô 3-5 học sinh), `MistakeNotebooks` (Sổ tay lỗi sai SM-2) và hạ tầng `IAiTutorClient` (Core Flow 4).
- **Kiểm Thử & Biên Dịch**:
  - Toàn bộ solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
