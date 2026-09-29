# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [29/09/2026] - Triển Khai Hoàn Thiện API 1, 2, 3: Quy Hoạch Lộ Trình, Timeline Cá Nhân Hóa & Chi Tiết Chặng Học 3 Thành Phần

- **Hoàn Thiện API 1 (`POST /api/v1/practice/roadmaps/generate`)**:
  - Triển khai quy trình 7 bước nghiệp vụ thích ứng năng lực học sinh, nạp dữ liệu gRPC Content Service.
  - Tích hợp 4 thuật toán đồ thị: `TarjanCycleDetector` (SCC), `PathPruner` (cắt tỉa 3 tầng), `TopologicalSorter` (Kahn DAG + PriorityQueue sư phạm), `MilestoneBinder` (State Machine 3 thành phần Video, Quiz, Live).
  - Tích hợp cấu trúc Phân nhóm Chặng theo Miền Năng Lực (Group by Domain / Stages), bảo toàn thứ tự `stepOrder` sư phạm.
- **Hoàn Thiện API 2 (`GET /api/v1/practice/roadmaps/my-roadmap`)**:
  - Triển khai `GetMyRoadmapQuery` và `GetMyRoadmapQueryHandler` tra cứu lộ trình `ACTIVE` của học sinh.
  - Map DTO toàn diện [`RoadmapTimelineDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapTimelineDto.cs): `ProgressPercentage` tiến độ phần trăm, `Stages` phân môn trực quan và `Nodes` tuần tự.
- **Hoàn Thiện API 3 (`GET /api/v1/practice/roadmaps/nodes/{nodeId}`)**:
  - Triển khai `GetRoadmapNodeDetailQuery` và `GetRoadmapNodeDetailQueryHandler` lấy chi tiết chặng học.
  - Map DTO [`RoadmapNodeDetailDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapNodeDetailDto.cs) và `LiveSessionDetailDto` thể hiện trọn vẹn 3 thành phần (Video `MaterialId`, Quiz `QuizExamId`, Buổi học Live Q&A kèm lịch sử điểm danh và Quiz bù).
  - Kiểm soát phân quyền học sinh bảo mật (`403 Forbidden` khi truy cập chặng học sinh khác).
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Test trực tiếp `GET /api/v1/practice/roadmaps/nodes/{nodeId}` thành công trả về `200 OK` đầy đủ dữ liệu 3 thành phần.
