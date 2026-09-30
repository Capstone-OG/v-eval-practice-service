# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [01/10/2026] - Nâng Cấp Core Flow 2 (Bước 1, 2 & 3): Entity Class, Migration CSDL, gRPC DomainCode & Thuật Toán K-Means Phân Cụm Lỗ Hổng

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
- **Bước 3 - Viết Thuật Toán K-Means Student Clustering (K-Means++ & Elbow Method)**:
  - Khởi tạo thuật toán phân cụm chuẩn mực [`StudentKMeansClusterer.cs`](./V-Eval-Practice_Service.Application/Common/Graph/StudentKMeansClusterer.cs) và interface `IStudentKMeansClusterer` trong thư mục `Common/Graph`.
  - Hỗ trợ số lượng học sinh $N$ động ($N \ge 2$), tự động thích ứng giới hạn số cụm $K_{\max} = \min(8, \max(2, \lfloor N / 3 \rfloor))$.
  - Tích hợp 3 kỹ thuật cốt lõi:
    1. **K-Means++ Initialization**: Khởi tạo tâm cụm phân tán đều dựa trên phân phối xác suất bình phương khoảng cách Euclidean, loại bỏ bẫy cực tiểu cục bộ.
    2. **Lloyd's Algorithm**: Tối ưu hóa phân cụm lặp và cập nhật toạ độ tâm cụm vector trung bình kèm cơ chế phục hồi cụm rỗng.
    3. **Elbow Method (Chord Method)**: Quét dải $K \in [2, K_{\max}]$ tính tổng phương sai cụm (WCSS) và tự động nhận diện điểm gập khuỷu tay tối ưu nhất.
  - Phân tích Centroid sư phạm: Tự động phát hiện miền kiến thức yếu nổi trội (< 0.60), gợi ý tên lớp chuyên đề và xác định `TargetDomainCode`.
  - Đăng ký `IStudentKMeansClusterer` vào DI container ([`DependencyInjection.cs`](./V-Eval-Practice_Service.Application/DependencyInjection.cs)).
  - Đã kiểm thử thực nghiệm thành công với bộ dữ liệu mô phỏng 45 học sinh phân hóa 4 nhóm $\rightarrow$ Thuật toán tự động tìm ra $K = 4$ tối ưu với độ suy giảm WCSS từ 4.2867 xuống 0.1173.
- **Kiểm Thử Toàn Diện & Biên Dịch Solution**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
