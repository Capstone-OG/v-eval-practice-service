# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [03/11/2026] - Mở Rộng Hợp Đồng API AI Tutor Hội Thoại Đa Lượt & Nâng Cấp Phản Hồi Dự Phòng Phổ Quát

- **Mở Rộng Hợp Đồng API AI Tutor (`StudentQuery`) ([`IAiTutorClient.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/IAiTutorClient.cs), [`AiTutorController.cs`](./V-Eval-Practice_Service.API/Controllers/AiTutorController.cs), [`AiTutorClient.cs`](./V-Eval-Practice_Service.Infrastructure/HttpClients/AiTutorClient.cs))**:
  - Hỗ trợ trường `StudentQuery` trong `AskTutorRequest` và `SocraticAskRequestPayload`, chuyển tiếp thắc mắc riêng biệt của học sinh sang Python RAG Service.
- **Nâng Cấp Fallback Socratic Phổ Quát ([`AiTutorClient.cs`](./V-Eval-Practice_Service.Infrastructure/HttpClients/AiTutorClient.cs))**:
  - Xóa bỏ câu chữ cố định về đạo hàm, chuyển sang cơ chế gợi mở đối chiếu giả thiết tổng quát cho toàn bộ các môn thi ĐGNL.
- **Kiểm Thử & Xác Thực**:
  - `dotnet build` (0 warning, 0 error) & `dotnet test` (4/4 pass) thành công 100%.
