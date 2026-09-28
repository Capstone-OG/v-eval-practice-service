# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [28/09/2026] - Hợp Nhất Toàn Diện Core Flow 1 (Web Runner UI, Bloom 6 Cấp, Khóa Đề 24h) & Core Flow 2 (Graph Engine 4 Thuật Toán Đồ Thị)

- **Hợp Nhất Toàn Diện Nhánh `develop` và Nhánh `ThinhTT/feat-diagnostic-exam-studio-bloom-flow`**:
  - Tích hợp trọn vẹn giữa giao diện kiểm thử chẩn đoán năng lực, cơ chế phòng chống gian lận 24h và động cơ đồ thị 4 thuật toán cá nhân hóa lộ trình học.
- **Core Flow 2 (Giai Đoạn 2) — Động Cơ Đồ Thị Graph Engine (4 Thuật Toán Đồ Thị & Sư Phạm)**:
  - **Thuật Toán 1: `TarjanCycleDetector.cs` (Tarjan SCC)**:
    - Tìm kiếm các thành phần liên thông mạnh (SCC) để phát hiện và ngăn ngừa chu trình kín trong đồ thị DAG tiên quyết kỹ năng.
    - Phát hiện cả chu trình nhiều đỉnh và self-loop; trả về rỗng khi đồ thị là DAG hợp lệ.
  - **Thuật Toán 2: `PathPruner.cs` (Path Pruning Engine)**:
    - Chiến lược cắt tỉa 3 tầng thông minh: Tầng 1 (trọng số < 5%), Tầng 2 (năng lực đã thành thạo $P(L_0) \ge 85\%$), Tầng 3 (dồn trọng tâm điểm rơi khi thời gian gấp rút < 30 ngày).
  - **Thuật Toán 3: `TopologicalSorter.cs` (Kahn + Priority Queue)**:
    - Sắp xếp thứ tự học tập hợp logic sư phạm dựa trên thuật toán Kahn kết hợp PriorityQueue đa tiêu chí: $\text{PriorityScore} = (1.0 - P(L_0)) \times 0.5 + \text{Weight} \times 0.3 + \text{IsWeak} \times 0.2$.
  - **Thuật Toán 4: `MilestoneBinder.cs` (Milestone Binding Engine)**:
    - Đóng gói từng kỹ năng thành chặng học tập `RoadmapNode`, gắn kết 3 tài nguyên: Video lý thuyết (`material_id`), Quiz củng cố (`quiz_exam_id`), và Lịch Live Q&A (`live_session_id`).
    - Khởi tạo State Machine: Chặng đầu tiên $\rightarrow$ `IN_PROGRESS`, chặng sau $\rightarrow$ `LOCKED`, chặng bị cắt tỉa $\rightarrow$ `SKIPPED_PRUNED`.
- **Core Flow 1 — Web Runner Khảo Sát Năng Lực, AI Exam Studio & Kiểm Soát Hết Hạn 24h (Unhappy Case 2)**:
  - **Kiểm Định Hết Hạn 24h & Khóa Đề (`SubmitDiagnosticCommandHandler.cs`)**:
    - Tự động phát hiện phiên làm bài quá 24h, ghi nhận `Status = "EXPIRED"`, điểm 0 và không tính $\theta_0$ để bảo vệ tính chính xác của mô hình tâm lý học.
    - Ngăn chặn nộp lại đề đã hết hạn (`Exam.Locked`) hoặc đề đã hoàn thành (`Exam.AlreadyCompleted`).
  - **Phát Hành Giao Diện Web Runner Khảo Sát Năng Lực (`wwwroot/view-diagnostic.html`)**:
    - Giao diện trực quan hóa 3 Tab: AI Exam Studio (sinh đề theo Bloom 6 cấp), Phòng Thi Học Sinh (điều hướng 30 câu, Demo Solver), Báo Cáo Năng Lực & Radar Chart.
    - Hỗ trợ lưu CSDL chờ duyệt (`IsPublished = false`), phê duyệt xuất bản (`IsPublished = true`), tự động lưu LocalStorage và phục vụ qua Kestrel `app.UseStaticFiles()`.
- **Kiểm Thử Biên Dịch & Vận Hành**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
