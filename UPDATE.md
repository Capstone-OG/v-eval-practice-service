# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [18/10/2026] - Hiện Thực Core Flow 6: Nền Tảng Entity/Repository & API 1 Tra Cứu Dải Điểm SEM & Toa Thuốc Bù Điểm

- **Nền Tảng Mô Hình Thực Thể & CSDL Core Flow 6**:
  - Tạo entity [`ScorePrediction.cs`](./V-Eval-Practice_Service.Domain/Entities/ScorePrediction.cs) lưu trữ dải điểm tin cậy `[PredictedScoreMin, PredictedScoreMax]`, sai số chuẩn `SemScore`, điểm thâm hụt `DeficitScore` và cột `PrescriptionItems` (JSONB) chứa danh sách các dạng bài bù điểm Knapsack ROI.
  - Tạo entity [`SystemAlert.cs`](./V-Eval-Practice_Service.Domain/Entities/SystemAlert.cs) phục vụ hệ thống cảnh báo sớm 3 cấp BR-04 và nhật ký can thiệp sư phạm của Mentor tại lớp.
  - Cập nhật [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs) đăng ký 2 `DbSet` và cấu hình Fluent API mapping tương thích PostgreSQL Schema.
  - Tạo bộ Repository [`IScorePredictionRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IScorePredictionRepository.cs), [`ScorePredictionRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/ScorePredictionRepository.cs), [`ISystemAlertRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ISystemAlertRepository.cs), [`SystemAlertRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/SystemAlertRepository.cs) và đăng ký vào DI Container trong [`DependencyInjection.cs`](./V-Eval-Practice_Service.Infrastructure/DependencyInjection.cs).
- **Hiện Thực API 1: Tra Cứu Dải Điểm Dự Báo & Toa Thuốc Bù Điểm (GetMyForecast)**:
  - Khởi tạo chuỗi CQRS: [`PrescriptionItemDto.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/DTOs/PrescriptionItemDto.cs), [`MyForecastResponseDto.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/DTOs/MyForecastResponseDto.cs), `GetMyForecastQuery.cs` và [`GetMyForecastQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/Queries/GetMyForecast/GetMyForecastQueryHandler.cs).
  - Khởi tạo controller [`PredictionsController.cs`](./V-Eval-Practice_Service.API/Controllers/PredictionsController.cs) với endpoint `GET /api/practice/predictions/my-forecast`.
  - Hỗ trợ linh hoạt định danh `X-User-Id` (Gateway/JWT) hoặc query param `studentId`, tự động phân loại trạng thái cân đối điểm (`ON_TRACK`, `AT_RISK`, `CRITICAL`).
- **Kiểm Thử & Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
