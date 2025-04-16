namespace OneTimeLink.Samples.Web;

public class MockEmailSender : IEmailSender
{
    private readonly ILogger<MockEmailSender> _logger;
    
    public MockEmailSender(ILogger<MockEmailSender> logger)
    {
        _logger = logger;
    }
    
    public Task SendEmailAsync(string email, string subject, string message)
    {
        _logger.LogInformation(
            "Email would be sent to {Email} with subject '{Subject}' and message: {Message}", 
            email, 
            subject, 
            message);
        
        return Task.CompletedTask;
    }
}