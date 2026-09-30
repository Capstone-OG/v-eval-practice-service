# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [01/10/2026] - Nâng Cấp Core Flow 2 (Bước 1 -> 5): Entity Class, Migration CSDL, gRPC DomainCode, Thuật Toán K-Means, API Auto-Cluster & Gắn LiveSession Theo Miền Chuyên Đề

- **Bước 1 - Mở Rộng Mô Hình Thực Thể [`Class.cs`](./V-Eval-Practice_Service.Domain/Entities/Class.cs) & Migration CSDL**:
  - Bổ sung 4 trường dữ liệu cho lớp chuyên đề K-Means: `ClassType`, `DomainId`, `DomainCode`, `ClusterIndex`.
  - Cấu hình ánh xạ Fluent API trong [`PracticeDbContext.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs).
  - Áp dụng migration `20260930184119_AddThematicCohortFields` vào CSDL PostgreSQL trên Supabase.
- **Bước 2 - Đồng Bộ DomainCode Qua gRPC & Chuỗi DTO Lộ Trình**:
  - Đồng bộ [`content.proto`](./V-Eval-Practice_Service.Infrastructure/Protos/content.proto) với trường `string domain_code = 8;` trong message `SkillNode`.
  - Cập nhật [`IContentGrpcClient.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/IContentGrpcClient.cs) (`SkillTreeNodeDto`) và [`ContentGrpcClient.cs`](./V-Eval-Practice_Service.Infrastructure/GrpcClients/ContentGrpcClient.cs) map `DomainCode` từ Content Service.
  - Mở rộng các DTO lộ trình: [`RoadmapNodeSummaryDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapNodeSummaryDto.cs), [`RoadmapStageDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapStageDto.cs), [`RoadmapNodeDetailDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/RoadmapNodeDetailDto.cs) bổ sung `DomainCode`.
  - Bổ sung `PlacementClass` vào [`GenerateRoadmapResponseDto.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/GenerateRoadmapResponseDto.cs) (`FOUNDATION` / `ACCELERATION` / `BREAKTHROUGH`).
  - Cập nhật [`GenerateRoadmapCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/GenerateRoadmap/GenerateRoadmapCommandHandler.cs), [`GetMyRoadmapQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetMyRoadmap/GetMyRoadmapQueryHandler.cs) và [`GetRoadmapNodeDetailQueryHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Queries/GetRoadmapNodeDetail/GetRoadmapNodeDetailQueryHandler.cs) map hoàn chỉnh `DomainCode` và `PlacementClass`.
- **Bước 3 - Viết Thuật Toán K-Means Student Clustering (K-Means++ & Elbow Method)**:
  - Khởi tạo thuật toán phân cụm chuẩn mực [`StudentKMeansClusterer.cs`](./V-Eval-Practice_Service.Application/Common/Graph/StudentKMeansClusterer.cs) và interface `IStudentKMeansClusterer` trong thư mục `Common/Graph`.
  - Hỗ trợ số lượng học sinh $N$ động ($N \ge 2$), tự động thích ứng giới hạn số cụm $K_{\max} = \min(8, \max(2, \lfloor N / 3 \rfloor))$.
  - Tích hợp 3 kỹ thuật cốt lõi: K-Means++ Seeding, Lloyd's Iteration và Elbow Method (khoảng cách cực đại đến dây cung WCSS).
  - Phân tích Centroid sư phạm: Tự động phát hiện miền kiến thức yếu nổi trội (< 0.60), gợi ý tên lớp chuyên đề và xác định `TargetDomainCode`.
  - Đăng ký `IStudentKMeansClusterer` vào DI container ([`DependencyInjection.cs`](./V-Eval-Practice_Service.Application/DependencyInjection.cs)).
  - Kiểm thử thực nghiệm với 45 học sinh: Elbow Method tìm ra $K = 4$ tối ưu với độ suy giảm WCSS từ 4.2867 xuống 0.1173.
- **Bước 4 - Hiện Thực API Tự Động Phân Cụm Lớp Chuyên Đề (POST /api/practice/classes/auto-cluster)**:
  - Khởi tạo DTOs [`AutoClusterThematicClassesDtos.cs`](./V-Eval-Practice_Service.Application/Features/Classes/DTOs/AutoClusterThematicClassesDtos.cs) (`AutoClusterThematicClassesRequestDto`, `ThematicClassCreatedDto`, `AutoClusterThematicClassesResponseDto`).
  - Bổ sung phương thức `GetByStudentIdsAsync` trong [`ILearningProfileRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILearningProfileRepository.cs) & `LearningProfileRepository.cs`.
  - Bổ sung `GetEnrolledStudentIdsByCampusIdAsync` và `CreateThematicClassWithEnrollmentsAsync` trong [`IClassEnrollmentRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IClassEnrollmentRepository.cs) & `ClassEnrollmentRepository.cs`.
  - Xây dựng `AutoClusterThematicClassesCommand`, `AutoClusterThematicClassesCommandValidator` và handler [`AutoClusterThematicClassesCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Classes/Commands/AutoClusterThematicClasses/AutoClusterThematicClassesCommandHandler.cs).
  - Bổ sung endpoint `[HttpPost("auto-cluster")]` vào [`ClassesController.cs`](./V-Eval-Practice_Service.API/Controllers/ClassesController.cs).
- **Bước 5 - Liên Kết Buổi Học LiveSession Cho Từng Chặng Lộ Trình Theo Đúng Miền Chuyên Đề (Thematic Cohort Binding)**:
  - Bổ sung phương thức `GetUpcomingThematicLiveSessionsAsync` trong [`ILearningRoadmapRepository.cs`](./V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILearningRoadmapRepository.cs) & [`LearningRoadmapRepository.cs`](./V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LearningRoadmapRepository.cs).
  - Triển khai cơ chế truy vấn 3 tầng: Ưu tiên lớp chuyên đề học sinh đã ghi danh $\rightarrow$ Lớp chuyên đề cùng Campus $\rightarrow$ Fallback lớp hành chính chung.
  - Cập nhật Bước 6 trong [`GenerateRoadmapCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/GenerateRoadmap/GenerateRoadmapCommandHandler.cs): Ánh xạ `LiveSessionId` riêng biệt theo đúng `DomainCode` của từng chặng kỹ năng (Toán $\rightarrow$ Live Toán, Ngôn ngữ $\rightarrow$ Live Ngôn ngữ).
- **Kiểm Thử Toàn Diện & Biên Dịch Solution**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
