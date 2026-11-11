using System;
using System.Collections.Generic;
using System.Linq;

namespace V_Eval_Practice_Service.Application.Common.Models;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public IReadOnlyList<Error> Errors { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failure result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
        Errors = error == Error.None ? Array.Empty<Error>() : new[] { error };
    }

    protected Result(bool isSuccess, IEnumerable<Error> errors)
    {
        var errorList = errors.ToList();
        IsSuccess = isSuccess;
        Error = errorList.FirstOrDefault() ?? Error.None;
        Errors = errorList.AsReadOnly();
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(IEnumerable<Error> errors) => new(false, errors);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);
    public static Result<TValue> Failure<TValue>(IEnumerable<Error> errors) => Result<TValue>.Failure(errors);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    protected internal Result(TValue? value, bool isSuccess, IEnumerable<Error> errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result cannot be accessed.");

    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);
    public new static Result<TValue> Failure(Error error) => new(default, false, error);
    public new static Result<TValue> Failure(IEnumerable<Error> errors) => new(default, false, errors);

    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure(Error.NullValue);
}
