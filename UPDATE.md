# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [22/09/2026] - Hoàn Tất Trọn Vẹn Core Flow 1 (Bước 4 & 5): Tích Hợp AI Diagnostic, BKT Priors & Tự Động Xếp Lớp
- **Tích Hợp AI Subsystem Client (`HttpClients/AiDiagnosticClient.cs`)**:
  - Triển khai `IAiDiagnosticClient` & `AiDiagnosticClient` kết nối API `POST /api/v1/diagnostic/analyze` của AI Engine.
  - Gửi vector kết quả bài làm 30 câu hỏi (đúng/sai, thời gian `time_spent_seconds`, độ khó, danh mục kỹ năng).
  - Tích hợp cơ chế **Resilient Local Fallback**: Tự động kích hoạt bộ tính toán ước lượng nội bộ nếu AI Engine tạm ngắt kết nối, đảm bảo bài thi không bao giờ bị nghẽn (Zero-Blocking SLA).
- **Mở Rộng Domain Entities & CSDL Schema Supabase**:
  - `ExamSubmission`: Bổ sung `Theta0` (IRT ability), `PlacementClass` (FOUNDATION / ACCELERATION / BREAKTHROUGH), `AiCommentary` và `EnrolledClassId`.
  - `LearningProfile`: Lưu trữ giá trị xác suất làm chủ ban đầu BKT Prior $P(L_0) \in [0.05, 0.95]$ cho từng kỹ năng vào bảng `LearningProfiles`.
  - `Class` & `ClassEnrollment`: Quản lý việc tự động phân bổ học sinh vào lớp học tại cơ sở (`CampusId`) gắn liền với bài nộp chẩn đoán (`diagnostic_submission_id`).
- **Mở Rộng EF Core Persistence (`PracticeDbContext`)**:
  - Đăng ký `DbSet<LearningProfile>`, `DbSet<Class>`, `DbSet<ClassEnrollment>`.
  - Cấu hình Fluent API tương thích chuẩn xác với schema CSDL Supabase PostgreSQL.
- **Triển Khai Repositories Nghiệp Vụ**:
  - `ILearningProfileRepository` / `LearningProfileRepository`: Upsert thông minh danh sách $P(L_0)$ của học sinh.
  - `IClassEnrollmentRepository` / `ClassEnrollmentRepository`: Tìm kiếm hoặc tự động khởi tạo lớp học tại Campus của học sinh theo phân lớp và tạo bản ghi ghi danh (`ENROLLED`).
- **Nâng Cấp Use Case `SubmitDiagnosticCommandHandler`**:
  - Khép kín toàn bộ 5 bước của Core Flow 1: Xác thực Identity (1) -> Lấy bảng đáp án Content (2) -> Chấm điểm (3) -> Ước lượng IRT & BKT qua AI (4) -> Tự động xếp lớp tại Campus và trả về Biểu đồ Radar đa giác trong $< 2$ giây (5).
- **Di Trú CSDL & Khắc Phục Schema PostgreSQL (`Program.cs`)**:
  - Bổ sung migration tự động trên startup: `ALTER TABLE practice.exam_submissions ADD COLUMN IF NOT EXISTS ...` (`theta_0`, `placement_class`, `ai_commentary`, `enrolled_class_id`).
  - Khởi tạo bảng `LearningProfiles`, `Classes`, `ClassEnrollments` trên CSDL Supabase.
- **Kiểm Thử Vận Hành & End-to-End Trực Tiếp Qua Swagger**:
  - Biên dịch toàn bộ giải pháp `V-Eval-Practice_Service.sln`: **0 Warning(s), 0 Error(s)**.
  - Kiểm thử trực tiếp `POST /api/v1/practice/diagnostic-submissions` trên Swagger UI với 30 câu hỏi thật: Nhận kết quả thành công HTTP 200 OK với đầy đủ `theta_0 = -1.5`, xếp lớp `FOUNDATION`, tự động tạo lớp học tại cơ sở `Lớp Nền tảng (Foundation) - Cơ sở ...`, ghi danh học sinh, lưu trữ 12 BKT Priors và nhận xét sư phạm Socratic.

## [20/09/2026] - Triển Khai Hoàn Thiện Clean Architecture 4 Tầng & Core Flow 1 (Bước 3: Chấm Điểm Chẩn Đoán 30 Câu & Tích Hợp gRPC)
- **Triển Khai Chuẩn Kiến Trúc Clean Architecture 4 Tầng**:
  - `Domain Layer`: Xây dựng thực thể `ExamSubmission` (thang điểm 0–30, tổng câu 30, thời gian), `SubmissionAnswer` (ghi nhận chi tiết từng câu: `selected_option`, `is_correct`, `time_spent_seconds`) và `IExamSubmissionRepository`.
  - `Application Layer`: Đồng bộ 100% **Result Pattern** (`Result<T>`, `Error`, `ErrorType`), tích hợp **FluentValidation Pipeline** qua `ValidationBehavior`, CQRS MediatR trọn bộ `SubmitDiagnosticCommand`, `GetDiagnosticSubmissionByIdQuery`, `GetDiagnosticSubmissionsByStudentQuery`.
  - `Infrastructure Layer`: Cấu hình EF Core Npgsql trên CSDL Supabase PostgreSQL schema `practice` (`exam_submissions`, `submission_answers`), triển khai gRPC Clients kết nối Identity Service (port 5156) và Content Service (port 5250).
  - `API Layer`: Xây dựng `ApiControllerBase` chuẩn hóa RFC 7807 ProblemDetails, `DiagnosticSubmissionsController` (`[Route("api/v1/practice/diagnostic-submissions")]`), `GlobalExceptionHandlerMiddleware`, Swagger UI tại `http://localhost:5261/swagger`.
- **Hoàn Tất Core Flow 1 (Bước 3: Đánh Giá Năng Lực Đầu Vào & Chấm Điểm Tự Động)**:
  - Tiếp nhận bài nộp 30 câu hỏi khảo sát chẩn đoán năng lực ban đầu qua `POST /api/v1/practice/diagnostic-submissions`.
  - Tự động gọi gRPC xác thực điều kiện học sinh và cơ sở đào tạo (`CampusId`) từ Identity Service (port 5156).
  - Tự động gọi gRPC lấy bảng đáp án bảo mật, độ khó câu hỏi và mã kỹ năng từ Content Service (port 5250).
  - Tự động chấm điểm thô (thang 30), ghi nhận chi tiết thời gian phản hồi (`time_spent_seconds`) từng câu.
  - Chẩn đoán phân tích năng lực: Thống kê tỷ lệ đúng theo Kỹ năng (`SkillBreakdown`), tự động nhận diện kỹ năng yếu (`WeakSkillIds` có tỷ lệ đúng < 60%), và thống kê theo 4 mức độ khó (Dễ, Trung bình, Khó, Rất khó).
  - Lưu trữ toàn bộ kết quả vào Supabase schema `practice`.
  - Cung cấp dữ liệu sẵn sàng cho AI Subsystem (Bước 4 & 5) ước lượng $\theta_0$, $P(L_0)$, vẽ biểu đồ Radar và xếp lớp.
- **Tối Ưu Hóa DTO API (DTO Separation Pattern)**:
  - Tách bạch `DiagnosticSubmissionSummaryDto` cho API lịch sử học sinh (`GET /student/{studentId}`): Chỉ trả về các chỉ số cốt lõi (Điểm số, câu đúng, % chính xác, thời gian), không nhồi mảng 30 câu hỏi giúp giảm tải payload mạng.
  - Giữ nguyên `GET /{id}` để tra cứu toàn bộ chi tiết 30 câu hỏi, đúng/sai, đáp án, độ khó và phân tích kỹ năng khi cần xem lại bài làm.
- **Kiểm Thử Toàn Diện (End-to-End Test)**:
  - Biên dịch giải pháp `V-Eval-Practice_Service.sln`: **0 Error(s), 0 Warning(s)**.
  - Chạy kịch bản tích hợp liên dịch vụ 3 microservices (Identity 5155/5156, Content 5249/5250, Practice 5261): Thành công 100%!

## [18/09/2026] - Phát Hành Công Cụ Push Độc Lập `Scripts/push.bat` Cho Practice Service
- **Tích Hợp `Scripts/push.bat` Độc Lập**:
  - Khởi tạo script [`Scripts/push.bat`](file:///e:/CapStone/All%20Services/V-Eval-Practice_Service/Scripts/push.bat) độc lập cho Practice Service.
  - Hỗ trợ Push nhanh trên nhánh hiện tại, chọn nhánh đã có qua Menu đánh số, hoặc tạo nhánh mới tự động.
  - Tích hợp tự động kiểm tra đồng bộ lịch sử Git với Remote, tự động pull code khi bi cham (behind) và đưa ra **Cảnh báo Đỏ (Red Warning)** ngắt quy trình khi bị xung đột lịch sử (Conflict/Diverged).

## [15/09/2026] - Dockerize Practice Service & Chuẩn Hóa Docker Compose
- **Dockerfile Multi-Stage .NET 9**:
  - Khởi tạo `Dockerfile` chuẩn cho Practice Service (`V-Eval-Practice_Service.API`) với cổng `5002`.
  - Kết nối chung mạng nội bộ `veval_network` trong `docker-compose.yml`.

## [14/09/2026] - Chuẩn Hóa Cấu Hình Production & Git Security
- **Khởi Tạo `appsettings.example.json`**:
  - Tạo file cấu hình mẫu chứa `ConnectionStrings` và `JwtSettings`.
- **Bảo Mật Git Security**:
  - Cập nhật `.gitignore` ẩn tất cả file `appsettings.json` chứa mật khẩu cá nhân.
