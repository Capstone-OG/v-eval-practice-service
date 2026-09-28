# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [28/09/2026] - Khởi Tạo Thực Thể LearningRoadmaps, RoadmapNodes, LiveSessions & LiveSessionAttendance (Core Flow 2 - Giai Đoạn 1)

- **Thực Thể Lộ Trình Cá Nhân Hóa & Chặng Học (`LearningRoadmap.cs` & `RoadmapNode.cs`)**:
  - Khởi tạo entity [`LearningRoadmap.cs`](./V-Eval-Practice_Service.Domain/Entities/LearningRoadmap.cs) quản lý lộ trình học tập cá nhân hóa, liên kết với kết quả bài thi chẩn đoán `ExamSubmissions`, lưu trữ trạng thái cắt tỉa (`IsPruned`, `PrunedReason`) và tổng số mốc học.
  - Khởi tạo entity [`RoadmapNode.cs`](./V-Eval-Practice_Service.Domain/Entities/RoadmapNode.cs) đại diện cho từng chặng học (Milestone) tích hợp chặt chẽ 3 thành phần: Bài giảng lý thuyết (`material_id`), Bài Quiz củng cố (`quiz_exam_id`), và Buổi Live Q&A (`live_session_id`).
- **Thực Thể Lịch Học Trực Tuyến & Điểm Danh (`LiveSession.cs` & `LiveSessionAttendance.cs`)**:
  - Khởi tạo entity [`LiveSession.cs`](./V-Eval-Practice_Service.Domain/Entities/LiveSession.cs) quản lý các buổi học Live Q&A của lớp cơ sở, bổ sung trường `RecordingUrl` và `IsRecorded` phục vụ Unhappy Case 3 (xem lại video khi vắng mặt).
  - Khởi tạo entity [`LiveSessionAttendance.cs`](./V-Eval-Practice_Service.Domain/Entities/LiveSessionAttendance.cs) quản lý điểm danh và điều kiện vượt qua bài Quiz bù (`MakeupQuizId`, `IsMakeupQuizPassed`).
- **Cấu Hình CSDL EF Core 9 (`PracticeDbContext.cs`)**:
  - Đăng ký 4 `DbSet` mới: `LearningRoadmaps`, `RoadmapNodes`, `LiveSessions`, `LiveSessionAttendances` thuộc schema `v_eval_practice`.
  - Cấu hình khóa ngoại, quan hệ 1-N cascade và cascade delete hợp lý giữa Roadmaps - Nodes và LiveSessions - Attendances.
- **Kiểm Thử Biên Dịch & CSDL Supabase**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Khởi tạo thành công các bảng và trường tương ứng trên PostgreSQL Supabase.