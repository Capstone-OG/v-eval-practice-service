# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [18/10/2026] - Hiện Thực Core Flow 6: Bộ 4 APIs Dự Báo Điểm SEM, Lấy Đề Biến Thể, Nộp Bài Xóa Nợ Điểm & Tái Tính Toán Toa Thuốc

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
- **Hiện Thực API 3: Nộp Bài Ôn Tập Vi Mô, Chấm Điểm & Xóa Nợ Điểm Thâm Hụt (SubmitPrescriptionSession)**:
  - Khởi tạo DTOs [`SubmitPrescriptionSessionDtos.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/DTOs/SubmitPrescriptionSessionDtos.cs), Command `SubmitPrescriptionSessionCommand.cs` và Handler [`SubmitPrescriptionSessionCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/Commands/SubmitPrescriptionSession/SubmitPrescriptionSessionCommandHandler.cs).
  - Endpoint: `POST /api/practice/predictions/prescriptions/{patternId}/submit-session`.
  - Nghiệp vụ: Chấm điểm tự động động 100% bằng cách gọi gRPC `GetQuestionDetailAsync` sang Content Service; nếu đạt độ chính xác $\ge 67\%$ thì đánh dấu dạng bài đã khắc phục (`is_remedied = true`), nâng xác suất thành thạo BKT $P(L_t)$ và trừ trực tiếp điểm thâm hụt (`deficit_score`) trong bảng `ScorePredictions`.
- **Hiện Thực API 4: Kích Hoạt Tái Tính Toán Dải Điểm SEM & Toa Thuốc Bù Điểm (RecalculatePrediction)**:
  - Khởi tạo DTOs [`RecalculatePredictionDtos.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/DTOs/RecalculatePredictionDtos.cs), Command `RecalculatePredictionCommand.cs` và Handler [`RecalculatePredictionCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Predictions/Commands/RecalculatePrediction/RecalculatePredictionCommandHandler.cs).
  - Endpoint: `POST /api/practice/predictions/recalculate`.
  - Nghiệp vụ: Tái ước lượng dải điểm tin cậy $[Min, Max]$ qua sai số chuẩn đo lường SEM $95\%$, cập nhật mức độ thâm hụt so với mục tiêu, tự động giải bài toán Knapsack chọn 2–3 dạng bài bù điểm có ROI cao nhất và lưu bản ghi dự báo mới vào CSDL.
- **Kiểm Thử & Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Đã kiểm thử live đầy đủ kịch bản thành công (200 OK) và kịch bản thất bại (401 Unauthorized).
