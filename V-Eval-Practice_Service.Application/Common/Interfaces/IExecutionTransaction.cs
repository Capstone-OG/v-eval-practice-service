using System;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces;

/// <summary>
/// Giao diện trừu tượng hóa Database Transaction (ACID) cho tầng Application
/// </summary>
public interface IExecutionTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

