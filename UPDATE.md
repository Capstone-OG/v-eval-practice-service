# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [19/10/2026] - Triển Khai Giai Đoạn 3 (Flow 1): REST API Student Dashboard & CQRS Query Handler

- **Giai Đoạn 3 (Flow 1: Student Learning Analytics Dashboard)**:
  - **DTOs (`StudentDashboardDtos.cs`)**: Tạo các DTO phục vụ biểu đồ Radar 4 miền năng lực, tiến độ lộ trình DAG %, chuỗi ngày chuyên cần Streak Analytics và lịch sử thi thử gần nhất.
  - **CQRS Query & Handler**:
    - `GetStudentDashboardQuery`: Nhận `StudentId` và trả về `Result<StudentDashboardResponseDto>`.
    - `GetStudentDashboardQueryHandler`:
      - Gọi `IIdentityGrpcClient.GetStudentProfileAsync` lấy target score và thông tin học sinh với cơ chế fallback kiên cường.
      - Phân tích dữ liệu làm bài qua `IExamSubmissionRepository`, `IStudentActivityRepository` để tự động kích hoạt cờ Onboarding UI (`needsOnboarding = true`).
      - Tính toán Biểu đồ Radar 4 miền năng lực ĐGNL dựa trên dữ liệu ma trận nhiệt thực tế `IClassHeatmapRepository.GetHeatmapsByStudentIdAsync`.
      - Tổng hợp tiến độ lộ trình học tập thích ứng (DAG Milestones %) từ `ILearningRoadmapRepository.GetActiveByStudentIdAsync`.
      - Thống kê chuỗi ngày chuyên cần (Streak Analytics) gồm số ngày hiện tại, kỷ lục chuỗi dài nhất, cờ bảo vệ chuỗi (Freeze) và mảng hoạt động 7 ngày gần nhất.
  - **REST API Endpoint**:
    - Tạo `AnalyticsController.cs` kế thừa `ApiControllerBase` với endpoint `GET /api/practice/analytics/student/{studentId:guid}/dashboard`.
  - **Kiểm Thử & Biên Dịch**:
    - Chạy `dotnet build` toàn bộ solution `V-Eval-Practice_Service.sln` thành công 100% (**0 Error, 0 Warning**).
