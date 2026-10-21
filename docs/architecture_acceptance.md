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

### 7.6 Bayesian Knowledge Tracing & Adaptive Item Submission API Specification (API 5)
- **Endpoint**: `POST /api/practice/stages/{stageProgressId}/submit-answer`
  - Route: Clean REST path without `v1` version prefix (`[HttpPost("{stageProgressId:guid}/submit-answer")]`).
  - Request: `SubmitAnswerRequestDto` (`StudentId`, `QuestionId`, `SelectedOption`, `TimeSpentSeconds`, `PatternId`).
  - Response: `SubmitAnswerResponseDto` (`StageProgressId`, `QuestionId`, `IsCorrect`, `CorrectOption`, `PriorPlt`, `PosteriorPlt`, `IsLuckyGuess`, `CurrentStep`, `Status`, `ConsecutiveAdvancedCorrect`, `ConsecutiveIncorrect`, `IsMasteryAchieved`, `IsRemedialTriggered`, `FeedbackMessage`, `NextAction`).
- **Pedagogical Engine Architecture (`BktEngine.cs`)**:
  1. **Lucky Guess Penalty**: If answer is correct but response time `` `t < 5\text{s}` `` on higher-order item (`` `b \ge 0.50` ``), parameter `` `P(G)` `` spikes from 0.25 to 0.60, preventing inflated mastery scores.
  2. **Bayesian Posterior Update**: Computes `` `P(L_t \mid obs)` `` via standard conditional probability and updates knowledge transition `` `P(L_{t+1}) = P(L_t \mid obs) + (1 - P(L_t \mid obs)) \cdot P(T)` `` with `` `P(T) = 0.15` `` and `` `P(S) = 0.10` ``.
  3. **Rule BR-01 (Mastery Gate)**: When `` `P(L_t) \ge 0.85` `` and student demonstrates mastery over 2 consecutive advanced items (`` `ConsecutiveAdvancedCorrect \ge 2` ``), transitions state machine to `REFLECT`.
  4. **Rule BR-03 (Remedial Gate)**: When student incurs 3 consecutive errors (`` `ConsecutiveIncorrect \ge 3` ``), halts adaptive item delivery and locks stage into `Status = "REMEDIAL_REQUIRED"`.

### 7.7 Metacognitive Reflection & Milestone Completion API Specification (API 6)
- **Endpoint**: `POST /api/practice/stages/{stageProgressId}/reflect-complete`
  - Route: Clean REST path without `v1` version prefix (`[HttpPost("{stageProgressId:guid}/reflect-complete")]`).
  - Request: `ReflectCompleteRequestDto` (`StudentId`, `ConfidenceRating`, `LearnedSummary`, `MistakeNotes`).
  - Response: `ReflectCompleteResponseDto` (`StageProgressId`, `RoadmapNodeId`, `Status`, `ConfidenceRating`, `FinalMasteryPlt`, `NextUnlockedNodeId`, `Message`).
- **Cross-Aggregate Orchestration Pipeline (`ReflectCompleteCommandHandler`)**:
  1. Verifies stage progress eligibility (must be in `REFLECT` phase or `` `P(L_t) \ge 0.80` ``).
  2. Finalizes aggregate milestone: marks `StageProgress.Status = "COMPLETED"`.
  3. Synchronizes Roadmap aggregate: updates `RoadmapNode.Status = "COMPLETED"`, sets `QuizScore = P(L_t) * 10.0`, `IsQuizPassed = true`, `CompletedAt = UtcNow`.
  4. Automatic Milestone Unlocking: Locates immediate successor milestone in graph order via `ILearningRoadmapRepository.GetNextLockedNodeAsync` and unlocks it (`Status = "IN_PROGRESS"`).

---

## 8. CORE FLOW 3 - MODULE 2: MISTAKE NOTEBOOK & SPACED REPETITION ENGINE (SM-2)

### 8.1 Domain Aggregate & Schema Design (`practice.MistakeNotebooks`)
- **Table Definition**: `v_eval_practice."MistakeNotebooks"`
  - `id`: UUID Primary Key.
  - `student_id`: UUID, foreign ownership key.
  - `question_id`: UUID, original mistaken question.
  - `skill_id`: UUID, competency domain identifier.
  - `pattern_id`: VARCHAR(100), pedagogical problem pattern (e.g. `PATTERN_DERIVATIVE_01`).
  - `cognitive_error_tag`: VARCHAR(50), pedagogical error categorization (`CARELESS`, `MISREAD_QUESTION`, `MISSING_CONCEPT`).
  - `student_notes`: TEXT, metacognitive self-reflection notes.
  - `next_review_date`: DATE NOT NULL, next scheduled review timestamp (UTC).
  - `review_count`: INT, total review attempts.
  - `consecutive_correct_reviews`: INT, consecutive streak of correct reviews.
  - `interval_days`: INT, current interval spacing (1, 3, 7+ days).
  - `ease_factor`: DOUBLE PRECISION, SuperMemo-2 ease factor (default 2.50).
  - `is_mastered`: BOOLEAN, mastery flag set upon 3 consecutive successful reviews.
  - `last_reviewed_at`: TIMESTAMP WITH TIME ZONE.
  - `created_at`, `updated_at`: Standard temporal audit columns.
- **Index**: `idx_mistake_notebook_daily_review` on `(student_id, next_review_date, is_mastered)` for $O(\log N)$ review queries.

### 8.2 Pedagogical Rule BR-15: Automated Error Capture
- Implemented in `SubmitAnswerCommandHandler.cs`:
  - When an adaptive question attempt at the `APPLY` step produces `isCorrect == false`, the engine automatically intercepts the event.
  - Verifies whether an entry for `(StudentId, QuestionId)` already exists. If absent, an entry is persisted with `NextReviewDate = Tomorrow` (UTC), `ConsecutiveCorrectReviews = 0`, and `IntervalDays = 1`.

### 8.3 SuperMemo-2 (SM-2) Spaced Repetition Mathematical Model
- Implemented in `SpacedRepetitionCalculator.cs`:
  - **Repetition 1 (Correct)**: `` `Interval_1 = 1` `` day, `` `Consecutive = 1` ``.
  - **Repetition 2 (Correct)**: `` `Interval_2 = 3` `` days, `` `Consecutive = 2` ``.
  - **Repetition `` `n \ge 3` `` (Correct)**:
    - `` `Interval_n = \text{round}(Interval_{n-1} \times EaseFactor)` `` days.
    - If `` `Consecutive \ge 3` ``: sets `` `IsMastered = true` ``, permanently retiring the knowledge gap.
  - **Incorrect Review**:
    - Resets `` `Consecutive = 0` ``, resets `` `Interval = 1` `` day.
    - Adjusts ease factor downwards: `` `EaseFactor = \max(1.30, EaseFactor - 0.20)` ``.

### 8.4 Module 2 RESTful Endpoints (`MistakesController`)
1. **`GET /api/practice/mistakes`**:
   - Paginated mistake list with filters (`skillId`, `isMastered`).
   - Computes statistical summary: `totalCount`, `masteredCount`, and `unmasteredCount`.
2. **`GET /api/practice/mistakes/daily-review`**:
   - Queries due review items: `` `next_review_date \le \text{TargetDate} \land \neg is\_mastered` ``.
   - Enriches candidates with Isomorphic Variant questions fetched dynamically from Content Service via gRPC.
3. **`POST /api/practice/mistakes/{id}/tag-error`**:
   - Enables metacognitive self-diagnosis (`CARELESS`, `MISREAD_QUESTION`, `MISSING_CONCEPT`).
   - Validates input with FluentValidation and records personal student notes.
4. **`POST /api/practice/mistakes/{id}/review-submit`**:
   - Validates student answer against variant key, updates SM-2 repetition schedule, and yields next review date.

---

## 9. DYNAMIC CONTENT SERVICE INTEGRATION FOR REMEDIAL NODES (BR-03)

### 9.1 Zero-Hardcoding Content Resolution Architecture
- Remedial questions and their verification keys are completely decoupled from static application code.
- **Dynamic Retrieval (`GetRemedialPackageQueryHandler`)**:
  - Connects to `Content Service` via gRPC HTTP/2 port `5250` (`GetMilestoneQuizAsync`).
  - Filters out previously attempted questions (`attemptedQuestionIds`) based on student's `AdaptiveQuizAttempt` history to prevent rote memorization and answer retention.
  - Dynamically packages the easiest foundational questions ($b < 0.0$, Bloom levels: Remember / Understand).
- **Dynamic Grading & Resolution (`SubmitRemedialCommandHandler`)**:
  - Queries question details and options via `GetQuestionDetailAsync` in `Content Service`.
  - Determines correct option from `Options.FirstOrDefault(o => o.IsCorrect)` and fetches authoritative pedagogical explanation (`Explanation`).
  - Fully eliminates static Guid lookup tables.
- **Concurrency & State Management**:
  - Employs dedicated repository insertion (`_stageProgressRepository.AddAttemptAsync`) preventing EF Core from erroneously marking new attempts as modified in navigation graphs.
  - Requires 100% correct answers (3/3) to transition status back to `IN_PROGRESS` and resume the `APPLY` step.

---

## 10. STRATEGIC MIGRATION: TRANSITION TO OFFLINE DELIVERY & RETIREMENT OF LIVESTREAM APIS

### 10.1 Strategic Rationale
- The educational delivery model transitions from real-time online video streaming to dedicated offline campus lectures and in-person mentorship.
- Real-time online streaming endpoints (`/api/practice/live-sessions`) are formally decommissioned to eliminate dead code and prevent operational overhead.

### 10.2 Decommissioned Components
1. **API Layer**: `LiveSessionsController.cs` and all 7 associated endpoints (`POST /live-sessions`, `GET /my-schedule`, `POST /{sessionId}/join`, `POST /{sessionId}/attendance`, `GET /teacher-schedule`, `PUT /{sessionId}/recording`, `PUT /{sessionId}/cancel`) have been completely removed.
2. **Application Layer**: Cleaned all CQRS commands, queries, and DTOs within `Features/LiveSessions`.
3. **Repository Decoupling**: Refactored `AssignTeacherCommandHandler` to depend on `IClassEnrollmentRepository` rather than `ILiveSessionRepository`. Fully removed `ILiveSessionRepository` and `LiveSessionRepository`.

---

## 11. CORE FLOW 2 UPGRADE: OFFLINE COHORT CAPACITY ENFORCEMENT & MICRO STUDY GROUPING (3 - 5 STUDENTS)

### 11.1 20-Student Cohort Capacity Enforcement & Auto-Incrementing Class Numbering
- **Pedagogical Rationale**: Offline classroom delivery demands bounded cohort sizes to ensure close academic monitoring. A cap of 20 students represents the golden ratio for intensive offline coaching.
- **Implementation (`ClassEnrollmentRepository.EnrollStudentAsync`)**:
  - Imposes strict capacity ceiling `MaxClassCapacity = 20`.
  - Queries active placement cohorts matching the student's assigned ability tier (`FOUNDATION`, `ACCELERATION`, `BREAKTHROUGH`).
  - Computes active enrollment count per cohort (`e.Status == "ENROLLED"`).
  - Automatically provisions new classes when existing cohorts reach 20 students, naming them deterministically with two-digit increments (e.g., `Lớp Nền tảng (Foundation) 01 - Cơ sở Quận 9`, `Lớp Nền tảng (Foundation) 02 - Cơ sở Quận 9`).

### 11.2 Domain Entities & Relational Schema for Micro Study Groups
- **`ClassGroup` Entity**: Represents a micro study group within an offline cohort.
  - Properties: `GroupId`, `ClassId`, `GroupName`, `FocusArea`, `CommonWeakSkillIds`, `RecommendedWorksheetTitle`, `AssignedWorksheetId`, `AssignedWorksheetTitle`, `WorksheetAssignedAt`, `CreatedAt`.
  - Navigation: Belongs to `Class` with cascade deletion; owns multiple `Members` (`ClassGroupMember`).
- **`ClassGroupMember` Entity**: Bridges individual students to their micro-group.
  - Properties: `GroupMemberId`, `GroupId`, `StudentId`, `JoinedAt`.
- **Database Bootstrapping**: Registered in `PracticeDbContext` and bootstrapped via SQL DDL in `Program.cs` under tables `"ClassGroups"` and `"ClassGroupMembers"`.

### 11.3 Constrained Homogeneous Micro-Clustering Engine (`ClassMicroClusterer.cs`)
- **Strict Size Constraint**: Guarantees that every generated micro-group strictly satisfies `` `3 \le \text{Size} \le 5` ``.
- **Mathematical Partitioning**:
  - Valid group count range: `` `\lceil N / 5.0 \rceil \le M \le \lfloor N / 3.0 \rfloor` ``.
  - Resolves target group sizes `` `\text{baseSize} = \lfloor N / M \rfloor` `` with remainder distributed across the first `` `N \bmod M` `` groups.
- **Homogeneous Deficiency Matching**:
  - Encodes student knowledge states via 4-domain vectors combined with granular skill mastery priors (`LearningProfiles.MasteryScore`).
  - Initializes $M$ centroids using K-Means++ and applies a capacitated greedy matching algorithm to group students possessing similar knowledge gaps at the same study table.
  - Profiling: Detects shared deficiency skills (`` `\text{MasteryScore} < 0.60` ``), names the group accordingly (e.g., `"Nhóm 01 - Bàn trọng tâm: [Skill Name]"`), and recommends targeted adaptive worksheets.

### 11.4 REST API Specifications for In-Class Micro-Grouping
1. **Auto-Partition Endpoint**: `POST /api/practice/classes/{classId}/micro-groups/auto-partition`
   - Request: `AutoPartitionMicroGroupsRequestDto` (`PreferredGroupSize`: optional, default 4).
   - Response: `AutoPartitionMicroGroupsResponseDto` (`ClassId`, `ClassName`, `TotalStudents`, `TotalGroups`, `Groups`).
2. **Class Group Roster Query Endpoint**: `GET /api/practice/classes/{classId}/micro-groups`
   - Response: `List<ClassMicroGroupDto>` detailing table layout, member rosters, common weak skills, and assigned worksheets.
3. **Adaptive Worksheet Assignment Endpoint**: `POST /api/practice/classes/{classId}/micro-groups/{groupId}/assign-worksheet`
   - Request: `AssignWorksheetRequestDto` (`WorksheetId`, `WorksheetTitle`).
   - Response: `AssignWorksheetResponseDto` confirming targeted worksheet distribution to table members.
### 11.5 Student Skill Profile Encoding & Integration with Content Service API 19
- **Profile Representation (`StudentSkillProfileItem`)**:
  - `DomainFeatures` (`double[4]`): Macro 4-domain vector representing overall student strength across `[Lang, Math, NatSci, SocSci]`. Used in K-Means distance calculations to place students in compatible learning contexts.
  - `SkillScores` (`Dictionary<Guid, double>`): Micro-level granular mastery scores per competency node from `LearningProfiles.MasteryScore`.
  - **Weak Skill Identification**: Skills where `` `MasteryScore < 0.60` `` across a majority of group members are aggregated into `CommonWeakSkillIds`.
- **Automated Worksheet Generation Handshake with Content Service API 19 (Completed)**:
  - When teacher submits `POST /api/practice/classes/{classId}/micro-groups/{groupId}/assign-worksheet` with empty or omitted `WorksheetId`:
  - `AssignGroupWorksheetCommandHandler` extracts `CommonWeakSkillIds` from the `ClassGroup` record.
  - The handler invokes Content Service via `IContentHttpClient` (`POST /api/content/exams/quiz`), passing the target skill IDs and requesting 5 Bloom L1-L3 questions.
  - Content Service dynamically packages an on-the-fly `MockExam` (`ExamCategory = "QUIZ"`), stores linked `ExamQuestion` items, and returns the generated `ExamId` and `Title`.
  - Practice Service registers the `ExamId` as `AssignedWorksheetId` and assigns it directly to all members of the micro-group.

---

## 12. CORE FLOW 3 (MODULE 3) - PROCTORED MOCK EXAM & ACADEMIC INTEGRITY MONITORING

### 12.1 Relational Schema & Persistence Architecture
- **Reuse of `ExamSubmission`**: Avoids duplicate submission tables by adopting unified classification via `ExamType = "MOCK_EXAM"`.
- **Integrity Tracking Fields**:
  - `TabDepartureCount`: Quantifies student tab / window switches away from fullscreen mode.
  - `IntegrityStatus`: Flagged as `NORMAL` or escalated to `FLAGGED_FOR_REVIEW` when violations exceed threshold (>3 departures).
  - `MentorVerdict`: Workflow authorization status (`PENDING`, `APPROVED`, `DISQUALIFIED`).
  - `MentorNotes`: Mentor justification notes.
- **`ProctoringSnapshot` Entity**:
  - Captures periodic / randomized webcam audit frames (`Id`, `SubmissionId`, `SnapshotUrl`, `CapturedAt`, `FaceDetected`).
  - Cascades on deletion with `ExamSubmission`.

### 12.2 API 1: Start Mock Exam Session (`POST /api/practice/mock-exams/{examId}/start`)
- **Execution Pipeline**:
  1. Validates input parameters (`ExamId`, `StudentId`).
  2. Queries active uncompleted attempts for resuming valid sessions.
  3. Integrates with Content Service via gRPC (`GetMilestoneQuizAsync`) to load 120 secure test items with hidden answer keys.
  4. Enforces graceful fallback generation when external gRPC is unavailable.
  5. Initializes `ExamSubmission` with 150-minute countdown (9,000 seconds), `ExamType = "MOCK_EXAM"`, and default `NORMAL` integrity status.

### 12.3 API 2: Log Tab Departure & Academic Integrity Flagging (`POST /api/practice/mock-exams/{submissionId}/log-tab-departure`)
- **Execution Pipeline**:
  1. Validates that the targeted exam submission exists and remains in `IN_PROGRESS` status.
  2. Increments `TabDepartureCount` on every window blur / tab switch / fullscreen exit.
  3. Enforces Business Rule BR-20: When `TabDepartureCount > 3`, dynamically updates `IntegrityStatus` to `FLAGGED_FOR_REVIEW`.
  4. Returns real-time warning feedback and timestamped audit logs.

### 12.4 API 3: Upload Random Proctoring Snapshot (`POST /api/practice/mock-exams/{submissionId}/upload-snapshot`)
- **Execution Pipeline**:
  1. Validates that the targeted exam submission exists and remains in `IN_PROGRESS` status.
  2. Persists timestamped webcam frame metadata into `ProctoringSnapshots` table (`SnapshotUrl`, `CapturedAt = UtcNow`, `FaceDetected`).
  3. Records anomaly warnings if `FaceDetected` is false (absence or occlusion).
  4. Yields total accumulated snapshots count for student verification.

### 12.5 API 4: Submit Mock Exam, Grade & Integrity Routing (`POST /api/practice/mock-exams/{submissionId}/submit`)
- **Execution Pipeline**:
  1. Validates that the targeted exam submission exists and remains in `IN_PROGRESS` status.
  2. Fetches official answer keys from Content Service via gRPC `GetExamAnswerKeysAsync`, supporting resilient fallback grading in isolated local testing.
  3. Evaluates correctness for each question item, computes total score (120 questions x 10 = 1200 points scale), and logs granular answers into `SubmissionAnswers`.
  4. Applies Proctoring Integrity Policy (BR-20): If flagged (`IntegrityStatus == "FLAGGED_FOR_REVIEW"` or `TabDepartureCount > 3`), sets `Status = "UNDER_REVIEW"` and `MentorVerdict = "PENDING"` for mentor verification; otherwise marks `Status = "COMPLETED"` and `MentorVerdict = "APPROVED"`.
  5. Core Flow 3 Closed-Loop Integration: Automatically injects incorrect answers into the student's personal `MistakeNotebook` with `NextReviewDate = Tomorrow` for spaced repetition (SM-2).
  6. Core Flow 2 Feedback Loop: Aggregates skill-level performance and updates `LearningProfiles` (`UpsertSkillPriorsAsync`) to inform downstream micro-group re-clustering (3-5 students) for next week's offline classes.

### 12.6 API 5: Retrieve Mock Exam Details & Visual Audit Trail (`GET /api/practice/mock-exams/{submissionId}`)
- **Execution Pipeline**:
  1. Validates that the targeted exam submission exists.
  2. Eagerly loads relational aggregates including `SubmissionAnswers` and `ProctoringSnapshots` via `IMockExamRepository.GetByIdWithDetailsAsync`.
  3. Returns high-level scores, integrity telemetry (`TabDepartureCount`, `IntegrityStatus`), and mentor review decisions (`MentorVerdict`, `MentorNotes`).
  4. Exposes chronological webcam visual audit trail (`CapturedAt` ascending order) with facial presence verification indicators (`FaceDetected`) for mentor inspection.
  5. Outputs granular item-by-item student selections and correctness evaluations.

### 12.7 API 6: Mentor Review & Academic Integrity Verdict (`PATCH /api/practice/mock-exams/{submissionId}/mentor-review`)
- **Execution Pipeline**:
  1. Validates that the targeted exam submission exists and has transitioned out of active student testing (`Status != "IN_PROGRESS"`).
  2. Enforces strict input validation: `Verdict` must be either `APPROVED` or `DISQUALIFIED`. If `DISQUALIFIED`, `Notes` is strictly required.
  3. Under `APPROVED` verdict: Updates `Status = "COMPLETED"`, resets suspicious flag to `IntegrityStatus = "NORMAL"`, and legitimizes the earned `TotalScore`.
  4. Under `DISQUALIFIED` verdict: Updates `Status = "DISQUALIFIED"`, voids academic credentials by zeroing out `TotalScore = 0` and `TotalCorrect = 0`.
  5. Persists mentor review rationale into `MentorNotes` and timestamps the decision for audit traceability.

---

## 13. CORE FLOW 4 — GRAPH-RAG SOCRATIC AI TUTOR INTEGRATION ACCEPTANCE (PHASES 5 & 6)

### 13.1 Architecture Overview
- **Inter-Service Role**: Acts as the student-facing gateway and streaming proxy bridge between client web/mobile applications and the downstream Python FastAPI AI Tutor engine (`rag-service` on port 8000).
- **Core Principles**:
  - Zero-blocking streaming: tokens emitted by the Python Socratic engine are piped instantaneously to students via Server-Sent Events (SSE).
  - High resilience: network timeouts, model outages, or rate-limit violations trigger structured pedagogical fallbacks synthesized from Knowledge Graph theorems, preventing broken student sessions.
  - Strict abuse defense: enforces ASP.NET Core 9 Rate Limiting with strict per-student token quotas.

### 13.2 Component Specifications
1. **Application Interface (`IAiTutorClient`)**:
   - `StreamSocraticGuidanceAsync(SocraticAskRequestPayload, CancellationToken)`: Yields asynchronous stream chunks (`IAsyncEnumerable<string>`).
   - `AskSocraticGuidanceAsync(SocraticAskRequestPayload, CancellationToken)`: Direct synchronous request returning `SocraticAskResponseDto`.
2. **Infrastructure Client (`AiTutorClient`)**:
   - Consumes `POST /api/v1/socratic/ask-stream` with `HttpCompletionOption.ResponseHeadersRead`.
   - Parses incoming SSE lines (`data: {"type": "token", "token": "..."}`) with `JsonDocument` and yields tokens dynamically.
   - Graceful fallback: If connection fails, yields verified Socratic heuristic guidance.
3. **API Layer (`AiTutorController` & `Program.cs`)**:
   - `StrictTutorPolicy`: Fixed window rate limiter configured to 10 requests / minute per client (`429 Too Many Requests`).
   - `POST /api/practice/ai-tutor/ask-stream`: Emits `text/event-stream; charset=utf-8` with SSE events, terminating with `data: [DONE]\n\n`.
   - Identity extraction: Seamlessly resolves `StudentId` from Gateway `X-User-Id` header with payload fallback.

### 13.3 Human-in-the-Loop Reporting & Mentor Audit Integration (Phase 6)
- **Student Reporting Action**:
  - `POST /api/practice/ai-tutor/report`: Dispatches student feedback flags to Python FastAPI engine via `ReportInteractionAsync`.
  - Captures optional justification text and flags the record in `v_eval_ai.ai_tutor_interaction_logs` (`student_reported = TRUE`).
- **Mentor Audit Querying**:
  - `GET /api/practice/ai-tutor/interactions`: Fetches paginated interaction logs with `reportedOnly` filtering for mentor review.
- **Fail-Safe Operation**:
  - Downstream communication failures fallback to local logging ensuring the student experience is never disrupted.

---

## 14. CORE FLOW 5 — LEARNING ANALYTICS & CLASS HEATMAP ARCHITECTURE ACCEPTANCE

### 14.1 Architecture Overview & Hybrid Aggregation Strategy
- **Service Responsibility**: High-throughput multi-stakeholder progress monitoring and 2D matrix heatmap visualization across Student, Teacher, Parent, and Campus Director portals.
- **Latency Target**: Sub-300ms response time for dense classroom matrices (up to 30 students x 35 skills), surpassing the initial 3-second requirement.
- **Hybrid Aggregation Engine**:
  - **Fast-Path (Write-Through Micro-update)**: When a student completes an adaptive quiz or submits a proctored mock exam, a lightweight event updates their respective row/cell in `practice.ClassSkillHeatmaps` and invalidates the cached class matrix in Redis (`O(1)` runtime).
  - **Batch-Path (Background Sweep Worker)**: An `IHostedService` (`ClassHeatmapAggregationWorker`) triggers every 15 minutes to compute statistical distributions (Mean `` `\overline{P(L_t)}` ``, sample standard deviation `` `\sigma` ``, struggling rate, and bimodal polarization flags) and persists aggregate snapshots into `practice.ClassHeatmapSummaries`.

### 14.2 Pedagogical Rules & Psychometric Formulations
1. **Three-Tier Warning Threshold**:
   - `CRITICAL_RED`: Triggered if `` `\ge 40\%` `` of students in the class have `` `P(L_t) < 0.50` `` OR class mean `` `\overline{P(L_t)} < 0.50` ``.
   - `WARNING_AMBER`: Triggered if `` `20\% \le` `` struggling rate `` `< 40\%` `` OR `` `0.50 \le \overline{P(L_t)} < 0.70` ``.
   - `SAFE_GREEN`: Achieved when `` `\ge 70\%` `` of students master the skill with `` `P(L_t) \ge 0.70` ``.
2. **Bimodal Polarized Distribution Warning**:
   - When `` `\sigma_s \ge 0.28` `` and struggling rate `` `\ge 30\%` ``, flags `is_bimodal = true` to alert the teacher against uniform lecturing, recommending micro-group partitioning instead.
3. **Parent Exam Readiness Index (`` `R_{\text{exam}}` ``)**:
   - Formulated as: `` `R_{\text{exam}} = 0.50 \cdot S_{\theta} + 0.35 \cdot C_{\text{BKT}} + 0.15 \cdot F_{\text{consistency}}` ``.
   - Blends standardized IRT latent ability, comprehensive BKT skill coverage, and weekly study consistency.
4. **Active Study Day & Anti-Vanity Streak Verification**:
   - Requires tangible study proof (1 completed adaptive practice attempt OR `` `\ge 15` `` minutes video watching OR 1 completed mock exam) to increment daily streak counters.

### 14.3 Phase 1 Database & EF Core Entities Acceptance
1. **Domain Entities Implemented**:
   - [`ClassSkillHeatmap`](../V-Eval-Practice_Service.Domain/Entities/ClassSkillHeatmap.cs): Primary key `Id`, foreign key `ClassId` (cascade delete), `StudentId`, `SkillId`, `DomainCode`, `PltValue`, `MasteryLevel`, `LastAssessmentType`, `UpdatedAt`.
   - [`ClassHeatmapSummary`](../V-Eval-Practice_Service.Domain/Entities/ClassHeatmapSummary.cs): Primary key `Id`, foreign key `ClassId` (cascade delete), `SkillId`, `AveragePlt`, `StrugglingCount`, `TotalStudents`, `StrugglingRate`, `StdDevPlt`, `AlertStatus`, `IsBimodal`, `CalculatedAt`.
   - [`StudentDailyActivity`](../V-Eval-Practice_Service.Domain/Entities/StudentDailyActivity.cs): Primary key `Id`, `StudentId`, `ActivityDate` (`date`), `StudyTimeMinutes`, `PracticeQuestionsSolved`, `VideoMinutesWatched`, `MockExamsTaken`, `IsStreakCounted`, `IsFrozen`.
   - [`StudentReadinessMetric`](../V-Eval-Practice_Service.Domain/Entities/StudentReadinessMetric.cs): Primary key `Id`, unique `StudentId`, `NormalizedTheta`, `SkillCoveragePlt`, `ConsistencyScore`, `ExamReadinessIndex`, `ReadinessTier`, `StudyHoursThisWeek`, `CurrentStreakDays`.
2. **Infrastructure Persistence & Fluent API Mapping**:
   - Verified 4 `DbSet` registrations in [`PracticeDbContext`](../V-Eval-Practice_Service.Infrastructure/Persistence/PracticeDbContext.cs).
   - Unique constraints enforced: `uq_class_student_skill`, `uq_class_skill_summary`, `uq_student_activity_date`, `idx_readiness_student_id`.
   - Explicit schema isolation to `v_eval_practice`.
3. **Automated DDL Provisioning**:
   - Database startup pipeline in [`Program.cs`](../V-Eval-Practice_Service.API/Program.cs) provisions all 4 tables and their indexes dynamically with `CREATE TABLE IF NOT EXISTS`.
   - Standalone migration script maintained in [`SQL_Migration_Flow5_Analytics.sql`](../../docs/SQL/SQL_Migration_Flow5_Analytics.sql).

### 14.4 Phase 2 Hybrid Aggregation Engine & Background Worker Acceptance
1. **Repository Abstractions & Implementations**:
   - [`IClassHeatmapRepository`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IClassHeatmapRepository.cs) & [`ClassHeatmapRepository`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/ClassHeatmapRepository.cs): Multi-criteria heatmap queries, domain filtering, single-cell updates, and batch summary upserts.
   - [`IStudentActivityRepository`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IStudentActivityRepository.cs) & [`StudentActivityRepository`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/StudentActivityRepository.cs): Backward streak tracing algorithm, weekly study hours aggregation, and readiness metric persistence.
2. **Matrix Caching Strategy**:
   - [`IClassHeatmapCacheService`](../V-Eval-Practice_Service.Application/Common/Interfaces/Services/IClassHeatmapCacheService.cs) & [`ClassHeatmapCacheService`](../V-Eval-Practice_Service.Infrastructure/Caching/ClassHeatmapCacheService.cs): High-performance in-memory cache leveraging `IMemoryCache` with concurrent key tracking and class-level cache invalidation (`InvalidateClassHeatmapAsync`).
3. **Core Aggregator Service**:
   - [`IClassHeatmapAggregatorService`](../V-Eval-Practice_Service.Application/Common/Interfaces/Services/IClassHeatmapAggregatorService.cs) & [`ClassHeatmapAggregatorService`](../V-Eval-Practice_Service.Infrastructure/Services/ClassHeatmapAggregatorService.cs):
     - Complete implementation of the psychometric formula: mean ability, struggling rates, standard deviation `` `\sigma` ``, bimodal polarization culling, and 3-tier pedagogical alert generation (`CRITICAL_RED`, `WARNING_AMBER`, `SAFE_GREEN`).
     - Composite Parent Exam Readiness Index: `` `R_{\text{exam}} = 0.50 \cdot S_{\theta} + 0.35 \cdot C_{\text{BKT}} + 0.15 \cdot F_{\text{consistency}}` ``.
     - Active Study Day evaluation logic across adaptive quiz attempts, video lectures, and mock exams.
4. **Periodic 15-Minute Background Worker**:
   - [`ClassHeatmapAggregationWorker`](../V-Eval-Practice_Service.Infrastructure/BackgroundJobs/ClassHeatmapAggregationWorker.cs): Registered `IHostedService` periodically running in the background every 15 minutes across all active classes with graceful cancellation tokens.
5. **Real-time Write-Through Integration**:
   - Integrated into [`SubmitAnswerCommandHandler`](../V-Eval-Practice_Service.Application/Features/Stages/Commands/SubmitAnswer/SubmitAnswerCommandHandler.cs) (step 10), [`SubmitMockExamCommandHandler`](../V-Eval-Practice_Service.Application/Features/MockExams/Commands/SubmitMockExam/SubmitMockExamCommandHandler.cs) (step 7), and [`TrackVideoCommandHandler`](../V-Eval-Practice_Service.Application/Features/Stages/Commands/TrackVideo/TrackVideoCommandHandler.cs) to deliver instant zero-lag updates to student streak and class heatmap.

### 14.5 Phase 3 Flow 1: Student Dashboard API & CQRS Handler Acceptance
1. **REST Controller**:
   - [`AnalyticsController`](../V-Eval-Practice_Service.API/Controllers/AnalyticsController.cs) exposed under route `api/practice/analytics`.
   - Endpoint: `GET /api/practice/analytics/student/{studentId:guid}/dashboard`.
2. **CQRS Architecture**:
   - Query: [`GetStudentDashboardQuery`](../V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetStudentDashboard/GetStudentDashboardQuery.cs).
   - Handler: [`GetStudentDashboardQueryHandler`](../V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetStudentDashboard/GetStudentDashboardQueryHandler.cs).
   - Zero direct DbContext dependency in Application layer; fully abstracted via `IExamSubmissionRepository`, `ILearningRoadmapRepository`, `IStudentActivityRepository`, `IClassHeatmapRepository`, and `IIdentityGrpcClient`.
3. **Core Features Verified**:
   - 4-Domain Radar Competency aggregation from real-time heatmaps.
   - DAG Roadmap milestone completion percentage and current milestone tracking.
   - Comprehensive streak analytics (current streak, longest streak, freeze status, 7-day visual history).
   - Resilient onboarding detection (`needsOnboarding = true`) for new students with insufficient historical data.

### 14.6 Phase 3 Flow 2: Class Heatmap & Force Aggregate Endpoints Acceptance
1. **REST Controller**:
   - Extended [`ClassesController`](../V-Eval-Practice_Service.API/Controllers/ClassesController.cs) under route `api/practice/classes`.
   - Endpoints:
     - `GET /api/practice/classes/{classId:guid}/heatmap` (Class Heatmap Matrix).
     - `POST /api/practice/classes/{classId:guid}/heatmap/force-aggregate` (Manual Re-computation Sweep).
2. **CQRS Architecture**:
   - Query: [`GetClassHeatmapQuery`](../V-Eval-Practice_Service.Application/Features/Classes/Queries/GetClassHeatmap/GetClassHeatmapQuery.cs) & Handler [`GetClassHeatmapQueryHandler`](../V-Eval-Practice_Service.Application/Features/Classes/Queries/GetClassHeatmap/GetClassHeatmapQueryHandler.cs).
   - Command: [`ForceAggregateHeatmapCommand`](../V-Eval-Practice_Service.Application/Features/Classes/Commands/ForceAggregateHeatmap/ForceAggregateHeatmapCommand.cs) & Handler [`ForceAggregateHeatmapCommandHandler`](../V-Eval-Practice_Service.Application/Features/Classes/Commands/ForceAggregateHeatmap/ForceAggregateHeatmapCommandHandler.cs).
3. **Core Features Verified**:
   - Sub-300ms response guarantee via Cache-First architecture backed by `IClassHeatmapCacheService`.
   - Dynamic domain filtering (`domainCode`) and mastery thresholding (`minMastery`).
   - Automated pedagogical alert triggers: flag skills with `` `\text{StrugglingRate} \ge 40\%` `` or `` `\overline{P(L_t)} < 0.50` `` as `CRITICAL_RED`.
   - Bimodal polarization detection (`isBimodal = true`) delivering micro-group pedagogical recommendations.
   - 3-tier student risk segmentation: `HIGH_RISK` (`` `\ge 2` `` weak skills), `MEDIUM_RISK` (1 weak skill), and `SAFE`.
   - Manual override endpoint allowing teachers/administrators to force immediate re-computation and cache refresh.

### 14.7 Phase 3 Flow 3: 1-Click Pedagogical Action Live Session Acceptance
1. **REST Controller**:
   - Exposed endpoint in [`ClassesController`](../V-Eval-Practice_Service.API/Controllers/ClassesController.cs):
     - `POST /api/practice/classes/{classId:guid}/heatmap/trigger-live-session`.
2. **CQRS Architecture**:
   - Command: [`TriggerHeatmapLiveSessionCommand`](../V-Eval-Practice_Service.Application/Features/Classes/Commands/TriggerHeatmapLiveSession/TriggerHeatmapLiveSessionCommand.cs).
   - Handler: [`TriggerHeatmapLiveSessionCommandHandler`](../V-Eval-Practice_Service.Application/Features/Classes/Commands/TriggerHeatmapLiveSession/TriggerHeatmapLiveSessionCommandHandler.cs).
   - Repository Extensions: Added `AddLiveSessionAsync` and `GetNodesBySkillAndStudentIdsAsync` in [`ILearningRoadmapRepository`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/ILearningRoadmapRepository.cs) and [`LearningRoadmapRepository`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/LearningRoadmapRepository.cs).
3. **Core Features Verified**:
   - Dynamic identification of struggling students (`` `P(L_t) < 0.50` ``) directly from class heatmaps.
   - Atomic creation of scheduled `LiveSession` instances linked to the assigned teacher.
   - Automated propagation to active DAG `RoadmapNodes`, binding targeted students to the newly scheduled session without requiring manual page navigation.

### 14.8 Phase 3 Flow 4: Parent Companion Portal Acceptance
1. **REST Controller**:
   - Exposed endpoint in [`AnalyticsController`](../V-Eval-Practice_Service.API/Controllers/AnalyticsController.cs):
     - `GET /api/practice/analytics/parent/{parentId:guid}/children`.
2. **CQRS Architecture**:
   - Query: [`GetParentDashboardQuery`](../V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQuery.cs).
   - Handler: [`GetParentDashboardQueryHandler`](../V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetParentDashboard/GetParentDashboardQueryHandler.cs).
   - Inter-Service Integration: Extended [`IIdentityGrpcClient`](../V-Eval-Practice_Service.Application/Common/Interfaces/IIdentityGrpcClient.cs) and [`IdentityGrpcClient`](../V-Eval-Practice_Service.Infrastructure/GrpcClients/IdentityGrpcClient.cs) with `GetParentStudentsAsync` calling Identity Service gRPC.
3. **Core Features Verified**:
   - Automatic retrieval or just-in-time computation of composite Exam Readiness Metric `` `R_{\text{exam}}` `` with multi-tier classification (`READY`, `NEAR_READY`, `AT_RISK`).
   - Self-study discipline monitoring comparing 7-day verified study hours against the 12-hour baseline.
   - Curated `AiParentAlerts` generating contextual success markers and parental nudges without exposing raw psychometric complexity.

### 14.9 Phase 3 Flow 5: Campus Comparative Heatmap Acceptance
1. **REST Controller**:
   - Exposed endpoint in [`AnalyticsController`](../V-Eval-Practice_Service.API/Controllers/AnalyticsController.cs):
     - `GET /api/practice/analytics/campus/{campusId:guid}/comparative-heatmap`.
2. **CQRS Architecture**:
   - Query: [`GetCampusComparativeHeatmapQuery`](../V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetCampusComparativeHeatmap/GetCampusComparativeHeatmapQuery.cs).
   - Handler: [`GetCampusComparativeHeatmapQueryHandler`](../V-Eval-Practice_Service.Application/Features/Analytics/Queries/GetCampusComparativeHeatmap/GetCampusComparativeHeatmapQueryHandler.cs).
   - Repository Extensions: Added `GetClassesByCampusIdAsync` in [`IClassHeatmapRepository`](../V-Eval-Practice_Service.Application/Common/Interfaces/Repositories/IClassHeatmapRepository.cs) and [`ClassHeatmapRepository`](../V-Eval-Practice_Service.Infrastructure/Persistence/Repositories/ClassHeatmapRepository.cs).
3. **Core Features Verified**:
   - Aggregated campus-wide mastery comparison across 4 foundational domains (`DOM_LANG`, `DOM_MATH`, `DOM_NAT_SCI`, `DOM_SOC_SCI`).
   - Class-by-class vulnerability detection with 3-tier overall risk evaluation (`SAFE`, `MEDIUM_RISK`, `HIGH_RISK`) enabling academic managers to dispatch pedagogical interventions.

### 14.10 Phase 3 Backend Polish & Seed Data Acceptance
1. **Realistic Seed Data Provisioning**:
   - Class `11111111-2222-3333-4444-555555555555` ("Lớp ĐGNL Cơ sở 1 - ĐHQG TP.HCM") with 5 enrolled demo students (`3fa85f64-...` Hoàng, `4fa85f64-...` Trâm, `5fa85f64-...` Kiệt, `6fa85f64-...` My, `7fa85f64-...` Long).
   - 4 representative competence skills across core domains.
   - Purposeful 60% struggling rate (3/5 students with `` `P(L_t) < 0.50` ``) on "Toán logic mệnh đề & suy luận kéo theo" (`88888888-9999-aaaa-bbbb-cccccccccccc`) triggering immediate visual `CRITICAL_RED` alert on Teacher Heatmap.
   - 7 consecutive active study days (`StudentDailyActivities`) and exam readiness metric `` `R_{\text{exam}} = 82.4` `` (`READY`) with 12-day current streak.
2. **Inter-Service gRPC Contract Fulfillment**:
   - Identity Service gRPC server now implements `GetParentStudents` and `LinkParentStudent` directly backed by `ParentStudentRelations`, `Students`, and `Users` entities, eliminating client-side fallback necessity.


---

## 15. CORE FLOW 6 — SCORE PREDICTION & EARLY WARNING ACCEPTANCE (PHASE 1)

### 14.1 Architectural Overview
- **Domain Responsibilities**: Provides continuous standardized score forecasting with Standard Error of Measurement (SEM 95% confidence intervals), score deficit balancing, and Greedy Knapsack Remedial Prescription optimization.
- **Key Entities**:
  - `ScorePrediction`: Maps to table `v_eval_practice."ScorePredictions"`. Stores `PredictedScore`, `PredictedScoreMin`, `PredictedScoreMax`, `SemScore`, `TargetScore`, `DeficitScore`, and JSONB `PrescriptionItems`.
  - `SystemAlert`: Maps to table `v_eval_practice."SystemAlerts"` for 3-tier disciplinary and academic early warnings (BR-04).
- **Core Principles**:
  - Noise Cancellation: Confidence interval $[Min, Max]$ calculated via psychometric SEM prevents student panic from single-quiz deviations.
  - Micro-Prescription Layer: Selects top 2–3 question patterns optimizing Return On Investment ($ROI = \Delta Score / StudyHoursNeeded$) without restructuring the entire macro learning roadmap.

### 14.2 API 1 Implementation Specification
- **Endpoint**: `GET /api/practice/predictions/my-forecast`
- **Controller**: `PredictionsController.cs`
- **CQRS Handler**: `GetMyForecastQueryHandler.cs`
- **Data Flow**:
  1. Resolves `StudentId` from Gateway `X-User-Id` header with query parameter fallback.
  2. Queries `IScorePredictionRepository.GetLatestByStudentIdAsync`.
  3. Deserializes `PrescriptionItems` JSONB using `JsonNamingPolicy.SnakeCaseLower`.
  4. Classifies balance status (`ON_TRACK` for deficit $\le 0$, `AT_RISK` for $\le 30$, `CRITICAL` for $> 30$).
  5. Returns structured JSON containing forecast interval, deficit score, and prioritized remedial item list.
- **Acceptance Status**: 100% verified against PostgreSQL database and YARP API Gateway proxy.

### 14.3 API 2 Implementation Specification (Remedial Practice Session)
- **Endpoint**: `GET /api/practice/predictions/prescriptions/{patternId}/practice-session`
- **Controller**: `PredictionsController.cs`
- **CQRS Handler**: `GetPrescriptionSessionQueryHandler.cs`
- **Data Flow**:
  1. Validates `patternId` against student's active prescription items in `ScorePredictions`.
  2. If pattern not found or already remedied, returns RFC 7807 404 NotFound.
  3. Dispatches gRPC request to Content Service (`GetMilestoneQuizAsync`) requesting 3–5 isomorphic items for `SkillId`.
  4. Strips correct answer keys from question options to preserve test integrity.
  5. Implements resilient fallback generator (`GenerateFallbackQuestions`) ensuring the practice session remains accessible even during downstream service restarts.
- **Acceptance Status**: 100% verified live via unit endpoints and YARP API Gateway proxy.

### 14.4 API 3 Implementation Specification (Remedial Practice Submission & Deficit Recovery)
- **Endpoint**: `POST /api/practice/predictions/prescriptions/{patternId}/submit-session`
- **Controller**: `PredictionsController.cs`
- **CQRS Command & Handler**: `SubmitPrescriptionSessionCommand.cs` & `SubmitPrescriptionSessionCommandHandler.cs`
- **Data Flow**:
  1. Resolves `StudentId` from Gateway `X-User-Id` header (or query param).
  2. Validates `patternId` exists in student's latest active prescription record in `ScorePredictions`.
  3. Evaluates submitted answers against known key dictionary and isomorphic test schemas.
  4. Computes accuracy rate. If accuracy $\ge 67\%$:
     - Marks the prescription item as remedied (`is_remedied = true`).
     - Upgrades Bayesian Knowledge Tracing mastery posterior $P(L_t)$.
     - Decrements the student's `deficit_score` by `targetPrescription.DeltaScore`.
     - Serializes updated `PrescriptionItems` JSONB and persists to PostgreSQL.
  5. If accuracy $< 67\%$: Retains current deficit and returns constructive feedback advising review and retry.
- **Acceptance Status**: 100% verified live with successful deficit decrement (Happy Case) and strict threshold rejection (Unhappy Case).

### 14.5 API 4 Implementation Specification (Forecast & Prescription Recalculation Engine)
- **Endpoint**: `POST /api/practice/predictions/recalculate`
- **Controller**: `PredictionsController.cs`
- **CQRS Command & Handler**: `RecalculatePredictionCommand.cs` & `RecalculatePredictionCommandHandler.cs`
- **Data Flow**:
  1. Resolves `StudentId` from Gateway `X-User-Id` header or request body.
  2. Queries existing prediction history and extracts current target score (defaults to 850).
  3. Re-estimates ability level taking into account recent stage progressions, remedied prescription items, or mock exam submissions.
  4. Calculates 95% confidence interval $[Min, Max]$ via Standard Error of Measurement ($SEM = 24.5$).
  5. Computes new deficit against target score and classifies status (`ON_TRACK`, `AT_RISK`, `CRITICAL`).
  6. Re-executes the Knapsack ROI selection engine, prioritizing top 2–3 question patterns that yield highest score gain per study hour ($ROI = \Delta Score / H$).
  7. Inserts new `ScorePrediction` audit record into PostgreSQL `v_eval_practice."ScorePredictions"`.
- **Acceptance Status**: 100% verified live via unit endpoints and Gateway proxy.

### 14.6 API 5 Implementation Specification (Personal Early Warnings Query)
- **Endpoint**: `GET /api/practice/early-warnings/my-alerts`
- **Controller**: `EarlyWarningsController.cs`
- **CQRS Query & Handler**: `GetMyAlertsQuery.cs` & `GetMyAlertsQueryHandler.cs`
- **Data Flow**:
  1. Resolves `StudentId` from Gateway `X-User-Id` header with query parameter fallback.
  2. Queries `ISystemAlertRepository.GetByStudentIdAsync` to fetch all alerts for the student.
  3. Applies client-specified filtering: `unreadOnly` (boolean) and `alertType` (`YELLOW`, `RED`, `UNREALISTIC_GOAL`, `GREEN`).
  4. Computes invariant high-level summary counters: `TotalAlerts`, `UnreadCount`, `RedAlertsCount`, `YellowAlertsCount`.
  5. Maps domain entities to `SystemAlertDto` and returns a comprehensive `MyAlertsResponseDto`.
- **Acceptance Status**: 100% verified live with full support for unread/read flags, alert category segmentation, and Mentor intervention metadata.

### 14.7 API 6 Implementation Specification (Mark Alert As Read)
- **Endpoint**: `PATCH /api/practice/early-warnings/{alertId}/read`
- **Controller**: `EarlyWarningsController.cs`
- **CQRS Command & Handler**: `MarkAlertAsReadCommand.cs` & `MarkAlertAsReadCommandHandler.cs`
- **Data Flow**:
  1. Resolves `StudentId` from Gateway `X-User-Id` header with query parameter fallback.
  2. Queries alert record by `alertId` from `ISystemAlertRepository.GetByIdAsync`.
  3. Enforces multi-tenant ownership guard: returns RFC 7807 403 Forbidden if the requesting student attempts to update another student's notification.
  4. Flips `IsRead = true` and updates database record via `ISystemAlertRepository.UpdateAsync`.
  5. Returns idempotent confirmation payload containing updated `AlertId`, `IsRead: true`, and `ReadAt` timestamp.
- **Acceptance Status**: 100% verified live with validation handling, ownership security checks, and badge synchronization.

### 14.8 API 7 Implementation Specification (Mentor Early Warnings Dashboard)
- **Endpoint**: `GET /api/practice/early-warnings/dashboard`
- **Controller**: `EarlyWarningsController.cs`
- **CQRS Query & Handler**: `GetAlertsDashboardQuery.cs` & `GetAlertsDashboardQueryHandler.cs`
- **Data Flow**:
  1. Resolves `MentorId` from Gateway `X-User-Id` header and optional `classId` query parameter.
  2. Queries class context and associated student enrollments via `IClassEnrollmentRepository`.
  3. Queries alerts via `ISystemAlertRepository.GetAlertsByClassAsync` (or system-wide via `GetAllAlertsAsync`).
  4. Applies status filter: active pending cases (`isResolved = false`), resolved cases (`isResolved = true`), or all cases (`isResolved = null`), along with `alertType` categorization.
  5. Enriches each student alert with real-time ability projections: latest `PredictedScore`, `TargetScore`, and `DeficitScore` from `IScorePredictionRepository`.
  6. Computes aggregate KPI counters: `TotalAlerts`, `ActiveAlertsCount`, `RedAlertsCount`, `YellowAlertsCount`, `UnrealisticGoalCount`, `ResolvedAlertsCount`.
- **Acceptance Status**: 100% verified live via unit endpoints and Gateway proxy.

### 14.9 API 8 Implementation Specification (Mentor Pedagogical Intervention Resolution)
- **Endpoint**: `POST /api/practice/early-warnings/{alertId}/resolve`
- **Controller**: `EarlyWarningsController.cs`
- **CQRS Command & Handler**: `ResolveAlertCommand.cs` & `ResolveAlertCommandHandler.cs`
- **Data Flow**:
  1. Resolves `MentorId` from Gateway `X-User-Id` header or request body.
  2. Validates `ResolutionNotes` payload is provided and non-empty.
  3. Queries target alert record via `ISystemAlertRepository.GetByIdAsync`.
  4. Updates record: assigns `HandledBy = MentorId`, `ResolutionNotes`, stamps `ResolvedAt = DateTime.UtcNow`, and marks `IsRead = true`.
  5. Persists update to PostgreSQL via `ISystemAlertRepository.UpdateAsync`.
  6. Returns structured audit response confirming closure of the intervention ticket.
- **Acceptance Status**: 100% verified live with input validation and audit logging.

### 14.10 API 9 Implementation Specification (Tripartite Counseling Scheduling for Unrealistic Goals)
- **Endpoint**: `POST /api/practice/early-warnings/{alertId}/schedule-counseling`
- **Controller**: `EarlyWarningsController.cs`
- **CQRS Command & Handler**: `ScheduleCounselingCommand.cs` & `ScheduleCounselingCommandHandler.cs`
- **Data Flow**:
  1. Resolves `MentorId` from Gateway `X-User-Id` header or request body (`mentor_id`).
  2. Validates request parameters: ensures `counseling_date` is scheduled in the future, and `proposed_target_score` is within standard V-Eval examination range (0–1200 points).
  3. Queries target alert record by `alertId` from `ISystemAlertRepository.GetByIdAsync`.
  4. Formulates a tripartite meeting record (Parent - Student - Mentor) containing location, proposed feasible score, and pedagogical guidance notes.
  5. Marks the alert status as resolved (`IsResolved = true`, `HandledBy = MentorId`, `ResolvedAt = DateTime.UtcNow`, `IsRead = true`).
  6. Updates PostgreSQL database via `ISystemAlertRepository.UpdateAsync`.
  7. Returns confirmation payload containing meeting coordinates, proposed score, and confirmation status `SCHEDULED`.
- **Acceptance Status**: 100% verified live with RFC 7807 validation (future date checks, score range constraints, unauthorized mentor fallback, and not-found guards).






