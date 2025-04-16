using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneTimeLink.Core.Services;
using OneTimeLink.Samples.Web.Data;
using OneTimeLink.Samples.Web.Models;

namespace OneTimeLink.Samples.Web.Controllers;

public class EmailVerificationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILinkService _linkService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailVerificationController> _logger;

    public EmailVerificationController(
        ApplicationDbContext context,
        ILinkService linkService,
        IEmailSender emailSender,
        ILogger<EmailVerificationController> logger)
    {
        _context = context;
        _linkService = linkService;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _context.Users.ToListAsync();
        return View(users);
    }

    [HttpGet]
    public IActionResult SendVerification()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SendVerification(SendVerificationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            // For security, don't reveal that the user doesn't exist
            _logger.LogWarning("Verification attempted for non-existent email: {Email}", model.Email);
            return RedirectToAction(nameof(VerificationSent));
        }

        if (user.IsEmailVerified)
        {
            ModelState.AddModelError("", "This email is already verified.");
            return View(model);
        }

        // Generate a verification link using the one-time link library
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var verificationLink = await _linkService.GenerateLinkAsync(
            $"{baseUrl}/EmailVerification/Verify",
            user.Id.ToString(),
            "email-verification",
            TimeSpan.FromDays(7) // Links valid for 7 days
        );

        // Send verification email
        var emailBody = $@"
            <h1>Verify your email address</h1>
            <p>Please click the link below to verify your email address:</p>
            <p><a href='{verificationLink}'>Verify Email</a></p>
            <p>This link will expire in 7 days.</p>
        ";

        await _emailSender.SendEmailAsync(
            user.Email,
            "Verify your email address",
            emailBody
        );

        return RedirectToAction(nameof(VerificationSent));
    }

    [HttpGet]
    public IActionResult VerificationSent()
    {
        return View();
    }

    [HttpGet("EmailVerification/Verify/{token}")]
    public async Task<IActionResult> Verify(string token)
    {
        // Validate and use the one-time link
        var link = await _linkService.ValidateAndUseLinkAsync(token);
        if (link == null)
        {
            return View("VerificationFailed");
        }

        // Check if this is an email verification link
        if (link.Purpose != "email-verification")
        {
            _logger.LogWarning("Token used for wrong purpose: {Purpose}", link.Purpose);
            return View("VerificationFailed");
        }

        // Find the user
        if (!Guid.TryParse(link.UserId, out var userId))
        {
            _logger.LogError("Invalid user ID in link: {UserId}", link.UserId);
            return View("VerificationFailed");
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogError("User not found for ID: {UserId}", userId);
            return View("VerificationFailed");
        }

        // Mark the user's email as verified
        user.IsEmailVerified = true;
        await _context.SaveChangesAsync();

        return View("VerificationSuccessful");
    }
}