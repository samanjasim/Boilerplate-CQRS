namespace Starter.Application.Common.Models;

public record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsHtml = true);
