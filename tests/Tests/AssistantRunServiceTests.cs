using Core.Entities;
using Core.Services;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests;

public class AssistantRunServiceTests
{
    private readonly Mock<IAssistantRunRepository> _repository = new();
    private readonly Mock<IRepoAssistantService> _repoAssistant = new();
    private readonly AssistantRunService _service;

    public AssistantRunServiceTests()
    {
        _service = new AssistantRunService(
            _repository.Object,
            _repoAssistant.Object,
            NullLogger<AssistantRunService>.Instance
        );
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsRunWithSubmittedStatus()
    {
        _repository
            .Setup(r => r.AddAsync(It.IsAny<AssistantRun>(), default))
            .ReturnsAsync((AssistantRun r, CancellationToken _) => r);

        var run = await _service.CreateAsync("my goal", null, null);

        run.Status.Should().Be(AssistantRunStatus.Submitted);
        run.UserGoal.Should().Be("my goal");
        _repository.Verify(r => r.AddAsync(It.IsAny<AssistantRun>(), default), Times.Once);
        _repository.Verify(
            r => r.AddEventAsync(It.Is<AssistantRunEvent>(e => e.Action == "Created"), default),
            Times.Once
        );
    }

    // ── GenerateDraft ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AssistantRunStatus.Submitted)]
    [InlineData(AssistantRunStatus.Rejected)]
    public async Task GenerateDraftAsync_TransitionsToNeedsHumanReview_FromAllowedStatus(
        AssistantRunStatus startStatus
    )
    {
        var run = RunInStatus(startStatus);
        SetupGetById(run);
        SetupDraftGeneration(run, "draft text");

        var result = await _service.GenerateDraftAsync(run.Id);

        result.Status.Should().Be(AssistantRunStatus.NeedsHumanReview);
        result.GeneratedDraft.Should().Be("draft text");
    }

    [Theory]
    [InlineData(AssistantRunStatus.NeedsHumanReview)]
    [InlineData(AssistantRunStatus.Approved)]
    public async Task GenerateDraftAsync_ThrowsWhenStatusIsInvalid(AssistantRunStatus status)
    {
        var run = RunInStatus(status);
        SetupGetById(run);

        await _service
            .Invoking(s => s.GenerateDraftAsync(run.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAsync_TransitionsToApprovedAndSetsFinalOutput()
    {
        var run = RunInStatus(AssistantRunStatus.NeedsHumanReview, draft: "the draft");
        SetupGetById(run);

        var result = await _service.ApproveAsync(run.Id);

        result.Status.Should().Be(AssistantRunStatus.Approved);
        result.FinalOutput.Should().Be("the draft");
    }

    [Theory]
    [InlineData(AssistantRunStatus.Submitted)]
    [InlineData(AssistantRunStatus.Approved)]
    [InlineData(AssistantRunStatus.Rejected)]
    public async Task ApproveAsync_ThrowsWhenStatusIsInvalid(AssistantRunStatus status)
    {
        var run = RunInStatus(status);
        SetupGetById(run);

        await _service
            .Invoking(s => s.ApproveAsync(run.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RejectAsync_TransitionsToRejectedWithReason()
    {
        var run = RunInStatus(AssistantRunStatus.NeedsHumanReview);
        SetupGetById(run);

        var result = await _service.RejectAsync(run.Id, "not good enough");

        result.Status.Should().Be(AssistantRunStatus.Rejected);
        _repository.Verify(
            r =>
                r.AddEventAsync(
                    It.Is<AssistantRunEvent>(e =>
                        e.Action == "Rejected" && e.Detail == "not good enough"
                    ),
                    default
                ),
            Times.Once
        );
    }

    [Theory]
    [InlineData(AssistantRunStatus.Submitted)]
    [InlineData(AssistantRunStatus.Approved)]
    public async Task RejectAsync_ThrowsWhenStatusIsInvalid(AssistantRunStatus status)
    {
        var run = RunInStatus(status);
        SetupGetById(run);

        await _service
            .Invoking(s => s.RejectAsync(run.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    // ── EditAndApprove ────────────────────────────────────────────────────────

    [Fact]
    public async Task EditAndApproveAsync_SetsFinalOutputToEditedText()
    {
        var run = RunInStatus(AssistantRunStatus.NeedsHumanReview, draft: "original draft");
        SetupGetById(run);

        var result = await _service.EditAndApproveAsync(run.Id, "edited output");

        result.Status.Should().Be(AssistantRunStatus.Approved);
        result.FinalOutput.Should().Be("edited output");
    }

    [Theory]
    [InlineData(AssistantRunStatus.Submitted)]
    [InlineData(AssistantRunStatus.Approved)]
    public async Task EditAndApproveAsync_ThrowsWhenStatusIsInvalid(AssistantRunStatus status)
    {
        var run = RunInStatus(status);
        SetupGetById(run);

        await _service
            .Invoking(s => s.EditAndApproveAsync(run.Id, "edit"))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    // ── Regenerate ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AssistantRunStatus.NeedsHumanReview)]
    [InlineData(AssistantRunStatus.Rejected)]
    public async Task RegenerateAsync_ProducesNewDraftFromAllowedStatus(
        AssistantRunStatus startStatus
    )
    {
        var run = RunInStatus(startStatus, draft: "old draft");
        SetupGetById(run);
        SetupDraftGeneration(run, "new draft");

        var result = await _service.RegenerateAsync(run.Id);

        result.Status.Should().Be(AssistantRunStatus.NeedsHumanReview);
        result.GeneratedDraft.Should().Be("new draft");
    }

    [Theory]
    [InlineData(AssistantRunStatus.Submitted)]
    [InlineData(AssistantRunStatus.Approved)]
    public async Task RegenerateAsync_ThrowsWhenStatusIsInvalid(AssistantRunStatus status)
    {
        var run = RunInStatus(status);
        SetupGetById(run);

        await _service
            .Invoking(s => s.RegenerateAsync(run.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((AssistantRun?)null);

        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListRecentAsync_DelegatesToRepository()
    {
        var expected = new List<AssistantRun> { new() { UserGoal = "g" } };
        _repository.Setup(r => r.ListRecentAsync(5, default)).ReturnsAsync(expected);

        var result = await _service.ListRecentAsync(5);

        result.Should().BeSameAs(expected);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AssistantRun RunInStatus(AssistantRunStatus status, string? draft = null) =>
        new()
        {
            UserGoal = "goal",
            Status = status,
            GeneratedDraft = draft,
        };

    private void SetupGetById(AssistantRun run)
    {
        _repository.Setup(r => r.GetByIdAsync(run.Id, default)).ReturnsAsync(run);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<AssistantRun>(), default))
            .Returns(Task.CompletedTask);
        _repository
            .Setup(r => r.AddEventAsync(It.IsAny<AssistantRunEvent>(), default))
            .Returns(Task.CompletedTask);
    }

    private void SetupDraftGeneration(AssistantRun run, string draft) =>
        _repoAssistant
            .Setup(s =>
                s.RunAsync(
                    It.IsAny<string>(),
                    run.UserGoal,
                    run.FileContext,
                    run.ProjectArea,
                    default
                )
            )
            .ReturnsAsync(new PromptRunResult(draft, "test0000"));
}
