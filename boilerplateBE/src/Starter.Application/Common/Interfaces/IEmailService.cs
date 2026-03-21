using Starter.Application.Common.Models;

namespace Starter.Application.Common.Interfaces;

public interface IEmailService
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
