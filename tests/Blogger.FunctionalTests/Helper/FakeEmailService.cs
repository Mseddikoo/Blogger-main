using Blogger.Application.ApplicationServices;

namespace Blogger.FunctionalTests.Helper;

/// <summary>
/// Fake email service for functional tests to avoid external SMTP dependencies.
/// </summary>
public class FakeEmailService : IEmailService
{
    public Task SendAsync(string email, string subject, string content, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
