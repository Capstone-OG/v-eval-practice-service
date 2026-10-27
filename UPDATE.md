# NHẬT KÝ CẬP NHẬT (UPDATE LOG) - PRACTICE SERVICE

## [27/10/2026] - Chuẩn Hóa Bộ Unit Test Thích Ứng (Adaptive) Và Tích Hợp CI Test

- **Chuẩn Hóa Bộ Unit Test Adaptive (4 Tests)**:
  - Thiết lập dự án kiểm thử `V-Eval-Practice_Service.Tests` theo chuẩn xUnit và .NET 9.
  - Tối ưu và rút gọn bộ unit test còn đúng 4 test cases trọng tâm theo kiến trúc:
    - [`BktEngineTests.cs`](./V-Eval-Practice_Service.Tests/Adaptive/BktEngineTests.cs) (2 tests): Kiểm thử công thức cập nhật xác suất thành thạo kiến thức Bayesian Knowledge Tracing khi học sinh trả lời Đúng (`pMastery` tăng) và Sai (`pMastery` giảm).
    - [`SpacedRepetitionCalculatorTests.cs`](./V-Eval-Practice_Service.Tests/Adaptive/SpacedRepetitionCalculatorTests.cs) (1 test): Kiểm thử thuật toán lặp lại ngắt quãng SM-2 tính toán khoảng cách ngày ôn tập theo điểm đánh giá chất lượng.
    - [`ZpdQuestionSelectorTests.cs`](./V-Eval-Practice_Service.Tests/Adaptive/ZpdQuestionSelectorTests.cs) (1 test): Kiểm thử bộ chọn câu hỏi vùng phát triển gần ZPD, chọn câu hỏi có độ khó phù hợp với năng lực học sinh.
- **Tích Hợp Tự Động Hóa CI/CD Workflow**:
  - Bổ sung bước thực thi `dotnet test --no-build --configuration Release --verbosity normal` vào `.github/workflows/ci.yml`.
- **Bổ Sung Trạng Thái Vòng Đời Sổ Tay Lỗi Sai (Mistake Notebook Lifecycle)**:
  - Cập nhật thực thể [`MistakeNotebook.cs`](./V-Eval-Practice_Service.Domain/Entities/MistakeNotebook.cs): Bổ sung thuộc tính `Status` và lớp hằng số `MistakeLifecycleState` (`NEW_MISTAKE`, `DUE_FOR_REVIEW`, `REVIEWING`, `RETAINED`, `RELAPSED`, `MASTERED`) đồng bộ với máy trạng thái và tài liệu thiết kế.
  - Đồng bộ thực thể [`LearningProfile.cs`](./V-Eval-Practice_Service.Domain/Entities/LearningProfile.cs) và bảng `v_eval_practice."LearningProfiles"` vào sơ đồ thực thể ERD tổng thể.

