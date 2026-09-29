# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [29/09/2026] - Triển Khai Hoàn Thiện API 1: Khởi Tạo Lộ Trình Học Tập Thích Ứng (POST /api/v1/practice/roadmaps/generate) & Phân Nhóm Chặng Theo Miền Năng Lực (Group by Domain)

- **Triển Khai API 1 (Core Flow 2 - Giai Đoạn 1)**:
  - Xây dựng hoàn chỉnh endpoint `POST /api/v1/practice/roadmaps/generate` theo chuẩn Clean Architecture và Result Pattern.
  - Tích hợp cấu trúc **Phân nhóm Chặng theo Miền Năng Lực (Group by Competency Domains / Stages)**: DTO đầu ra phân tách rõ ràng theo từng môn/miền (`DomainId`, `DomainName`, `TotalNodes`, `CompletedNodes`, danh sách `Nodes` giữ nguyên thứ tự `stepOrder` tối ưu).
  - Tích hợp điều phối chuỗi 7 bước nghiệp vụ:
    1. Trích xuất tham số psychometrics `theta_0`, lớp học được xếp `EnrolledClassId`, ma trận `P(L_0)` và phát hiện kỹ năng yếu `IsWeak`.
    2. Nạp Cây khung năng lực kỹ năng chuẩn, thông tin miền năng lực và quan hệ DAG từ Content Service qua gRPC (`GetSkillsTreeAsync`).
    3. Kiểm tra chu trình kín bằng thuật toán Tarjan SCC (`TarjanCycleDetector`), bảo vệ toàn vẹn đồ thị.
    4. Cắt tỉa 3 tầng thích ứng quỹ thời gian bằng `PathPruner` (trọng số < 5%, `P(L_0) >= 0.85`, dồn trọng tâm khi thời gian < 30 ngày).
    5. Sắp xếp thứ tự học chuẩn sư phạm bằng thuật toán Kahn kết hợp PriorityQueue (`TopologicalSorter`).
    6. Gắn kết 3 tài nguyên (`MaterialId`, `QuizExamId`, `LiveSessionId`) và khởi tạo State Machine bằng `MilestoneBinder` (`IN_PROGRESS`, `LOCKED`, `SKIPPED_PRUNED`).
    7. Lưu vết lộ trình cũ sang `ARCHIVED`, lưu trữ nguyên tử lộ trình mới cùng các chặng học vào CSDL PostgreSQL schema `v_eval_practice`.
- **Hạ Tầng Repository & gRPC Client**:
  - Triển khai `ILearningRoadmapRepository` và `LearningRoadmapRepository`.
  - Mở rộng proto `content.proto` (thêm `double weight = 5`, `string domain_id = 6`, `string domain_name = 7`), cập nhật `IContentGrpcClient` và `ContentGrpcClient` hiện thực `GetSkillsTreeAsync`.
- **API Controller (`RoadmapsController.cs`)**:
  - Endpoint `POST /api/v1/practice/roadmaps/generate` hỗ trợ bóc tách `StudentId` từ Gateway Header (`X-User-Id`) hoặc request body linh hoạt.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp sinh lộ trình thành công trả về 5 Stages phân nhóm môn (Ngôn ngữ, Toán & Tư duy định lượng, Tổng hợp, Tự nhiên, Xã hội).
