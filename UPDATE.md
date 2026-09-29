# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [29/09/2026] - Triển Khai Hoàn Thiện API 1 & API 2: Quy Hoạch Lộ Trình Thích Ứng & Tra Cứu Timeline Cá Nhân Hóa (Group by Domain)

- **Hoàn Thiện API 1 (`POST /api/v1/practice/roadmaps/generate`)**:
  - Triển khai quy trình 7 bước nghiệp vụ thích ứng năng lực học sinh, nạp dữ liệu gRPC Content Service.
  - Tích hợp 4 thuật toán đồ thị: `TarjanCycleDetector` (SCC), `PathPruner` (cắt tỉa 3 tầng), `TopologicalSorter` (Kahn DAG + PriorityQueue sư phạm), `MilestoneBinder` (State Machine 3 thành phần Video, Quiz, Live).
  - Tích hợp cấu trúc Phân nhóm Chặng theo Miền Năng Lực (Group by Domain / Stages), bảo toàn thứ tự `stepOrder` sư phạm.
- **Hoàn Thiện API 2 (`GET /api/v1/practice/roadmaps/my-roadmap`)**:
  - Triển khai `GetMyRoadmapQuery` và `GetMyRoadmapQueryHandler` tra cứu lộ trình `ACTIVE` của học sinh.
  - Map DTO toàn diện [`RoadmapTimelineDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapTimelineDto.cs): `ProgressPercentage` tiến độ phần trăm, `Stages` phân môn trực quan và `Nodes` tuần tự.
  - Hỗ trợ linh hoạt bóc tách `StudentId` từ Gateway Header (`X-User-Id`) hoặc query parameter.
- **Kiểm Thử Biên Dịch & Vận Hành Thực Tế**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Test trực tiếp `GET /api/v1/practice/roadmaps/my-roadmap?studentId=...` thành công trả về `200 OK` với 5 Stages và 437 chặng học.
