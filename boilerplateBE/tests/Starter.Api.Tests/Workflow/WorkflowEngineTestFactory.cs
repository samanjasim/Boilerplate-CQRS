using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;

namespace Starter.Api.Tests.Workflow;

/// <summary>
/// Central fixture builder for <see cref="WorkflowEngine"/>-backed tests.
/// Seven test classes construct the engine with the same dependency graph
/// (mock user reader, null-object comment/webhook/notification, real
/// HumanTaskFactory/ParallelApprovalCoordinator, condition/auto-transition
/// evaluators). Centralizing that wiring keeps individual tests focused on
/// arrange/act/assert instead of DI setup, and means adding a new engine
/// dependency only touches one file.
/// </summary>
internal static class WorkflowEngineTestFactory
{
    public static WorkflowDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WorkflowDbContext(options);
    }

    /// <summary>
    /// Builds a <see cref="WorkflowEngine"/> around <paramref name="db"/> with
    /// default mocks for every external dependency. The returned
    /// <see cref="Fixture"/> exposes the mocks so tests can set up specific
    /// interactions (e.g. <c>userReader.Setup(...)</c>) before the act phase.
    /// </summary>
    public static Fixture Build(
        WorkflowDbContext db,
        IEnumerable<IAssigneeResolverProvider>? extraProviders = null)
    {
        var userReader = new Mock<IUserReader>();
        var providers = new List<IAssigneeResolverProvider>
        {
            new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>()),
        };
        if (extraProviders is not null) providers.AddRange(extraProviders);

        var assigneeResolver = new AssigneeResolverService(
            providers,
            userReader.Object,
            NullLogger<AssigneeResolverService>.Instance);

        var hookExecutor = new HookExecutor(
            Mock.Of<IMessageDispatcher>(),
            Mock.Of<IActivityService>(),
            Mock.Of<IWebhookPublisher>(),
            Mock.Of<INotificationServiceCapability>(),
            NullLogger<HookExecutor>.Instance);

        var humanTaskFactory = new HumanTaskFactory(db, assigneeResolver);
        var conditionEvaluator = new ConditionEvaluator(NullLogger<ConditionEvaluator>.Instance);
        var autoTransitionEvaluator = new AutoTransitionEvaluator(conditionEvaluator);
        var parallelCoordinator = new ParallelApprovalCoordinator(db);

        var engine = new WorkflowEngine(
            db,
            hookExecutor,
            Mock.Of<ICommentService>(),
            userReader.Object,
            new FormDataValidator(),
            humanTaskFactory,
            autoTransitionEvaluator,
            parallelCoordinator,
            NullLogger<WorkflowEngine>.Instance);

        return new Fixture(engine, userReader);
    }

    public sealed record Fixture(WorkflowEngine Engine, Mock<IUserReader> UserReader);
}
