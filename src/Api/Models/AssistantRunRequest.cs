namespace Api.Models;

public record AssistantRunRequest(
    string UserGoal,
    string? ProjectArea,
    string? FileContext,
    string? TemplateName = null
);

public record RejectRequest(string? Reason);

public record EditAndApproveRequest(string EditedOutput);
