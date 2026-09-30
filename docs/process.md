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
  - Phân tích chẩn đoán năng lực: thống kê tỷ lệ đúng theo kỹ năng, nhận diện kỹ năng yếu (`WeakSkillIds` có độ chính xác < 60%), và phân tích theo 6 cấp độ tư duy chuẩn Bloom (1. Nhận biết, 2. Thông hiểu, 3. Vận dụng, 4. Phân tích, 5. Đánh giá, 6. Sáng tạo).
  - Đồng bộ kết quả vào CSDL Supabase PostgreSQL Schema `practice`.
  - Cung cấp dữ liệu vi mô làm đầu vào cho AI Subsystem (Bước 4 & 5) ước lượng vector năng lực `\theta_0`, khởi tạo BKT `P(L_0)`, vẽ Radar đa giác và phân cụm xếp lớp.

### 2. Sơ Đồ CSDL PostgreSQL Schema `practice`
- `practice.exam_submissions`: Phiên nộp bài thi (`submission_id`, `student_id`, `exam_id`, `exam_type`, `total_score`, `total_correct`, `total_questions`, `total_time_spent_seconds`, `started_at`, `completed_at`, `status`).
- `practice.submission_answers`: Chi tiết 30 câu trả lời (`answer_id`, `submission_id`, `question_id`, `selected_option`, `is_correct`, `time_spent_seconds`).
- `practice.LearningRoadmaps`: Lộ trình học tập cá nhân hóa (`roadmap_id`, `student_id`, `target_score`, `total_milestones`, `is_pruned`, `status`).
- `practice.RoadmapNodes`: Chặng học 3 thành phần (`node_id`, `roadmap_id`, `skill_id`, `step_order`, `material_id`, `quiz_exam_id`, `live_session_id`, `status`).
- `practice.LiveSessions`: Buổi học trực tuyến Live Q&A của lớp (`session_id`, `class_id`, `scheduled_at`, `meeting_url`, `recording_url`, `is_recorded`, `status`).
- `practice.LiveSessionAttendance`: Điểm danh và bài Quiz bù (`attendance_id`, `session_id`, `student_id`, `attendance_status`, `makeup_quiz_id`, `is_makeup_quiz_passed`).

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
| 10 | **Core Flow 1: Nộp Bài & Chấm Điểm**| `DiagnosticSubmissionsController` | 🟢 Hoàn thành | 100% | `POST /api/practice/diagnostic-submissions` chấm điểm thang 30, ghi nhận `time_spent` |
| 11 | **Chẩn Đoán Năng Lực & Kỹ Năng Yếu**| `SubmitDiagnosticCommandHandler` | 🟢 Hoàn thành | 100% | Tách `SkillBreakdown`, `WeakSkillIds` (<60%), `DifficultyBreakdown` (6 cấp Bloom) |
| 12 | **API Tra Cứu Bài Nộp Theo ID** | `DiagnosticSubmissionsController` | 🟢 Hoàn thành | 100% | `GET /api/practice/diagnostic-submissions/{id}` |
| 13 | **API Lịch Sử Bài Làm Học Sinh**| `DiagnosticSubmissionsController` | 🟢 Hoàn thành | 100% | `GET /api/practice/diagnostic-submissions/student/{studentId}` |
| 14 | **Swagger UI & ProblemDetails** | `Program.cs` / `ApiControllerBase` | 🟢 Hoàn thành | 100% | Swagger UI tại `http://localhost:5261/swagger`, RFC 7807 |
| 15 | **Core Flow 1 (Bước 4): Tích Hợp AI Subsystem (IRT & BKT)** | `HttpClients/AiDiagnosticClient.cs` & `SubmitDiagnosticCommandHandler.cs` | 🟢 Hoàn thành | 100% | Gửi 30 câu sang `AI Engine` -> Nhận `` `\theta_0` ``, `` `P(L_0)` `` Sigmoid, Radar Chart, nhận xét Gemini |
| 16 | **Core Flow 1 (Bước 5): Tự Động Gợi Ý Phân Lớp Tại Campus** | `Repositories/ClassEnrollmentRepository.cs` | 🟢 Hoàn thành | 100% | Phân lớp `` `\theta_0` `` (FOUNDATION / ACCELERATION / BREAKTHROUGH) -> Tự động ghi danh `ClassEnrollments` |
| 17 | **Core Flow 1 (Unhappy Case 2): Khóa Bài Thi Hết Hạn 24h & Khóa Đề** | `SubmitDiagnosticCommandHandler.cs` | 🟢 Hoàn thành | 100% | Phát hiện bỏ dở > 24h, ghi nhận `EXPIRED`, khóa đề thi và chống gian lận nộp lại |
| 18 | **Chuẩn Hóa Thang Đo Bloom 6 Mức Độ** | `Domain/Constants/BloomTaxonomy.cs` | 🟢 Hoàn thành | 100% | Revised Bloom's Taxonomy 6 cấp độ (Nhận biết -> Sáng tạo) cho Difficulty Breakdown |
| 19 | **Giao Diện Khảo Sát & Radar Chart Runner** | `wwwroot/view-diagnostic.html` | 🟢 Hoàn thành | 100% | UI test thực tế 30 câu, tích hợp KaTeX, Chart.js Radar, Demo Solver đa kịch bản |
| 20 | **AI Exam Studio & Custom Prompting** | `wwwroot/view-diagnostic.html` | 🟢 Hoàn thành | 100% | Sinh đề tùy biến 5 môn hoặc V-ACT, nhận diện ý định prompt, chọn Bloom 6 cấp, Dual Engine |
| 21 | **Lưu Đề CSDL Chờ Duyệt & Xuất Bản** | `wwwroot/view-diagnostic.html` & Content API | 🟢 Hoàn thành | 100% | Nút lưu Supabase `IsPublished = false` (Chờ duyệt), duyệt khi bấm ACCEPT gọi publish (`IsPublished = true`) |
| 22 | **Core Flow 2 (Phase 1): Thực Thể Roadmaps & LiveSessions** | `Domain/Entities/` & `Infrastructure/Persistence/PracticeDbContext.cs` | 🟢 Hoàn thành | 100% | `LearningRoadmap`, `RoadmapNode`, `LiveSession`, `LiveSessionAttendance` và Fluent API mappings |
| 23 | **Core Flow 2 (Phase 2): Graph Engine 4 Thuật Toán** | `Application/Common/Graph/` | 🟢 Hoàn thành | 100% | `TarjanCycleDetector`, `PathPruner`, `TopologicalSorter`, `MilestoneBinder` |
| 24 | **Core Flow 2 - API 1: Khởi Tạo Lộ Trình Học Tập** | `RoadmapsController.cs` & `GenerateRoadmapCommandHandler.cs` | 🟢 Hoàn thành | 100% | `POST /api/practice/roadmaps/generate` quy trình 7 bước, phân nhóm Stage theo miền năng lực |
| 25 | **Core Flow 2 - API 2: Tra Cứu Lộ Trình Cá Nhân Hóa** | `RoadmapsController.cs` & `GetMyRoadmapQueryHandler.cs` | 🟢 Hoàn thành | 100% | `GET /api/practice/roadmaps/my-roadmap` tiến độ phần trăm, gom nhóm Stages theo miền |
| 26 | **Core Flow 2 - API 3: Chi Tiết Chặng Học 3 Thành Phần** | `RoadmapsController.cs` & `GetRoadmapNodeDetailQueryHandler.cs` | 🟢 Hoàn thành | 100% | `GET /api/practice/roadmaps/nodes/{nodeId}` chi tiết Video, Quiz củng cố, Buổi học Live Q&A |
| 27 | **Core Flow 2 - API 4: Ghi Nhận Xem Video Lý Thuyết** | `RoadmapsController.cs` & `TrackVideoCommandHandler.cs` | 🟢 Hoàn thành | 100% | `POST /api/practice/roadmaps/nodes/{nodeId}/track-video` tính % xem, mở khóa Quiz khi đạt >= 80% |
| 28 | **Core Flow 2 - API 5: Lấy Đề Thi Quiz Củng Cố Chặng** | `RoadmapsController.cs` & `GetMilestoneQuizQueryHandler.cs` | 🟢 Hoàn thành | 100% | `GET /api/practice/roadmaps/nodes/{nodeId}/quiz` kiểm tra điều kiện xem video >= 80%, ẩn đáp án đúng bảo mật |
| 29 | **Core Flow 2 - API 6: Nộp Bài Quiz & Mở Khóa FSM** | `RoadmapsController.cs` & `SubmitMilestoneQuizCommandHandler.cs` | 🟢 Hoàn thành | 100% | `POST /api/practice/roadmaps/nodes/{nodeId}/submit-quiz` chấm điểm bảo mật gRPC, kích hoạt State Machine mở khóa chặng kế tiếp khi đạt >= 60% |
| 30 | **Core Flow 2 - API 7: Nộp Quiz Bù Khi Vắng Mặt Live** | `RoadmapsController.cs` & `SubmitMakeupQuizCommandHandler.cs` | 🟢 Hoàn thành | 100% | `POST /api/practice/roadmaps/nodes/{nodeId}/submit-makeup-quiz` giải phóng phong tỏa chặng học cho học sinh ABSENT khi đạt >= 60% |
| 31 | **Core Flow 2 - API 8: Tạo Lịch Buổi Học Live Q&A** | `LiveSessionsController.cs` & `CreateLiveSessionCommandHandler.cs` | 🟢 Hoàn thành | 100% | `POST /api/practice/live-sessions` quản trị viên tạo lịch Live Q&A, tự động gắn giáo viên |
| 32 | **Core Flow 2 - API 9: Phân Công Giáo Viên Cho Lớp** | `ClassesController.cs` & `AssignTeacherCommandHandler.cs` | 🟢 Hoàn thành | 100% | `PUT /api/practice/classes/{classId}/assign-teacher` quản trị cơ sở phân công/điều chuyển giáo viên |
| 33 | **Core Flow 2 - API 10: Thời Khóa Biểu Buổi Học Live** | `LiveSessionsController.cs` & `GetMyLiveScheduleQueryHandler.cs` | 🟢 Hoàn thành | 100% | `GET /api/practice/live-sessions/my-schedule` tra cứu lịch Live Q&A của lớp cơ sở kèm trạng thái điểm danh |
| 34 | **Core Flow 2 - API 11: Tham Gia & Dấu Vết Vào Lớp** | `LiveSessionsController.cs` & `JoinLiveSessionCommandHandler.cs` | 🟢 Hoàn thành | 100% | `POST /api/practice/live-sessions/{sessionId}/join` nhận link phòng học và ghi vết JoinedAt |
| 35 | **Core Flow 2 - API 12: Giáo Viên Điểm Danh Buổi Live** | `LiveSessionsController.cs` & `TeacherAttendanceCommandHandler.cs` | 🟢 Hoàn thành | 100% | `POST /api/practice/live-sessions/{sessionId}/attendance` giáo viên đánh giá chuyên cần (ATTENDED / ABSENT) |
| 36 | **Core Flow 2 - API 13: Lịch Giảng Dạy Của Giáo Viên** | `LiveSessionsController.cs` & `GetTeacherScheduleQueryHandler.cs` | 🟢 Hoàn thành | 100% | `GET /api/practice/live-sessions/teacher-schedule` giáo viên tra cứu lịch Live Q&A được phân công |
| 37 | **Core Flow 2 - API 14: Cập Nhật Video Ghi Hình Buổi Live** | `LiveSessionsController.cs` & `UpdateLiveSessionRecordingCommandHandler.cs` | 🟢 Hoàn thành | 100% | `PUT /api/practice/live-sessions/{sessionId}/recording` cập nhật recording_url và cờ is_recorded |
| 38 | **Core Flow 2 - API 15: Hủy Buổi Học Live Khi Bận Đột Xuất** | `LiveSessionsController.cs` & `CancelLiveSessionCommandHandler.cs` | 🟢 Hoàn thành | 100% | `PUT /api/practice/live-sessions/{sessionId}/cancel` chuyển trạng thái CANCELLED kèm lý do hủy (không xóa vật lý bản ghi) |
| 39 | **Core Flow 5 - API 22: Danh Sách Học Sinh Thuộc Lớp** | `ClassesController.cs` & `GetClassStudentsQueryHandler.cs` | 🟡 Chuyển giao Core Flow 5 | 0% | Quy hoạch sang Core Flow 5 (Learning Analytics): `GET /api/practice/classes/{classId}/students` tra cứu tiến độ lộ trình và chuyên cần học sinh |
| 40 | **Core Flow 5 - API 23: Nhận Diện Học Sinh Nguy Cơ Sa Sút**| `ClassesController.cs` & `GetAtRiskStudentsQueryHandler.cs` | 🟡 Chuyển giao Core Flow 5 | 0% | Quy hoạch sang Core Flow 5 (Learning Analytics): `GET /api/practice/classes/{classId}/at-risk-students` cảnh báo can thiệp sư phạm học sinh nguy cơ |
| 41 | **Core Flow 2 (Thematic Cohort - Bước 1): Mở Rộng Thực Thể Class & Di Trú CSDL** | `Domain/Entities/Class.cs` & `Infrastructure/Migrations/` | 🟢 Hoàn thành | 100% | Bổ sung `ClassType`, `DomainId`, `DomainCode`, `ClusterIndex` vào bảng `Classes`, chạy migration `AddThematicCohortFields` |
| 42 | **Core Flow 2 (Thematic Cohort - Bước 2): Đồng Bộ DomainCode & PlacementClass DTOs** | `Protos/content.proto`, `GrpcClients/` & `DTOs/` | 🟢 Hoàn thành | 100% | Mở rộng proto, `SkillTreeNodeDto`, ánh xạ `DomainCode` vào Stages/Nodes và `PlacementClass` vào response |
| 43 | **Core Flow 2 (Thematic Cohort - Bước 3): Thuật Toán K-Means Student Clustering** | `Application/Common/Graph/StudentKMeansClusterer.cs` | 🟢 Hoàn thành | 100% | Thuật toán KMeans++ và Elbow Method tìm K tối ưu phân cụm lỗ hổng 4 chiều cho N học sinh |
| 44 | **Core Flow 2 (Thematic Cohort - Bước 4): API Tự Động Phân Cụm Lớp Chuyên Đề** | `ClassesController.cs` & `AutoClusterThematicClassesCommandHandler.cs` | 🟢 Hoàn thành | 100% | `POST /api/practice/classes/auto-cluster` tự động gom cụm N học sinh theo lỗ hổng 4 miền, sinh lớp chuyên đề và ghi danh tự động |





