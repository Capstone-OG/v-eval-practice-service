# NHẬT KÝ KIỂM TRA TIẾN ĐỘ VẬN HÀNH (DAILY CHECK LOG) - PRACTICE SERVICE

## [22/09/2026] - Hoàn Tất Core Flow 1 (Bước 4 & 5): Tích Hợp AI Diagnostic, Lưu Trữ BKT Priors & Tự Động Xếp Lớp Tại Campus
- **Triển Khai HTTP Client Kết Nối AI Subsystem (`IAiDiagnosticClient` & `AiDiagnosticClient`)**:
  - Xây dựng HTTP Client kết nối endpoint `POST /api/v1/diagnostic/analyze` của AI Engine (`http://localhost:8000`).
  - Gửi gói dữ liệu 30 câu hỏi kèm thời gian phản hồi (`time_spent_seconds`), độ khó và danh mục miền năng lực.
  - Tích hợp cơ chế **Resilient Local Fallback**: Nếu AI Engine tạm thời gián đoạn hoặc offline, hệ thống tự động kích hoạt bộ tính toán dự phòng cục bộ (ước lượng $\theta_0$, BKT Sigmoid, phân lớp và nhận xét chuẩn mực), đảm bảo bài nộp của học sinh không bao giờ bị nghẽn (Zero-Blocking SLA).
- **Mở Rộng Domain Entities & CSDL Supabase**:
  - `ExamSubmission`: Bổ sung các trường lưu trữ kết quả chẩn đoán: `Theta0` (IRT ability), `PlacementClass` (FOUNDATION / ACCELERATION / BREAKTHROUGH), `AiCommentary` (nhận xét sư phạm Socratic) và `EnrolledClassId` (khóa ngoại lớp học được xếp).
  - `LearningProfile`: Ánh xạ bảng `LearningProfiles` lưu trữ xác suất làm chủ ban đầu $P(L_0) \in [0.05, 0.95]$ cho từng kỹ năng của học sinh làm giá trị tiên nghiệm cho mô hình BKT.
  - `Class` & `ClassEnrollment`: Ánh xạ bảng `Classes` và `ClassEnrollments` quản lý việc phân bổ học sinh vào lớp học tại cơ sở (`CampusId`) gắn liền với bài nộp chẩn đoán (`diagnostic_submission_id`).
- **Mở Rộng EF Core Persistence (`PracticeDbContext`)**:
  - Đăng ký `DbSet<LearningProfile>`, `DbSet<Class>`, `DbSet<ClassEnrollment>`.
  - Cấu hình Fluent API ánh xạ tương thích chuẩn xác với schema CSDL PostgreSQL Supabase.
- **Triển Khai Các Repositories Nghiệp Vụ**:
  - `ILearningProfileRepository` / `LearningProfileRepository`: Thực hiện upsert thông minh danh sách $P(L_0)$ của học sinh vào bảng `LearningProfiles`.
  - `IClassEnrollmentRepository` / `ClassEnrollmentRepository`: Tìm kiếm lớp học đang hoạt động (`ACTIVE`) phù hợp với cấp độ phân lớp (`Nền tảng`, `Tăng tốc`, `Bứt phá`) tại cơ sở đã chọn (`CampusId`). Tự động khởi tạo lớp học nếu cơ sở chưa có lớp tương ứng và tạo bản ghi ghi danh (`ENROLLED`).
- **Nâng Cấp Use Case `SubmitDiagnosticCommandHandler`**:
  - Kết nối hoàn chỉnh chuỗi xử lý khép kín:
    1. Xác thực học sinh & Campus qua Identity gRPC (Bước 1).
    2. Lấy đáp án và metadata câu hỏi qua Content gRPC (Bước 2).
    3. Chấm điểm thô và ghi nhận vi mô thời gian từng câu (Bước 3).
    4. Gửi sang AI Engine tính toán IRT $\theta_0$, BKT $P(L_0)$ và nhận xét sư phạm (Bước 4).
    5. Lưu $P(L_0)$ vào `LearningProfiles`, tự động xếp lớp tại cơ sở và ghi nhận `ClassEnrollments` (Bước 5).
    6. Trả về DTO hoàn chỉnh gồm tọa độ biểu đồ Radar đa giác đối chiếu điểm mục tiêu (V-ACT target score) trong $< 2$ giây (Happy Case).
- **Di Trú CSDL & Khắc Phục Schema PostgreSQL (`Program.cs`)**:
  - Bổ sung migration tự động trên startup: `ALTER TABLE practice.exam_submissions ADD COLUMN IF NOT EXISTS ...` (`theta_0`, `placement_class`, `ai_commentary`, `enrolled_class_id`).
  - Khởi tạo bảng `LearningProfiles`, `Classes`, `ClassEnrollments` trên schema CSDL Supabase.
- **Trực Quan Hóa Dữ Liệu Phản Hồi (Human-Friendly DTOs)**:
  - Bổ sung `SkillName`, `DomainId`, `DomainName` vào `QuestionResultDto` và `SkillDiagnosticDto`.
  - Mở rộng DTO `WeakSkills` dạng object trực quan (`skillId`, `skillName`, `domainName`, `accuracyPercentage`) thay vì chỉ trả về mảng UUID.
  - Tích hợp tên cơ sở đào tạo thực tế (`CampusName`) vào `SubmitDiagnosticResponseDto` và định dạng tên lớp học (`ClassName: "Lớp Nền tảng (Foundation) - Cơ sở ..."`).
  - Truyền dữ liệu miền năng lực thực tế sang AI Engine giúp Biểu đồ Radar đa giác phân tách đầy đủ các trục môn thi của đề ĐGNL ĐHQG-HCM.
- **Kiểm Thử Vận Hành & End-to-End Trực Tiếp Qua Swagger**:
  - Biên dịch toàn bộ giải pháp `V-Eval-Practice_Service.sln`: **0 Warning(s), 0 Error(s)**.
  - Kiểm thử trực tiếp `POST /api/v1/practice/diagnostic-submissions` trên Swagger UI với 30 câu hỏi thật: Nhận kết quả thành công HTTP 200 OK với đầy đủ `theta_0 = -1.5`, xếp lớp `FOUNDATION`, tự động tạo lớp học tại cơ sở, ghi danh học sinh, lưu trữ 12 BKT Priors và nhận xét sư phạm Socratic.

---

## [20/09/2026] - Triển Khai Hoàn Thiện Clean Architecture 4 Tầng, Core Flow 1 (Bước 3: Chấm Điểm Chẩn Đoán 30 Câu & Tích Hợp gRPC)
- **Triển Khai Chuẩn Kiến Trúc Clean Architecture 4 Tầng**:
  - `Domain Layer`: Xây dựng thực thể `ExamSubmission` (thang điểm 0–30, tổng câu 30, thời gian), `SubmissionAnswer` (ghi nhận chi tiết từng câu: `selected_option`, `is_correct`, `time_spent_seconds`) và `IExamSubmissionRepository`.
  - `Application Layer`: Đồng bộ 100% **Result Pattern** (`Result<T>`, `Error`, `ErrorType`), tích hợp **FluentValidation Pipeline** qua `ValidationBehavior`, CQRS MediatR trọn bộ `SubmitDiagnosticCommand`, `GetDiagnosticSubmissionByIdQuery`, `GetDiagnosticSubmissionsByStudentQuery`.
  - `Infrastructure Layer`: Cấu hình EF Core Npgsql trên CSDL Supabase PostgreSQL schema `practice` (`exam_submissions`, `submission_answers`), triển khai gRPC Clients kết nối Identity Service (port 5156) và Content Service (port 5250).
  - `API Layer`: Xây dựng `ApiControllerBase` chuẩn hóa RFC 7807 ProblemDetails, `DiagnosticSubmissionsController` (`[Route("api/v1/practice/diagnostic-submissions")]`), `GlobalExceptionHandlerMiddleware`, Swagger UI tại `http://localhost:5261/swagger`.
- **Hoàn Tất Core Flow 1 (Bước 3)**:
  - Tiếp nhận bài nộp 30 câu hỏi khảo sát chẩn đoán năng lực ban đầu.
  - Tự động gọi gRPC xác thực điều kiện học sinh và cơ sở đào tạo (`CampusId`) từ Identity Service.
  - Tự động gọi gRPC lấy bảng đáp án bảo mật, độ khó câu hỏi và mã kỹ năng từ Content Service.
  - Tự động chấm điểm thô (thang 30), ghi nhận chi tiết thời gian phản hồi (`time_spent_seconds`) từng câu.
  - Chẩn đoán phân tích năng lực: Thống kê tỷ lệ đúng theo Kỹ năng (`SkillBreakdown`), tự động gắn cờ kỹ năng yếu (`WeakSkillIds` có tỷ lệ đúng < 60%), và thống kê theo 4 mức độ khó (Dễ, Trung bình, Khó, Rất khó).
  - Lưu trữ toàn bộ kết quả vào Supabase schema `practice`.
- **Tối Ưu Hóa Trải Nghiệm API (DTO Separation Pattern)**:
  - Tách bạch DTO tóm tắt `DiagnosticSubmissionSummaryDto` cho API lấy lịch sử học sinh (`GET /student/{studentId}`): Chỉ trả về thông số tổng quan (Điểm số, số câu đúng/sai, % chính xác, tổng thời gian, ngày nộp), loại bỏ mảng câu hỏi cồng kềnh giúp tối ưu băng thông và tải trang cực nhanh.
  - Giữ trọn vẹn chi tiết đầy đủ 30 câu hỏi kèm đáp án đúng/sai, thời gian phản hồi và phân tích kỹ năng/độ khó tại API tra cứu chi tiết (`GET /{id}`).
- **Kiểm Thử Toàn Diện (End-to-End Test)**:
  - Biên dịch giải pháp `V-Eval-Practice_Service.sln`: **0 Error(s), 0 Warning(s)**.
  - Chạy kịch bản tích hợp liên dịch vụ 3 microservices (Identity 5155/5156, Content 5249/5250, Practice 5261): Thành công 100%!
- **📌 Kế Hoạch Phối Hợp Kỹ Thuật Bước 4 (Core Flow 1 - Phân Tích Năng Lực IRT & BKT với AI Subsystem)**:
  - **Trách nhiệm của Practice Service**:
    1. Chuẩn bị hợp đồng giao tiếp (gRPC Client `IAiEngineGrpcClient` hoặc Event Bus): Đóng gói gói dữ liệu bài làm của học sinh gồm: `submission_id`, `student_id`, tổng điểm thô (0–30) và danh sách chi tiết 30 câu hỏi (`question_id`, `skill_id`, `difficulty_level`, `is_correct`, `time_spent_seconds`).
    2. Gọi sang **`V-Eval-Ai_Engine`** để kích hoạt tiến trình phân tích trí tuệ nhân tạo.
    3. Nhận phản hồi từ AI Engine gồm:
       - Chỉ số năng lực tiềm ẩn ban đầu $\theta_0$ (Theta IRT).
       - Ma trận xác suất làm chủ ban đầu $P(L_0)$ cho từng Kỹ năng thành phần (Knowledge Component - KC).
       - Tọa độ vector biểu đồ Radar (6–8 trục năng lực) đối chiếu với điểm kỳ vọng (`TargetScore` thang 1200).
    4. Lưu trữ vector năng lực vào CSDL `practice` và kích hoạt Bước 5 (So sánh $\theta_0$ với ngưỡng để phân lớp tại cơ sở).
  - **Trách nhiệm của AI Engine (`V-Eval-Ai_Engine`)**:
    1. Nhận vector dữ liệu 30 câu hỏi từ Practice Service.
    2. Chạy thuật toán **Item Response Theory (IRT)** (mô hình 2PL/3PL) dựa trên độ khó câu hỏi ($b$), độ phân biệt ($a$) và kết quả đúng/sai kèm thời gian phản hồi để ước lượng $\theta_0$.
    3. Chạy mô hình **Bayesian Knowledge Tracing (BKT)**: Khởi tạo xác suất làm chủ tri thức ban đầu $P(L_0)$ cho từng KC theo các tham số định chuẩn khoa học ($P(L_0)$, $P(T)$, $P(S)$, $P(G)$).
    4. Trực quan hóa dữ liệu biểu đồ Radar đa giác năng lực gửi ngược về cho Practice Service / Frontend.

---

## [18/09/2026] - Phát Hành Công Cụ Push Độc Lập `Scripts/push.bat` & Chuẩn Hóa Bộ Docs
- **Khởi Tạo `Scripts/push.bat`**: Đóng gói công cụ push độc lập hỗ trợ 3 chế độ (nhánh hiện tại, danh sách số nhánh có sẵn, tạo nhánh mới).
- **Chuẩn Hóa Bộ Docs Service**: Đồng bộ hệ thống tài liệu theo 3 file chuẩn `daily.md`, `process.md` và `architecture_acceptance.md`.

---

## [15/09/2026] - Dockerize Practice Service (Milestone 2)
- **Tạo `Dockerfile` Multi-stage**: Build và publish .NET 9 API image trên cổng `5002`.
- **Tích hợp Docker Compose**: Khai báo container `v_eval_practice_service` tham gia mạng `veval_network`.

---

## [14/09/2026] - Security & Configuration (Milestone 1)
- **Khởi Tạo `appsettings.example.json`**: Tạo file mẫu chứa đầy đủ `ConnectionStrings` và `JwtSettings` với label mẫu.
- **Bảo Mật Git Security**: Cập nhật `.gitignore` ẩn tất cả file `appsettings.json` chứa thông tin nhạy cảm.
- **Định Tuyến Gateway YARP**: Định tuyến `/api/practice/{**catch-all}` tại Gateway V-Eval trỏ về cổng `:5002`.
