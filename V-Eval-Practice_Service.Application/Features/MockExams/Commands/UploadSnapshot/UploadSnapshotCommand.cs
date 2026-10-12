using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.UploadSnapshot;

public record UploadSnapshotCommand(
    Guid SubmissionId,
    string SnapshotUrl,
    bool FaceDetected = true
) : IRequest<Result<UploadSnapshotResponseDto>>;
