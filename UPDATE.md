# Nhật Ký Cập Nhật (Update Log) - Practice Service

## [29/09/2026] - Triển Khai Hoàn Thiện API 7: Nộp Bài Quiz Bù Cho Học Sinh Vắng Mặt Buổi Live Q&A & Giải Phóng Phong Tỏa Chặng

- **Kiến Trúc CQRS & Result Pattern Cho Phân Hệ Lộ Trình (Features/Roadmaps/Commands/SubmitMakeupQuiz)**:
  - Khởi tạo DTOs [`SubmitMakeupQuizDtos.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/DTOs/SubmitMakeupQuizDtos.cs): `SubmitMakeupQuizRequestDto`, `SubmitMakeupQuizResponseDto`.
  - Xây dựng FluentValidation `SubmitMakeupQuizCommandValidator` kiểm tra ràng buộc đầu vào.
  - Triển khai `SubmitMakeupQuizCommand` và [`SubmitMakeupQuizCommandHandler.cs`](./V-Eval-Practice_Service.Application/Features/Roadmaps/Commands/SubmitMakeupQuiz/SubmitMakeupQuizCommandHandler.cs):
    1. Kiểm tra tồn tại chặng học (`404 Not Found`) và phân quyền sở hữu học sinh (`403 Forbidden`).
    2. Kiểm tra State Machine: Chặn nếu chặng học bị khóa (`LOCKED`) hoặc đã cắt tỉa (`SKIPPED_PRUNED`).
    3. Kiểm tra điều kiện tiên quyết xem video: Bắt buộc `node.IsVideoCompleted == true` (xem $\ge 80\%$ video bài giảng lý thuyết).
    4. Kiểm tra buổi Live Q&A và trạng thái điểm danh: Bắt buộc chặng học có liên kết buổi Live (`node.LiveSessionId != null`) và học sinh có trạng thái điểm danh là `ABSENT` trong `LiveSessionAttendance` (chặn `400 BadRequest` nếu không thuộc diện vắng mặt).
    5. Tự động khởi tạo hoặc nạp đề Quiz bù từ Content Service qua gRPC `GetMilestoneQuizAsync` (nếu chưa gán).
    6. Lấy bảng đáp án gốc bảo mật từ Content Service qua gRPC `GetExamAnswerKeysAsync(attendance.MakeupQuizId)`.
    7. Chấm điểm chi tiết từng câu hỏi, lưu bản ghi làm bài vào `ExamSubmissions` (`ExamType = "MAKEUP_QUIZ"`).
    8. Cập nhật `attendance.IsMakeupQuizPassed = isPassed` ($\ge 60\%$).
    9. **Kích Hoạt Máy Trạng Thái Hữu Hạn (FSM)**:
       - Nếu vượt qua bài Quiz bù VÀ học sinh đã vượt qua cả bài Quiz củng cố chuyên đề (`node.IsQuizPassed == true`): Hệ thống chính thức gỡ bỏ điều kiện phong tỏa do vắng mặt, đánh dấu chặng `Status = "COMPLETED"`, tăng `roadmap.CompletedMilestones++` và tự động mở khóa chặng `LOCKED` kế tiếp thành `IN_PROGRESS` (`UnlockedAt = UtcNow`).
       - Nếu trượt bài Quiz bù ($< 60\%$): Chặng tiếp tục bị giữ ở `IN_PROGRESS`, nhắc học sinh xem lại video ghi hình buổi Live (`recording_url`) và làm lại bài Quiz bù.
- **Tầng API Controller (`RoadmapsController.cs`)**:
  - Bổ sung endpoint `[HttpPost("nodes/{nodeId:guid}/submit-makeup-quiz")]` kèm bóc tách `X-User-Id` header xác thực phân quyền.
- **Kiểm Thử Vận Hành Trực Tiếp (Live End-to-End Test)**:
  - Solution `V-Eval-Practice_Service.sln` biên dịch sạch 100% (**0 Warning, 0 Error**).
  - Kiểm thử trực tiếp 5 kịch bản:
    1. Chặn khi chưa xem đủ 80% video lý thuyết: Trả về `400 BadRequest` chuẩn xác.
    2. Chặn khi học sinh không thuộc diện `ABSENT`: Trả về `400 BadRequest` chuẩn xác.
    3. Nộp bài Quiz củng cố khi đang bị `ABSENT`: Ghi nhận 100% điểm quiz củng cố nhưng State Machine chặn không cho hoàn thành chặng (chờ Quiz bù).
    4. Nộp bài Quiz bù điểm dưới 60%: Trả về `scorePercentage: 0%`, `isPassed: false`, chặng học giữ `IN_PROGRESS`.
    5. Nộp bài Quiz bù đạt chuẩn $\ge 60\%$ (100%): Gỡ bỏ hoàn toàn phong tỏa chặng, Node 2 ("Đại số, Hàm số & Giải tích") chuyển thành `COMPLETED`, tự động mở khóa Node 3 ("Ngữ pháp & Logic câu Tiếng Việt") thành `IN_PROGRESS`, `CompletedMilestones` tăng lên 2/437!
