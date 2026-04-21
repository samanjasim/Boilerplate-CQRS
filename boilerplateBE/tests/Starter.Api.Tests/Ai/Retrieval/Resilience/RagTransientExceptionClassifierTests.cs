using System.Data.Common;
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagTransientExceptionClassifierTests
{
    [Theory]
    [InlineData(typeof(System.Net.Http.HttpRequestException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(TaskCanceledException))]
    public void Transient_framework_exceptions_are_classified_as_transient(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType)!;
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void Grpc_RpcException_is_transient()
    {
        var ex = new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "down"));
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void DbException_subclass_is_transient()
    {
        var ex = new FakeDbException();
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(NullReferenceException))]
    public void Programmer_bugs_are_not_transient(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType)!;
        RagTransientExceptionClassifier.IsTransient(ex).Should().BeFalse();
    }

    private sealed class FakeDbException : DbException
    {
        public FakeDbException() : base("fake") { }
    }
}
