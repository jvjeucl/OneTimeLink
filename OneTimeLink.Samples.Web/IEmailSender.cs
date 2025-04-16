namespace OneTimeLink.Samples.Web;

public interface IEmailSender
{
    Task SendEmailAsync(string email, string subject, string message);
}