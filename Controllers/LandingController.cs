using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PWD_DSWD_SPC.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using PWD_DSWD_SPC;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace PWD_DSWD.Controllers
{

    public class LandingController : Controller
    {
        private readonly RegisterDbContext _registerDbContext;
        private readonly string _adminUsername;
        private readonly string _adminPassword;
        private readonly IEmailService _emailService;

        // Constructor to initialize RegisterDbContext
        public LandingController(RegisterDbContext registerDbContext, IConfiguration configuration, IEmailService emailService)
        {
            _registerDbContext = registerDbContext ?? throw new ArgumentNullException(nameof(registerDbContext));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));

        }

        // GET: Landing
        public ActionResult Landing()
        {
            return View();
        }

        public ActionResult Apply()
        {
            return View();
        }

        public ActionResult Login()
        {
            return View();
        }


        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Validate input
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Username and password are required.");
                return View();
            }

            // Check default admin credentials
            if (username == "Admin" && password == "Admin123")
            {
                await SignInUser("Admin", "Admin", Guid.Empty);
                return RedirectToAction("Admin", "Admin");
            }

            //// Attempt to authenticate an admin
            //var admin = _registerDbContext.AdminCredential.SingleOrDefault(a => a.Username == username);
            //if (admin != null && VerifyPassword(password, admin.Password))
            //{
            //    await SignInUser(admin.Username, "Admin", Guid.Empty);
            //    return RedirectToAction("Admin", "Admin");
            //}

            // Attempt to authenticate a user
            var user = _registerDbContext.UserCredential
                .Include(u => u.Accounts)
                .SingleOrDefault(u => u.Username == username);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View();
            }

            // Check for account expiration
            if (user.Accounts != null && user.Accounts.IsExpired)
            {
                ModelState.AddModelError("", "Your account is expired. Please renew your account.");
                return View();
            }

            // Verify password
            if (VerifyPassword(password, user.Password))
            {
                // Ensure the AccountId is available
                var accountId = user.Accounts?.Id ?? Guid.Empty;

                await SignInUser(user.Username, user.Role, accountId);

                if (user.Role == "Client")
                {
                    return RedirectToAction("UserDash", "User");
                }
                else
                {
                    return RedirectToAction("Login", "Landing");
                }
            }


            // Invalid login attempt
            ModelState.AddModelError("", "Invalid login attempt.");
            return View();
        }

        private async Task SignInUser(string username, string role, Guid accountId)
        {
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim("AccountId", accountId.ToString()) // Add the AccountId claim
        };

            var identity = new ClaimsIdentity(claims, "CookieAuth");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("CookieAuth", principal);
        }


        // Password verification
        private bool VerifyPassword(string inputPassword, string storedPasswordHash)
        {
            return BCrypt.Net.BCrypt.Verify(inputPassword, storedPasswordHash);
        }



        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                TempData["Error"] = "Username is required.";
                return RedirectToAction("Login");
            }

            // Find user by username
            var user = _registerDbContext.UserCredential
                .Include(u => u.Accounts)
                .FirstOrDefault(u => u.Username == username);

            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Generate a new password
            var newPassword = GenerateRandomPassword(); // 8-character password
            user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword); // Hash the new password

            // Save changes to the database
            await _registerDbContext.SaveChangesAsync();

            // Send email to the user's associated email
            var userEmail = user.Accounts?.EmailAddress; // Assuming Accounts has an Email property
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["Error"] = "Unable to send email. No registered email found.";
                return RedirectToAction("Login");
            }

            await _emailService.SendForgotPasswordEmailAsync(userEmail, newPassword);

            TempData["Message"] = "A new password has been sent to your registered email.";
            return RedirectToAction("Login");
        }

        // Auto Generation of Password
        private string GenerateRandomPassword()
        {
            // Generate a random password
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }


        // Track application logic remains the same
        [HttpPost]
        public JsonResult TrackApplication(string referenceNumber)
        {
            var application = _registerDbContext.Accounts
                .Include(a => a.Status)
                .Where(a => a.ReferenceNumber == referenceNumber)
                .Select(a => new
                {
                    Requirement1 = a.Status.Requirement1 ? "Approved" : "Pending",
                    Requirement2 = a.Status.Requirement2 ? "Approved" : "Pending",
                    Requirement3 = a.Status.Requirement3 ? "Approved" : "Pending",
                    Requirement4 = a.Status.Requirement4 ? "Approved" : "Pending",
                    Status = a.Status.Status
                })
                .FirstOrDefault();

            if (application != null)
            {
                return Json(new { success = true, approvalStatus = application });
            }
            else
            {
                return Json(new { success = false, message = "Application not found." });
            }
        }

        // Logout
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}