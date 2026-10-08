# ARCHITECTURE ACCEPTANCE REPORT - V-EVAL PRACTICE SERVICE

## 1. SERVICE OVERVIEW
- **Service Name**: V-Eval Practice Service (Online Examination, Auto-Grading & Competency Tracking).
- **Service Port**: `5261` (Local HTTP) / `5002` (Docker Container).
- **Architectural Paradigm**: 4-Layer Clean Architecture & CQRS Pattern.
- **Role in Core Flow 1**: Step 3 (Diagnostic Exam Submission, Automated Scoring, Micro-telemetry Tracking & Downstream AI Readiness).

---

## 2. SYSTEM ARCHITECTURE & INTER-SERVICE COMMUNICATION

### 2.1 Clean Architecture Layers
1. **Domain Layer**:
   - `ExamSubmission`: Root aggregate capturing `SubmissionId`, `StudentId`, `ExamId`, `TotalScore` (0–30 scale), `TotalCorrect`, `TotalQuestions` (30), `TotalTimeSpentSeconds`, timestamps, and status.
   - `SubmissionAnswer`: Detailed child entity recording `QuestionId`, `SelectedOption`, `IsCorrect`, and `TimeSpentSeconds` for each question item.
   - `IExamSubmissionRepository`: Persistence abstraction.
2. **Application Layer**:
   - **Result Pattern**: Uniform `Result<T>`, `Error`, and `ErrorType` enum aligned across the entire ecosystem.
   - **Pipeline Behavior**: `ValidationBehavior` leveraging FluentValidation to enforce input invariants prior to handler execution.
   - **gRPC Abstractions**: `IIdentityGrpcClient` and `IContentGrpcClient`.
   - **Use Cases (CQRS)**:
     - `SubmitDiagnosticCommand`: Executes auto-grading, micro-telemetry calculation, skill diagnosis, and database persistence.
     - `GetDiagnosticSubmissionByIdQuery`: Fetches granular test results.
     - `GetDiagnosticSubmissionsByStudentQuery`: Retrieves student submission history.
3. **Infrastructure Layer**:
   - **Persistence**: EF Core with Npgsql provider targeting Supabase PostgreSQL schema `practice` (`exam_submissions` and `submission_answers`). Automatic schema migration on startup.
   - **gRPC Clients**: Connects to `Identity Service` (dedicated HTTP/2 port `5156`) and `Content Service` (dedicated HTTP/2 port `5250`).
4. **API Layer**:
   - `ApiControllerBase`: Maps `Result<T>` to standard RFC 7807 `ProblemDetails`.
   - `DiagnosticSubmissionsController`: RESTful endpoints adhering to `/api/v1/practice/diagnostic-submissions`.
   - `GlobalExceptionHandlerMiddleware`: Handles unhandled 500 exceptions gracefully.
   - `Swagger UI`: Interactive documentation hosted at `http://localhost:5261/swagger`.

---

## 3. CORE FLOW 1 INTEGRATION (DIAGNOSTIC ASSESSMENT & PLACEMENT)

### 3.1 Flow Execution (Step 3)
1. **Client Submission**: Student posts 30 question answers with individual `time_spent_seconds`.
2. **Inter-Service Verification via Identity gRPC**: Calls `GetStudentProfileSummary` on port 5156 to verify student existence and confirm campus selection (`CampusId`).
3. **Answer Key Retrieval via Content gRPC**: Calls `GetExamAnswerKey` on port 5250 to obtain official keys, competency `SkillId`, and `DifficultyLevel` securely server-to-server.
4. **Automated Scoring & Telemetry Tracking**:
   - Raw score computed on a 0–30 scale (1 point per correct answer).
   - Per-question response time recorded (`time_spent_seconds`).
   - Grouping by `SkillId` to evaluate competency mastery and isolate `WeakSkillIds` (accuracy < 60%).
   - Grouping by `DifficultyLevel` (Easy, Medium, Hard, Very Hard) to profile cognitive performance.
5. **AI Subsystem Integration (Step 4)**:
   - Practice Service dispatches the 30-question diagnostic vector to `AI Engine` (`POST /api/v1/diagnostic/analyze`).
   - Estimates overall ability `` `\theta_0 \in [-3.0, +3.0]` `` (IRT 2PL + MAP).
   - Calculates initial mastery priors `` `P(L_0) \in [0.05, 0.95]` `` for all skills (Logistic Sigmoid), handling missing branch skills via domain-level fallback.
   - Generates multi-domain radar chart coordinates against the student's target score (`800/1200`).
   - Dynamically produces Socratic pedagogical feedback via Gemini.
   - Saves initial mastery priors into `LearningProfiles` (`mastery_score = p_l0`).
6. **Automatic Campus Class Placement (Step 5)**:
   - Evaluates placement tier based on `` `\theta_0` ``: `FOUNDATION` (`` `\theta_0 < -0.5` ``), `ACCELERATION` (`` `-0.5 \le \theta_0 \le 0.5` ``), `BREAKTHROUGH` (`` `\theta_0 > 0.5` ``).
   - Finds or initializes the corresponding class in `Classes` for the student's registered `CampusId`.
   - Records enrollment in `ClassEnrollments` linked to `diagnostic_submission_id`.
   - Returns full response payload with radar coordinates and Socratic guidance in under 2 seconds (Happy Case).

### 3.2 Unhappy Cases Acceptance (Edge Cases & Resilience)
1. **Unhappy Case 1 (Network Disconnection During Exam)**:
   - Backend gracefully accepts submission payloads regardless of delay, computing accurate duration from recorded `time_spent_seconds` per question item.
2. **Unhappy Case 2 (Abandoned Exam & 24-Hour Session Expiration)**:
   - Submissions exceeding 24 hours (`(DateTime.UtcNow - StartedAt).TotalHours > 24` or `TotalTimeSpentSeconds > 86400`) are automatically intercepted.
   - The session is persisted into the database with `Status = "EXPIRED"` and zero score, locking the old test.
   - Returns RFC 7807 validation error `Exam.Expired`, prompting the student to retake a new randomized diagnostic test to protect psychometrics model integrity.
   - Attempts to resubmit locked expired exams are prevented with `Exam.Locked`.
3. **Unhappy Case 3 (Untracked Sub-Skills in Compact Exam)**:
   - Missing sub-skills automatically inherit `P(L_0)` priors derived from their parent domain ability (`theta_domain`), preventing Topo Sort graph calculation failures.

---

## 4. CORE FLOW 2 READINESS (PATH PLANNING & LIVE SESSIONS)

### 4.1 Phase 1 Domain Entities & Database Mappings
1. **`LearningRoadmap`**:
   - Represents the personalized roadmap aggregate for each student, referencing the baseline diagnostic submission (`diagnostic_submission_id`).
   - Tracks milestones progress (`total_milestones`, `completed_milestones`), time-budget pruning status (`is_pruned`, `pruned_reason`), and roadmap lifecycle (`ACTIVE`, `COMPLETED`, `ARCHIVED`).
2. **`RoadmapNode`**:
   - Encapsulates discrete milestones combining three components: theoretical lecture (`material_id`), formative quiz (`quiz_exam_id`), and live interactive session (`live_session_id`).
   - Manages progressive milestone unlocking (`LOCKED`, `IN_PROGRESS`, `COMPLETED`, `SKIPPED_PRUNED`).
3. **`LiveSession` & `LiveSessionAttendance`**:
   - Manages physical campus cohort online Q&A sessions (`meeting_url`).
   - Implements Unhappy Case 3 fallback: records sessions (`recording_url`, `is_recorded = true`) and tracks mandatory makeup quizzes (`makeup_quiz_id`, `is_makeup_quiz_passed = false`) for absent students.

### 4.2 Phase 2 Graph Engine Algorithms
1. **`TarjanCycleDetector`** (`Application/Common/Graph/TarjanCycleDetector.cs`):
   - Implements Tarjan's Strongly Connected Components (SCC) algorithm for cycle detection in skill prerequisite graphs.
   - Returns empty list for valid DAGs; detects multi-node cycles and self-loops.
2. **`PathPruner`** (`Application/Common/Graph/PathPruner.cs`):
   - Implements 3-tier pruning: weight threshold (<5%), mastery threshold (`P(L0) >= 85%`), and focus-concentration for high-value domains.
   - Calculates available vs required time budget and triggers pruning when overloaded.
3. **`TopologicalSorter`** (`Application/Common/Graph/TopologicalSorter.cs`):
   - Implements Kahn's algorithm with pedagogical PriorityQueue multi-criteria scoring.
   - Priority formula: `(1.0 - P(L0)) * 0.5 + Weight * 0.3 + IsWeak * 0.2`.
4. **`MilestoneBinder`** (`Application/Common/Graph/MilestoneBinder.cs`):
   - Converts Topo-sorted skill list into `RoadmapNode` entities with 3-component binding (Video, Quiz, Live).
   - Initializes State Machine: first non-pruned milestone as `IN_PROGRESS`, rest as `LOCKED`.

### 4.3 Phase 3 Implementation (Core Flow 2 Endpoints)
1. **API 1 - Generate Roadmap (`POST /api/v1/practice/roadmaps/generate`)**:
   - Executes 7-step pedagogical optimization integrating Flow 1 diagnostic outputs, Content Service gRPC skill tree, Tarjan cycle verification, 3-tier time pruning, Kahn pedagogical topological sort, and 3-component milestone binding.
   - Implements Domain Grouping (`RoadmapStageDto`) providing structured stages by Competency Domain while maintaining optimal chronological `stepOrder`.
2. **API 2 - Get My Roadmap (`GET /api/v1/practice/roadmaps/my-roadmap`)**:
   - Retrieves active learning roadmap timeline including comprehensive progress metrics (`ProgressPercentage`), domain stages, and sequential node milestones.
3. **API 3 - Get Roadmap Node Detail (`GET /api/v1/practice/roadmaps/nodes/{nodeId}`)**:
   - Retrieves detailed breakdown of an individual milestone encompassing lecture video (`MaterialId`), formative quiz (`QuizExamId`), and live cohort session (`LiveSession` with attendance tracking & makeup quiz status).
   - Enforces student ownership security (`403 Forbidden`).
4. **API 4 - Track Video Progress (`POST /api/v1/practice/roadmaps/nodes/{nodeId}/track-video`)**:
   - Records cumulative lecture watch duration (`WatchedDurationSeconds`, `TotalDurationSeconds`), calculating real-time percentage (`WatchPercentage`).
   - Enforces 80% completion prerequisite rule before student is eligible to unlock formative quiz (`IsQuizEligible = true`).
   - Validates milestone state machine (`LOCKED` and `SKIPPED_PRUNED` states rejected with `400 BadRequest`).
5. **API 5 - Get Milestone Formative Quiz (`GET /api/v1/practice/roadmaps/nodes/{nodeId}/quiz`)**:
   - Inter-service gRPC contract `GetMilestoneQuiz` retrieves 5-10 milestone-aligned questions from Content Service.
   - Enforces prerequisite check: Rejects requests if student has not watched $\ge 80\%$ of theoretical lecture (`IsVideoCompleted = false`).
   - Strict Anti-Cheating Protocol: Strips all correct options (`is_correct`, `correct_option`) and explanations from student payload.
   - Auto-binds and persists `QuizExamId` into `RoadmapNodes` on first access to guarantee consistent re-taking.
6. **API 6 - Submit Milestone Quiz & FSM State Machine Unlock (`POST /api/v1/practice/roadmaps/nodes/{nodeId}/submit-quiz`)**:
   - Automated server-to-server grading via Content Service gRPC `GetExamAnswerKeys` obtaining tamper-proof official answer keys.
   - Creates full audit trail in `ExamSubmissions` (`ExamType = "QUIZ_MILESTONE"`) and `SubmissionAnswers` with individual question performance.
   - Finite State Machine (FSM) Transition:
     - Passing Threshold $\ge 60\%$: Transitions current node from `IN_PROGRESS` to `COMPLETED` (`CompletedAt = UtcNow`), increments roadmap `CompletedMilestones`, and automatically queries next `LOCKED` milestone via `GetNextLockedNodeAsync` to unlock it into `IN_PROGRESS` (`UnlockedAt = UtcNow`).
     - Failing Score $< 60\%$: Milestone remains `IN_PROGRESS`, subsequent nodes stay `LOCKED`, and pedagogical feedback advises lecture review and retake.
7. **API 7 - Submit Makeup Quiz for Absent Cohort Students (`POST /api/v1/practice/roadmaps/nodes/{nodeId}/submit-makeup-quiz`)**:
   - Implements Unhappy Case 3 (Absenteeism Fallback): Targets students marked as `ABSENT` during scheduled cohort Live Q&A sessions.
   - Validates prerequisites: enforces $\ge 80\%$ lecture watch completion and verified `ABSENT` attendance status.
   - Grades makeup quiz (5 questions) server-to-server via Content Service gRPC and creates audit submission (`ExamType = "MAKEUP_QUIZ"`).
   - Milestone Unblocking: When student achieves $\ge 60\%$ (`IsMakeupQuizPassed = true`) AND has passed the formative milestone quiz (`node.IsQuizPassed = true`), the absenteeism block is fully cleared, milestone transitions to `COMPLETED`, and next `LOCKED` milestone transitions to `IN_PROGRESS`.
8. **API 8 - Create Live Q&A Session (`POST /api/v1/practice/live-sessions`)**:
   - Enables Academic Managers to schedule live cohort Q&A sessions linked to campus classes (`Classes`).
   - Automatically binds class teacher (`TeacherId`) if omitted and generates unique room links (`MeetingUrl`).
9. **API 9 - Assign Teacher to Campus Class (`PUT /api/v1/practice/classes/{classId}/assign-teacher`)**:
   - Facilitates cohort teacher assignment and reassignment (`TeacherId`, `AssignedBy`, `AssignedAt`).
10. **API 10 - Get Student Live Q&A Cohort Schedule (`GET /api/v1/practice/live-sessions/my-schedule`)**:
    - Queries active campus enrollment (`ClassEnrollments`) and retrieves upcoming live sessions.
    - Aggregates individual attendance status (`ATTENDED`, `ABSENT`, `NOT_ATTENDED`), recording links, and makeup quiz results.
11. **API 11 - Join Live Session & Arrival Telemetry (`POST /api/v1/practice/live-sessions/{sessionId}/join`)**:
    - Validates session state (rejects cancelled sessions) and supplies live room URL (`MeetingUrl`).
    - Records arrival timestamp (`JoinedAt = UtcNow`) while strictly preserving official attendance grading authority for teachers in API 12.
12. **API 12 - Teacher Attendance Grading (`POST /api/v1/practice/live-sessions/{sessionId}/attendance`)**:
    - Allows teachers to formally grade attendance for class students (`ATTENDED` or `ABSENT`).
    - Upserts `LiveSessionAttendance` records and tracks cohort attendance counts.
13. **API 13 - Teacher Live Schedule & Cohort Monitoring (`GET /api/v1/practice/live-sessions/teacher-schedule`)**:
    - Queries assigned sessions for a teacher (`teacherId`), aggregating cohort enrollment size, attendance counts (`totalAttended`, `totalAbsent`), meeting and recording URLs.
14. **API 14 - Update Live Session Recording (`PUT /api/v1/practice/live-sessions/{sessionId}/recording`)**:
    - Enables teachers to publish session video archive (`RecordingUrl`), marks `IsRecorded = true` and updates session status to `COMPLETED` for absent student review.
15. **API 15 - Teacher / Academic Manager Cancel Live Session (`PUT /api/v1/practice/live-sessions/{sessionId}/cancel`)**:
    - Grants teachers or academic managers the ability to cancel scheduled sessions when an emergency occurs.
    - Preserves data integrity: prevents physical deletion (since sessions are created by Academic Managers and require audit history).
    - Updates `Status` to `CANCELLED` and appends cancellation reason to description.
    - Prevents downstream interactions: cancelled sessions reject student join attempts (API 11), teacher attendance grading (API 12), and recording uploads (API 14).

---

## 5. ACCEPTANCE & VERIFICATION METRICS
- **Diagnostic Assessment & AI Exam Studio Runner UI**: Interactive web runner hosted at `http://localhost:5261/view-diagnostic.html` with KaTeX formula support, interactive Chart.js Radar Chart, multi-scenario Demo Solver, and AI Exam Studio Tab with customizable teacher prompt and Bloom 6 difficulty levels.
- **Database Save Pending & Publishing Flow**: Dedicated button to save generated exam into Supabase PostgreSQL with `IsPublished = false` (Pending Approval), seamlessly published (`IsPublished = true`) on teacher acceptance.
- **Cognitive Taxonomy Standardization**: Standardized difficulty metrics across 6 Revised Bloom's Taxonomy levels (Remembering, Understanding, Applying, Analyzing, Evaluating, Creating).
- **Solution Build**: Compiled cleanly with **0 Warning(s), 0 Error(s)** (`V-Eval-Practice_Service.sln`).
- **Database Schema**: All 4 target tables (`LearningRoadmaps`, `RoadmapNodes`, `LiveSessions`, `LiveSessionAttendance`) mapped in `PracticeDbContext` under `v_eval_practice` schema and provisioned on Supabase PostgreSQL.
- **End-to-End Integration Verification**: Complete automated test script (`e2e_core_flow1.ps1`) verified all 5 steps across 3 microservices (Identity, Content, Practice): **Passed 100%**.
- **REST & gRPC Dual Port Protocol Architecture**:
  - Identity Service: Port 5155 (REST HTTP/1) + Port 5156 (gRPC HTTP/2).
  - Content Service: Port 5249 (REST HTTP/1) + Port 5250 (gRPC HTTP/2).
  - Practice Service: Port 5261 (REST HTTP/1 + Swagger UI).

---

## 6. THEMATIC COHORT ARCHITECTURE (CORE FLOW 2 UPGRADE - STEP 1)
- **Problem Formulation**: Shift from homogeneous administrative cohorting to domain-specialized cohorts (Thematic Cohorts) formed through K-Means clustering over multi-dimensional student vulnerability vectors.
- **Data Model Extensions ([`Class.cs`](../V-Eval-Practice_Service.Domain/Entities/Class.cs))**:
  - `ClassType` (`int`): Discern between Tier-based Administrative Classes (`0`) and Thematic Cohorts (`1`).
  - `DomainId` (`Guid?`): Unique identifier of target educational domain.
  - `DomainCode` (`string?`): Domain identifier (`DOM_LANG`, `DOM_MATH`, `DOM_NAT_SCI`, `DOM_SOC_SCI`).
  - `ClusterIndex` (`int?`): Index of the optimal K-Means cluster cluster centroid producing this thematic cohort.
- **Migration & Verification**:
  - Migration `AddThematicCohortFields` executed cleanly and verified via live Supabase PostgreSQL schema inspection (`class_type`, `cluster_index`, `domain_code`, `domain_id`).
  - Backward compatibility preserved 100% across all existing core flow 1 and core flow 2 handlers.

### 6.2 gRPC Protocol & DTO Chain Serialization (Step 2)
- **gRPC Contract Alignment**: Added `domain_code` to `SkillNode` message in `content.proto`.
- **Client Deserialization**: Extended `SkillTreeNodeDto` in `IContentGrpcClient` and deserialized in `ContentGrpcClient.cs`.
- **DTO Chain Propagation**:
  - `RoadmapNodeSummaryDto`: Includes `DomainCode` for each milestone.
  - `RoadmapStageDto`: Includes `DomainCode` to enable stage-level domain coloring and categorization on Frontend.
  - `RoadmapNodeDetailDto`: Includes `DomainCode` for milestone inspection.
  - `GenerateRoadmapResponseDto`: Transmits `PlacementClass` (`FOUNDATION` / `ACCELERATION` / `BREAKTHROUGH`) alongside structured stages.

### 6.3 Student K-Means Clustering & Adaptive Elbow Method Engine (Step 3)
- **Algorithm Architecture ([`StudentKMeansClusterer.cs`](../V-Eval-Practice_Service.Application/Common/Graph/StudentKMeansClusterer.cs))**:
  - Input: $N$ dynamic students, each represented by a 4-dimensional vector $[\text{DOM\_LANG}, \text{DOM\_MATH}, \text{DOM\_NAT\_SCI}, \text{DOM\_SOC\_SCI}]$ representing domain-aggregated prior knowledge $P(L_0)$.
  - Adaptive Cluster Bounds: Evaluates $K \in [2, K_{\max}]$ where $K_{\max} = \min(8, \max(2, \lfloor N / 3 \rfloor))$ to guarantee pedagogically viable cohort sizes.
  - K-Means++ Seeding: Samples initial centroids proportionally to squared Euclidean distance $D(x)^2$, circumventing degenerate local minima.
  - Lloyd's Iterative Optimization: Executes iterative nearest-centroid assignment and vector mean updates until convergence with empty-cluster recovery.
  - Geometric Elbow Method: Computes within-cluster sum of squares (WCSS) and identifies the optimal inflection point via maximum perpendicular chord distance.
  - Centroid Pedagogical Profiling: Automatically deduces prominent vulnerability domains (scores $< 0.60$), maps `TargetDomainId` and generates tailored cohort titles (e.g., *"Chuyên đề: Trọng điểm Toán - Logic"*).
- **Verification**: Verified with 45-student heterogeneous dataset; automatically isolated $K = 4$ optimal cohorts with WCSS sharp drop from 4.2867 to 0.1173.

### 6.4 Thematic Cohort Formation & Auto-Cluster API (Step 4)
- **API Specification**: `POST /api/practice/classes/auto-cluster`
  - Request: `AutoClusterThematicClassesRequestDto` (`CampusId`, `Grade`, `MaxCohortCapacity`).
  - Response: `AutoClusterThematicClassesResponseDto` (`TotalStudentsProcessed`, `OptimalK`, `ClassesCreated`).
- **Orchestration Pipeline (`AutoClusterThematicClassesCommandHandler`)**:
  1. Retrieve enrolled student IDs within campus (`GetEnrolledStudentIdsByCampusIdAsync`).
  2. Batch load multi-dimensional student skill priors (`LearningProfiles`) across all cohort students.
  3. Query Content Service gRPC `GetSkillsTree` to map granular skills to top-level domains (`DOM_LANG`, `DOM_MATH`, `DOM_NAT_SCI`, `DOM_SOC_SCI`).
  7. Enroll students into their respective thematic classes in `ClassEnrollments`.

### 6.5 Thematic Cohort Milestone LiveSession Binding (Step 5)
- **Domain-Specialized Interactive Binding**: Overcomes the previous limitation of attaching a single static administrative live session across all milestones.
- **Repository Abstraction (`GetUpcomingThematicLiveSessionsAsync`)**:
  - Implements a resilient 3-tier lookup hierarchy:
    1. *Individual Thematic Tier*: Queries scheduled live sessions (`SCHEDULED`) for thematic classes (`ClassType = 1`) that the individual student is currently enrolled in, keyed by `DomainCode`.
    2. *Campus Thematic Tier*: In cases where the student is not yet enrolled across all 4 knowledge domains, inspects campus-wide thematic cohorts (`CampusId`) for matching domain sessions.
    3. *Administrative Cohort Fallback*: Falls back to the baseline tier class (`ClassType = 0`) session via the `"DEFAULT"` fallback key.
- **Handler Integration (`GenerateRoadmapCommandHandler`)**:
  - Evaluates `SkillTreeNodeDto.DomainCode` for each milestone during Kahn Topological Sort output binding.
  - Binds the precise domain-specific interactive session `LiveSessionId` (e.g., Mathematics chặng $\rightarrow$ Math Live Session, Language chặng $\rightarrow$ Language Live Session).

### 6.6 Multi-Class Enrollment & Schedule Aggregation (Step 6)
- **Problem Resolution**: Following K-Means thematic clustering, students belong concurrently to an administrative placement class (`ClassType = 0`) and one or more thematic vulnerability cohorts (`ClassType = 1`).
- **Repository Upgrade (`LiveSessionRepository.GetUpcomingSessionsForStudentAsync`)**:
  - Replaced single-record selection (`FirstOrDefaultAsync`) with multi-class aggregation: queries all active enrollments for `studentId` (`e.Status == "ENROLLED"`), yielding `classIds`.
  - Dispatches an aggregated query across all classes via `classIds.Contains(s.ClassId)` ordered chronologically.
- **DTO Model Enhancements (`LiveSessionScheduleItemDto`)**:
  - Exposed `ClassId`, `ClassName`, and `DomainCode` (`DOM_LANG`, `DOM_MATH`, `DOM_NAT_SCI`, `DOM_SOC_SCI`) for each scheduled session, empowering students to readily distinguish domain live lectures from general cohort meetings.

---

## 7. CORE FLOW 3 ARCHITECTURE: ADAPTIVE PRACTICE (P-L-A-R) & STAGE INITIALIZATION

### 7.1 P-L-A-R State Machine & Domain Entities
- **Aggregate Entity (`StageProgress`)**:
  - Models the execution life cycle of an individual milestone via 4 deterministic phases: `PREVIEW`, `LEARN`, `APPLY`, and `REFLECT`.
  - Persists real-time pedagogical tracking telemetry: `VideoWatchPercentage`, BKT state variable `BktMasteryPlt` (default `0.1000`), consecutive hard successes `ConsecutiveAdvancedCorrect`, consecutive failures `ConsecutiveIncorrect`, and milestone status `Status` (`IN_PROGRESS`, `REMEDIAL_REQUIRED`, `COMPLETED`).
- **Child Entity (`AdaptiveQuizAttempt`)**:
  - Micro-telemetry audit trail of every item answered during the `APPLY` phase.
  - Captures IRT 2PL item characteristics (`ItemDifficultyB`, `ItemDiscriminationA`), `TimeSpentSeconds`, rapid guess penalty flag `IsLuckyGuess`, and Bayesian updates (`PriorPlt`, `PosteriorPlt`).
- **Persistence Abstraction**:
  - Registered `IStageProgressRepository` and `StageProgressRepository` with EF Core cascading navigation properties to `RoadmapNodes` and `AdaptiveAttempts`.

### 7.2 Stage Initialization API Specification (API 1)
- **Endpoint**: `POST /api/practice/stages/{roadmapNodeId}/start`
  - Route: Clean REST path without `v1` version prefix (`[Route("api/practice/stages")]`).
  - Request: `StartStageRequestDto` (`StudentId`).
  - Response: `StartStageResponseDto` (`StageProgressId`, `RoadmapNodeId`, `SkillId`, `CurrentStep`, `Status`, `BktMasteryPlt`, `PreviewQuestions`).
- **Orchestration Pipeline (`StartStageCommandHandler`)**:
  1. Validates milestone existence in `RoadmapNodes` via `ILearningRoadmapRepository.GetNodeByIdAsync`.
  2. Idempotently locates existing `StageProgress` or provisions a new record initialized at `PREVIEW` phase with `BktMasteryPlt = 0.1000`.
  3. Preloads 3 prerequisite Quick Check items from Content Service via gRPC `GetMilestoneQuizAsync` (`questionCount = 3`) with resilient local fallback for zero-downtime offline execution.

### 7.3 Preview Quick Check Evaluation & Phase Transition API Specification (API 2)
- **Endpoint**: `POST /api/practice/stages/{stageProgressId}/preview-submit`
  - Route: Clean REST path without `v1` version prefix (`[HttpPost("{stageProgressId:guid}/preview-submit")]`).
  - Request: `SubmitPreviewRequestDto` (`StudentId`, `Answers` list containing `QuestionId`, `SelectedOption`, `TimeSpentSeconds`).
  - Response: `SubmitPreviewResponseDto` (`StageProgressId`, `CurrentStep`, `TotalCorrect`, `TotalQuestions`, `FeedbackMessage`).
- **Orchestration Pipeline (`SubmitPreviewCommandHandler`)**:
  1. Verifies existing `StageProgress` via `IStageProgressRepository.GetByIdAsync` and validates ownership (`StudentId`).
  2. Enforces state machine invariant: `CurrentStep == "PREVIEW"` (rejects invalid transitions if already in `LEARN`, `APPLY`, or `REFLECT`).
  3. Fetches official answer keys from Content Service via gRPC `GetExamAnswerKey` or verifies against preloaded keys.
  4. Automatically transitions stage state machine to phase 2: `CurrentStep = "LEARN"`, unlocking theoretical materials and lecture videos.
  5. Dynamically generates pedagogical feedback based on score (e.g. 3/3: Excellent baseline readiness; < 3: Recommended careful video review in LEARN phase).

### 7.4 Lecture Video Telemetry & Adaptive Practice Unlock API Specification (API 3)
- **Endpoint**: `POST /api/practice/stages/{stageProgressId}/track-video`
  - Route: Clean REST path without `v1` version prefix (`[HttpPost("{stageProgressId:guid}/track-video")]`).
  - Request: `TrackVideoRequestDto` (`StudentId`, `WatchedSeconds`, `TotalSeconds`).
  - Response: `TrackVideoResponseDto` (`StageProgressId`, `CurrentStep`, `VideoWatchPercentage`, `IsCompletedLearn`, `NextAction`, `Message`).
- **Orchestration Pipeline (`TrackVideoCommandHandler`)**:
  1. Locates `StageProgress` record and verifies student ownership (`progress.StudentId == request.StudentId`).
  2. Computes progressive watch percentage `` `\text{percentage} = \min(100.0, \frac{\text{WatchedSeconds}}{\text{TotalSeconds}} \times 100)` `` monotonically (`Math.Max(progress.VideoWatchPercentage, percentage)`).
  3. State Machine transition: When `` `\text{VideoWatchPercentage} \ge 80.0\%` `` and current state is `LEARN`, transitions `CurrentStep` to `APPLY`.
  4. Cross-aggregate synchronization: Automatically updates `RoadmapNode` navigation entity (`IsVideoCompleted = true`, `VideoWatchedSeconds`, `VideoTotalSeconds`), ensuring consistent roadmap timeline progression.
  5. Returns guidance metadata (`NextAction = "START_ADAPTIVE_PRACTICE"`).

### 7.5 Adaptive ZPD Question Selection Engine API Specification (API 4)
- **Endpoint**: `GET /api/practice/stages/{stageProgressId}/next-question`
  - Route: Clean REST path without `v1` version prefix (`[HttpGet("{stageProgressId:guid}/next-question")]`).
  - Response: `NextQuestionResponseDto` (`StageProgressId`, `CurrentStep`, `Status`, `CurrentMasteryPlt`, `AttemptOrder`, `IsFinished`, `Message`, `QuestionId`, `Content`, `Options`, `DifficultyLevel`, `ItemDifficultyB`, `ItemDiscriminationA`, `SkillId`, `SkillName`).
- **Adaptive Engine Architecture (`ZpdQuestionSelector.cs`)**:
  1. **Logit Transformation**: Maps BKT mastery prior `` `P(L_t) \in [0.05, 0.95]` `` to psychometric latent trait `` `\theta = \ln(\frac{P(L_t)}{1 - P(L_t)}) \in [-2.5, +2.5]` ``.
  2. **IRT 2PL Probability Function**: Computes `` `P(X=1 \mid \theta, a, b) = \frac{1}{1 + e^{-1.7 \cdot a \cdot (\theta - b)}}` `` for every unattempted item.
  3. **3-Tier Pedagogical ZPD Filter**:
     - *Tier 1 (Ideal ZPD)*: Filters items within target zone `` `P \in [0.60, 0.75]` ``, sorting by proximity to zone center `` `0.675` ``.
     - *Tier 2 (Relaxed ZPD Fallback)*: Expands selection band to `` `P \in [0.50, 0.85]` `` when item bank is sparse.
     - *Tier 3 (Nearest Neighbor Fallback)*: Selects candidate item with minimum absolute delta `` `|P - 0.675|` ``.
  4. **Security & State Validation**: Completely suppresses correct answer flags in client payloads; gracefully yields `IsFinished = true` if student already achieved mastery or has triggered the 3-consecutive-failure remedial gate (`Status == "REMEDIAL_REQUIRED"`).




