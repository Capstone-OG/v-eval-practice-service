# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [19/10/2026] - Hiện Thực Core Flow 6: API 5 Tra Cứu Cảnh Báo Sớm Cá Nhân (GetMyAlerts)

- **Hiện Thực API 5: Học Sinh Tra Cứu Danh Sách Cảnh Báo Cá Nhân (GetMyAlerts)**:
  - Khởi tạo bộ DTOs [`EarlyWarningDtos.cs`](./V-Eval-Practice_Service.Application/Features/EarlyWarnings/DTOs/EarlyWarningDtos.cs):
    - `SystemAlertDto`: Chi tiết từng thông báo cảnh báo 3 cấp (`AlertId`, `StudentId`, `AlertType`, `Message`, `IsRead`, `HandledBy`, `ResolutionNotes`, `ResolvedAt`, `CreatedAt`).
    - `MyAlertsResponseDto`: Phản hồi thống kê toàn diện (`TotalAlerts`, `UnreadCount`, `RedAlertsCount`, `YellowAlertsCount`, `Alerts`).
  - Xây dựng Query CQRS `GetMyAlertsQuery.cs` và Handler [`GetMyAlertsQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/EarlyWarnings/Queries/GetMyAlerts/GetMyAlertsQueryHandler.cs):
    - Truy xuất danh sách cảnh báo cá nhân từ `ISystemAlertRepository`.
    - Hỗ trợ đa dạng bộ lọc: lọc cảnh báo chưa đọc (`unreadOnly = true/false`), lọc theo cấp độ cảnh báo (`alertType = 'YELLOW' | 'RED' | 'UNREALISTIC_GOAL' | 'GREEN'`).
    - Tính toán chính xác các chỉ số tổng hợp độc lập với bộ lọc để giao diện học sinh luôn hiển thị badge cảnh báo chuẩn xác.
  - Mở rộng Repository [`ISystemAlertRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ISystemAlertRepository.cs) và [`SystemAlertRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/SystemAlertRepository.cs) hỗ trợ lọc linh hoạt theo `alertType`.
  - Khởi tạo Controller [`EarlyWarningsController.cs`](./V-Eval-Practice_Service.API/Controllers/EarlyWarningsController.cs):
    - Định tuyến chuẩn: `GET /api/practice/early-warnings/my-alerts`.
    - Cơ chế xác thực an toàn: ưu tiên bốc `StudentId` từ Gateway Header `X-User-Id`, fallback query parameter `studentId` hỗ trợ kiểm thử tiện lợi trên Swagger.
  - Bổ sung Seed Data mẫu cho hệ thống cảnh báo sớm trong [`Program.cs`](./V-Eval-Practice_Service.API/Program.cs) với đầy đủ 3 cấp độ `RED`, `YELLOW`, `UNREALISTIC_GOAL` kèm nhật ký can thiệp của Mentor.
- **Kiểm Thử & Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
