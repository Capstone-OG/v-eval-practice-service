# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [08/10/2026] - Triển Khai Hoàn Tất Core Flow 3: API 4 - Động Cơ Bốc Câu Hỏi Thích Ứng IRT 2PL Trong Vùng ZPD (NextQuestion)

- **Xây Dựng Động Cơ Chọn Câu Hỏi Thích Ứng IRT 2PL Trong Vùng ZPD ([`ZpdQuestionSelector.cs`](./V-Eval-Practice_Service.Application/Common/Adaptive/ZpdQuestionSelector.cs))**:
  - Triển khai thuật toán quy đổi xác suất thành thạo BKT $P(L_t) \in [0.05, 0.95]$ sang tham số năng lực $\theta = \ln(\frac{P(L_t)}{1 - P(L_t)}) \in [-2.5, +2.5]$.
  - Tính toán xác suất trả lời đúng của từng câu hỏi ứng viên theo mô hình IRT 2PL: $P(X=1 \mid \theta, a, b) = 1 / (1 + \exp(-1.7 \cdot a \cdot (\theta - b)))$.
  - Triển khai bộ lọc 3 tầng sư phạm chuẩn V-Eval:
    1. Vùng ZPD tối ưu: $P \in [0.60, 0.75]$, ưu tiên câu tiệm cận tâm 0.675.
    2. Vùng ZPD nới lỏng (Fallback khi ngân hàng thưa): $P \in [0.50, 0.85]$.
    3. Nearest Neighbor Fallback: chọn câu có khoảng cách $|P - 0.675|$ nhỏ nhất.
  - Tự động loại trừ các câu hỏi học sinh đã làm trong phiên học (`AdaptiveAttempts`).
  - Đăng ký `IZpdQuestionSelector` vào DI container tại [`DependencyInjection.cs`](./V-Eval-Practice_Service.Application/DependencyInjection.cs).
- **Hiện Thực API 4: Lấy Câu Hỏi Thích Ứng Bước APPLY (`GET /api/practice/stages/{stageProgressId}/next-question`)**:
  - Tạo bộ DTOs [`NextQuestionDtos.cs`](./V-Eval-Practice_Service.Application/Features/Stages/DTOs/NextQuestionDtos.cs) ẩn toàn bộ đáp án đúng để bảo mật.
  - Tạo Query `GetNextQuestionQuery.cs` và Handler [`GetNextQuestionQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Stages/Queries/GetNextQuestion/GetNextQuestionQueryHandler.cs):
    1. Kiểm tra trạng thái máy: Yêu cầu chặng phải ở bước `APPLY`.
    2. Kiểm tra điều kiện hoàn thành chặng hoặc phong tỏa do quy tắc phụ đạo BR-03 (`REMEDIAL_REQUIRED`).
    3. Tải câu hỏi từ Content Service gRPC hoặc pool dự phòng thích ứng đa cấp độ.
    4. Trích xuất câu hỏi tối ưu và trả về client.
  - Bổ sung Action endpoint vào [`StagesController.cs`](./V-Eval-Practice_Service.API/Controllers/StagesController.cs): `[HttpGet("{stageProgressId:guid}/next-question")]`.
- **Đồng Bộ Kiến Trúc & Kiểm Thử Vận Hành**:
  - Cập nhật tài liệu nghiệm thu kiến trúc [`docs/architecture_acceptance.md`](./docs/architecture_acceptance.md) (Mục 7.5).
  - Cập nhật nhật ký tiến độ [`docs/daily.md`](./docs/daily.md) và bảng theo dõi [`docs/process.md`](./docs/process.md) (STT 50).
  - Kiểm thử trực tiếp qua PowerShell: Endpoint trả về mã `200 OK`, bốc câu hỏi khớp vùng ZPD tương ứng $P(L_t) = 0.10$ thành công 100%.
  - Toàn bộ Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
