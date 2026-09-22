# KIẾN TRÚC & BẢNG THEO DÕI TIẾN ĐỘ CHỦ THỂ (PROCESS & PLANNING) - V-EVAL PRACTICE SERVICE

---

## PHẦN 1: KIẾN TRÚC DỊCH VỤ & CÁC THÀNH PHẦN CẦN TRIỂN KHAI

### 1. Kiến Trúc Clean Architecture & Quản Lý Thi Trực Tuyến
- **Cổng Dịch Vụ**: `5261` (Local Launch HTTP) / `5002` (Docker Container `v_eval_practice_service`).
- **Nhiệm Vụ Chính**:
  - Tiếp nhận bài thi khảo sát chẩn đoán năng lực ban đầu (30 câu hỏi) của học sinh (Core Flow 1 - Bước 3).
  - Tích hợp gRPC liên dịch vụ:
    - Gọi **Identity Service** (port 5156) xác thực trạng thái học sinh và cơ sở đào tạo (`CampusId`).
    - Gọi **Content Service** (port 5250) lấy bảng đáp án bảo mật, độ khó và mã kỹ năng (`SkillId`).
  - Tự động chấm điểm khách quan (thang 30 câu), ghi nhận thời gian phản hồi vi mô (`time_spent_seconds`) từng câu.
  - Phân tích chẩn đoán năng lực: thống kê tỷ lệ đúng theo kỹ năng, nhận diện kỹ năng yếu (`WeakSkillIds` có độ chính xác < 60%), và phân tích theo 4 cấp độ độ khó câu hỏi (Dễ, Trung bình, Khó, Rất khó).
  - Đồng bộ kết quả vào CSDL Supabase PostgreSQL Schema `practice`.
  - Cung cấp dữ liệu vi mô làm đầu vào cho AI Subsystem (Bước 4 & 5) ước lượng vector năng lực $\theta_0$, khởi tạo BKT $P(L_0)$, vẽ Radar đa giác và phân cụm xếp lớp.

### 2. Sơ Đồ CSDL PostgreSQL Schema `practice`
- `practice.exam_submissions`: Phiên nộp bài thi (`submission_id`, `student_id`, `exam_id`, `exam_type`, `total_score`, `total_correct`, `total_questions`, `total_time_spent_seconds`, `started_at`, `completed_at`, `status`).
- `practice.submission_answers`: Chi tiết 30 câu trả lời (`answer_id`, `submission_id`, `question_id`, `selected_option`, `is_correct`, `time_spent_seconds`).

---

## PHẦN 2: BẢNG THEO DÕI TIẾN ĐỘ CHI TIẾT THEO TỪNG MỤC (PROGRESS MATRIX)

| STT | Hạng Mục / Chức Năng | Vị Trí Triển Khai trong Code | Trạng Thái | Tiến Độ (%) | Ghi Chú Chi Tiết |
| :---: | :--- | :--- | :---: | :---: | :--- |
| 1 | **Clean Architecture 4 Tầng** | Entire Solution | 🟢 Hoàn thành | 100% | `Domain`, `Application`, `Infrastructure`, `API` |
| 2 | **Cấu Hình Production & Security**| `appsettings.json` / `launchSettings.json` | 🟢 Hoàn thành | 100% | Supabase connection string & gRPC endpoints (5156, 5250) |
| 3 | **Định Tuyến Gateway YARP** | Gateway YARP Config | 🟢 Hoàn thành | 100% | Route `/api/practice/{**catch-all}` cổng 5002 |
| 4 | **Dockerfile & Compose** | `Dockerfile` | 🟢 Hoàn thành | 100% | Multi-Stage .NET 9 cổng 5002 trên `veval_network` |
| 5 | **Script Push Độc Lập** | `Scripts/push.bat` | 🟢 Hoàn thành | 100% | Hỗ trợ 3 chế độ push kèm kiểm tra lịch sử |
| 6 | **Result Pattern & Error Handling**| `Application/Common/Models/` | 🟢 Hoàn thành | 100% | `Result<T>`, `Error`, `ErrorType` enum đồng bộ |
| 7 | **Validation Pipeline MediatR**| `Application/Common/Behaviors/` | 🟢 Hoàn thành | 100% | `ValidationBehavior` tích hợp FluentValidation |
| 8 | **gRPC Clients Liên Dịch Vụ** | `Infrastructure/GrpcClients/` | 🟢 Hoàn thành | 100% | Client gọi Identity Service (5156) và Content Service (5250) |
| 9 | **CSDL Schema `practice`** | `Infrastructure/Persistence/` | 🟢 Hoàn thành | 100% | Tạo tự động bảng `exam_submissions` và `submission_answers` |
| 10 | **Core Flow 1: Nộp Bài & Chấm Điểm**| `DiagnosticSubmissionsController` | 🟢 Hoàn thành | 100% | `POST /api/v1/practice/diagnostic-submissions` chấm điểm thang 30, ghi nhận `time_spent` |
| 11 | **Chẩn Đoán Năng Lực & Kỹ Năng Yếu**| `SubmitDiagnosticCommandHandler` | 🟢 Hoàn thành | 100% | Tách `SkillBreakdown`, `WeakSkillIds` (<60%), `DifficultyBreakdown` |
| 12 | **API Tra Cứu Bài Nộp Theo ID** | `DiagnosticSubmissionsController` | 🟢 Hoàn thành | 100% | `GET /api/v1/practice/diagnostic-submissions/{id}` |
| 13 | **API Lịch Sử Bài Làm Học Sinh**| `DiagnosticSubmissionsController` | 🟢 Hoàn thành | 100% | `GET /api/v1/practice/diagnostic-submissions/student/{studentId}` |
| 14 | **Swagger UI & ProblemDetails** | `Program.cs` / `ApiControllerBase` | 🟢 Hoàn thành | 100% | Swagger UI tại `http://localhost:5261/swagger`, RFC 7807 |
| 15 | **Core Flow 1 (Bước 4): Tích Hợp AI Subsystem (IRT & BKT)** | `HttpClients/AiDiagnosticClient.cs` & `SubmitDiagnosticCommandHandler.cs` | 🟢 Hoàn thành | 100% | Gửi 30 câu sang `AI Engine` -> Nhận $\theta_0$, $P(L_0)$ Sigmoid, Radar Chart, nhận xét Gemini |
| 16 | **Core Flow 1 (Bước 5): Tự Động Gợi Ý Phân Lớp Tại Campus** | `Repositories/ClassEnrollmentRepository.cs` | 🟢 Hoàn thành | 100% | Phân lớp $\theta_0$ (FOUNDATION / ACCELERATION / BREAKTHROUGH) -> Tự động ghi danh `ClassEnrollments` |
