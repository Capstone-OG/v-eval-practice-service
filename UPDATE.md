# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [18/10/2026] - Hiện Thực Core Flow 6: API 1 (Tra Cứu Dải Điểm SEM & Toa Thuốc) & API 2 (Lấy Đề Ôn Tập Biến Thể Cho Dạng Bài Bù Điểm)

- **Nền Tảng Mô Hình Thực Thể & CSDL Core Flow 6**:
  - Tạo entity [`ScorePrediction.cs`](./V-Eval-Practice_Service.Domain/Entities/ScorePrediction.cs) và [`SystemAlert.cs`](./V-Eval-Practice_Service.Domain/Entities/SystemAlert.cs).
  - Cập nhật [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs) và bộ Repository [`ScorePredictionRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/ScorePredictionRepository.cs), [`SystemAlertRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/SystemAlertRepository.cs).
- **Hiện Thực API 1: Tra Cứu Dải Điểm Dự Báo & Toa Thuốc Bù Điểm (GetMyForecast)**:
  - Khởi tạo chuỗi CQRS: [`PrescriptionItemDto.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/DTOs/PrescriptionItemDto.cs), [`MyForecastResponseDto.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/DTOs/MyForecastResponseDto.cs), `GetMyForecastQuery.cs` và [`GetMyForecastQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/Queries/GetMyForecast/GetMyForecastQueryHandler.cs).
  - Endpoint `GET /api/practice/predictions/my-forecast`.
- **Hiện Thực API 2: Lấy Đề Ôn Tập Biến Thể Cho Dạng Bài Bù Điểm (GetPrescriptionPracticeSession)**:
  - Khởi tạo chuỗi CQRS: [`PrescriptionSessionResponseDto.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/DTOs/PrescriptionSessionResponseDto.cs), `GetPrescriptionSessionQuery.cs` và [`GetPrescriptionSessionQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/Queries/GetPrescriptionSession/GetPrescriptionSessionQueryHandler.cs).
  - Endpoint: `GET /api/practice/predictions/prescriptions/{patternId}/practice-session`.
  - Nghiệp vụ: Xác thực `patternId` thuộc Toa thuốc bù điểm của học sinh, kết nối `IContentGrpcClient` bốc ngẫu nhiên 3–5 câu hỏi biến thể từ Ngân hàng câu hỏi của Content Service (ẩn đáp án đúng), tích hợp cơ chế fallback sinh câu hỏi biến thể linh hoạt.
- **Kiểm Thử & Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Đã kiểm thử live thành công cả qua port trực tiếp `5261` và cổng API Gateway YARP `5212`.
