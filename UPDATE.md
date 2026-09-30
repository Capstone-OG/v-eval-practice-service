# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [01/10/2026] - Nâng Cấp Core Flow 2 (Bước 1 & 2): Mở Rộng Entity Class, Migration CSDL & Đồng Bộ DomainCode Qua gRPC / DTOs

- **Bước 1 - Mở Rộng Mô Hình Thực Thể [`Class.cs`](./V-Eval-Practice_Service.Domain/Entities/Class.cs) & Migration CSDL**:
  - Bổ sung 4 trường dữ liệu cho lớp chuyên đề K-Means: `ClassType`, `DomainId`, `DomainCode`, `ClusterIndex`.
  - Cấu hình ánh xạ cột trong [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs).
  - Áp dụng migration `20260930184119_AddThematicCohortFields` vào CSDL PostgreSQL trên Supabase và kiểm tra schema thành công.
- **Bước 2 - Đồng Bộ DomainCode Qua gRPC & Chuỗi DTO Lộ Trình**:
  - Đồng bộ [`content.proto`](./V-Eval-Practice_Service.Infrastructure/Protos/content.proto) với trường `string domain_code = 8;` trong message `SkillNode`.
  - Cập nhật [`IContentGrpcClient.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/IContentGrpcClient.cs) (`SkillTreeNodeDto`) và [`ContentGrpcClient.cs`](./V-Eval-Practice_Service.Infrastructure/GrpcClients/ContentGrpcClient.cs) map `DomainCode` từ Content Service.
  - Mở rộng các DTO lộ trình: [`RoadmapNodeSummaryDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapNodeSummaryDto.cs), [`RoadmapStageDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapStageDto.cs), [`RoadmapNodeDetailDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapNodeDetailDto.cs) bổ sung `DomainCode`.
  - Bổ sung `PlacementClass` vào [`GenerateRoadmapResponseDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/GenerateRoadmapResponseDto.cs) (`FOUNDATION` / `ACCELERATION` / `BREAKTHROUGH`).
  - Cập nhật [`GenerateRoadmapCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/GenerateRoadmap/GenerateRoadmapCommandHandler.cs), [`GetMyRoadmapQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetMyRoadmap/GetMyRoadmapQueryHandler.cs) và [`GetRoadmapNodeDetailQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetRoadmapNodeDetail/GetRoadmapNodeDetailQueryHandler.cs) map hoàn chỉnh `DomainCode` và `PlacementClass`.
- **Kiểm Thử Toàn Diện & Biên Dịch Solution**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
