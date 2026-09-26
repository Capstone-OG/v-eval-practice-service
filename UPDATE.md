# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [27/09/2026] - Chuẩn Hóa Thang Đo Tư Duy Bloom 6 Cấp Độ & Báo Cáo Phân Tích Độ Khó

- **Chuẩn Hóa Thang Đo Tư Duy Bloom 6 Mức Độ (Revised Bloom's Taxonomy)**:
  - Khởi tạo hằng số [`BloomTaxonomy.cs`](./V-Eval-Practice_Service.Domain/Constants/BloomTaxonomy.cs) định nghĩa 6 mức độ tư duy:
    1. `Remembering = 1`: Nhận biết
    2. `Understanding = 2`: Thông hiểu
    3. `Applying = 3`: Vận dụng
    4. `Analyzing = 4`: Phân tích
    5. `Evaluating = 5`: Đánh giá
    6. `Creating = 6`: Sáng tạo
- **Nâng Cấp CQRS Handlers Phân Tích Chẩn Đoán**:
  - Cập nhật [`SubmitDiagnosticCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Commands/SubmitDiagnostic/SubmitDiagnosticCommandHandler.cs) và [`GetDiagnosticSubmissionById.cs`](./V-Eval-Practice_Service.Application/Features/DiagnosticSubmissions/Queries/GetDiagnosticSubmissionById/GetDiagnosticSubmissionById.cs).
  - Phân tích chi tiết `DifficultyBreakdown` theo 6 cấp độ Bloom thay vì 4 cấp độ đơn giản trước đây.
- **Cập Nhật Nhật Ký Vận Hành**:
  - Đồng bộ chi tiết phân tích Bloom vào [`docs/daily.md`](./docs/daily.md).