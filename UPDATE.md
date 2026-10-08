# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [08/10/2026] - Hoàn Tất Toàn Diện Module 1 (P-L-A-R) & Dọn Sạch Toàn Bộ Dead Code Cũ Tại Tầng Application

- **Hoàn Tất Chu Trình Học Thích Ứng P-L-A-R (Module 1 - Core Flow 3)**:
  - Triển khai trọn vẹn 6/6 APIs quản lý chặng học:
    1. `POST /api/practice/stages/{roadmapNodeId}/start`: Khởi tạo chặng học thích ứng (P - Preview).
    2. `POST /api/practice/stages/{stageProgressId}/preview-submit`: Nộp bài khởi động & Mở khóa lý thuyết (P sang L).
    3. `POST /api/practice/stages/{stageProgressId}/track-video`: Ghi nhận thời lượng xem video bài giảng (L - Learn).
    4. `GET /api/practice/stages/{stageProgressId}/next-question`: Lấy câu hỏi thích ứng ZPD IRT 2PL (A - Apply).
    5. `POST /api/practice/stages/{stageProgressId}/submit-answer`: Nộp đáp án thích ứng & Cập nhật BKT, BR-01, BR-03 (A - Apply).
    6. `POST /api/practice/stages/{stageProgressId}/reflect-complete`: Phản tư cá nhân & Hoàn thành chặng học, tự động mở khóa chặng kế tiếp (R - Reflect).
- **Dọn Dẹp Triệt Để Toàn Bộ Tầng Xử Lý Dead Code Cũ Tại Application Layer**:
  - Xóa bỏ 100% các Command, Handler, Validator, Query và DTOs cũ của lộ trình tĩnh trong [`V-Eval-Practice_Service.Application/Features/Roadmaps`](./V-Eval-Practice_Service.Application/Features/Roadmaps):
    - `Commands/TrackVideo/`
    - `Commands/SubmitMilestoneQuiz/`
    - `Commands/SubmitMakeupQuiz/`
    - `Queries/GetMilestoneQuiz/`
    - `DTOs/` (`MilestoneQuizDto.cs`, `SubmitMilestoneQuizDtos.cs`, `SubmitMakeupQuizDtos.cs`, `TrackVideoRequestDto.cs`, `TrackVideoResponseDto.cs`).
  - Định vị rõ vai trò của Roadmap là **Quản lý lộ trình vĩ mô cá nhân hóa** (`generate`, `my-roadmap`, `nodes/{nodeId}`), toàn bộ việc học tập vi mô, video, luyện tập thích ứng được quy hoạch tập trung 100% tại `StagesController` (P-L-A-R).
- **Đồng Bộ Kiến Trúc & Kiểm Thử Vận Hành**:
  - Cập nhật nhật ký tiến độ [`docs/daily.md`](./docs/daily.md) và bảng theo dõi [`docs/process.md`](./docs/process.md).
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).

