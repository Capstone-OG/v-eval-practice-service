# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [28/09/2026] - Triển Khai Giai Đoạn 2 Core Flow 2: Graph Engine 4 Thuật Toán Đồ Thị & Toán Học

- **Thuật Toán 1: `TarjanCycleDetector.cs` (Tarjan SCC)**:
  - Cài đặt thuật toán Tarjan tìm các thành phần liên thông mạnh (SCC) để phát hiện chu trình kín trong đồ thị tiên quyết kỹ năng.
  - Phát hiện cả SCC nhiều đỉnh và self-loop (đỉnh tự trỏ vào chính nó).
  - Trả về danh sách rỗng khi đồ thị hợp lệ (DAG thuần túy, không chu trình).
- **Thuật Toán 2: `PathPruner.cs` (Path Pruning Engine)**:
  - Cài đặt chiến lược cắt tỉa 3 tầng: Tầng 1 (trọng số < 5%), Tầng 2 (năng lực đã đạt chuẩn `P(L0) >= 85%`), Tầng 3 (dồn trọng tâm điểm rơi).
  - Tính toán quỹ thời gian khả dụng vs thời lượng cần thiết, kích hoạt cắt tỉa khi quỹ thời gian không đủ hoặc gấp rút (< 30 ngày, mục tiêu >= 800 điểm).
- **Thuật Toán 3: `TopologicalSorter.cs` (Kahn + Priority Queue)**:
  - Cài đặt thuật toán Kahn Topological Sort kết hợp PriorityQueue đa tiêu chí.
  - Hàm ưu tiên sư phạm: `PriorityScore = (1.0 - P(L0)) * 0.5 + Weight * 0.3 + IsWeak * 0.2`.
  - Đảm bảo kỹ năng nền tảng (in-degree = 0) luôn đứng trước kỹ năng nâng cao.
- **Thuật Toán 4: `MilestoneBinder.cs` (Milestone Binding Engine)**:
  - Chuyển đổi danh sách kỹ năng đã sắp xếp Topo thành danh sách `RoadmapNode` thực thể.
  - Gắn kết 3 thành phần: Video lý thuyết (`material_id`), Quiz củng cố (`quiz_exam_id`), và Lịch Live Q&A (`live_session_id`).
  - Khởi tạo State Machine: Chặng đầu tiên chưa bị prune => `IN_PROGRESS`, còn lại => `LOCKED`, bị cắt tỉa => `SKIPPED_PRUNED`.
- **Kiểm Thử Biên Dịch**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**) sau mỗi thuật toán.