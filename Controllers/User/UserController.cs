using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PWD_DSWD_SPC.Data;
using PWD_DSWD_SPC.Models.Registered;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // To handle JSON objects
using System;
using System.Collections.Generic;
using BCrypt.Net; // Include this for BCrypt.Net.BCrypt
using Microsoft.Extensions.Logging; // For logging
using PWD_DSWD_SPC.Models;
using SkiaSharp;
using System.Text.Json;

using System.IO;
using DocumentFormat.OpenXml.InkML;
using static PWD_DSWD_SPC.Models.Registered.Medicine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;


namespace PWD_DSWD_SPC.Controllers.User
{
    [Authorize(Roles = "Client")]
    public class UserController : Controller
    {
        private readonly RegisterDbContext _registerDbContext;
        private readonly ILogger<UserController> _logger;

        public UserController(RegisterDbContext registerDbContext, ILogger<UserController> logger)
        {
            _registerDbContext = registerDbContext;
            _logger = logger;
        }

        public IActionResult UserDash()
        {
            var userAccountId = GetUserAccountId(); // Get the logged-in user's AccountId

            if (userAccountId == Guid.Empty)
            {
                return RedirectToAction("Login", "Account");
            }

            // Convert current UTC time to Philippine Standard Time (UTC +8)
            var philippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var philippineDateTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippineTimeZone);

            // Determine the start of the current week (Monday at 00:00:00) in Philippine time
            DateTime startOfWeek = philippineDateTime.Date.AddDays(-(int)philippineDateTime.DayOfWeek + (int)DayOfWeek.Monday);

            // Fetch the most recent commodity transaction for the user account
            var lastTransaction = _registerDbContext.CommodityTransactions
                .Where(t => t.AccountId == userAccountId)
                .OrderByDescending(t => t.CreatedDate)
                .FirstOrDefault();

            decimal remainingBalance = 2500; // Default weekly reset balance

            if (lastTransaction != null)
            {
                // Check if the last transaction is within the current week
                DateTime transactionDate = TimeZoneInfo.ConvertTimeFromUtc(lastTransaction.CreatedDate, philippineTimeZone);
                if (transactionDate >= startOfWeek)
                {
                    // Use the balance from the most recent transaction if it's within the current week
                    remainingBalance = lastTransaction.RemainingDiscount;
                }
            }

            // Pass the calculated balance to the view
            ViewBag.RemainingBalance = remainingBalance;

            // Fetch distinct accredited establishments
            var accreditedEstablishments = _registerDbContext.QrCodes
                .GroupBy(q => new { q.EstablishmentName, q.Branch })
                .Select(g => g.First()) // Get the first entry of each group
                .ToList();

            return View(accreditedEstablishments);
        }



        public IActionResult Logout()
        {
            HttpContext.SignOutAsync("CookieAuth");
            return RedirectToAction("Login", "Landing");
        }

        public IActionResult UserProfile()
        {
            // Retrieve username from claims
            var userName = User?.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var userAccount = _registerDbContext.Accounts
                                                    .Include(a => a.Status)
                                                    .Include(a => a.UserCredential)
                                                    .FirstOrDefault(a => a.UserCredential.Username == userName);

                if (userAccount == null)
                {
                    return NotFound("User not found");
                }

                ViewBag.FullName = $"{userAccount.FirstName} {userAccount.LastName}";
                ViewBag.UserAccountId = userAccount.Id; // Set the UserAccountId
                ViewBag.PWDNo = userAccount.DisabilityNumber;
                ViewBag.Validity = userAccount.ValidUntil;
                ViewBag.DisabilityType = userAccount.TypeOfDisability;
                ViewBag.ContactNo = userAccount.MobileNo;
                ViewBag.Address = userAccount.Barangay;
                ViewBag.DateOfBirth = userAccount.DateOfBirth.ToString("MM/dd/yyyy");
                ViewBag.Sex = userAccount.Gender;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile for {Username}", userName);
                return StatusCode(500, "An unexpected error occurred while fetching user profile.");
            }
        }

        public IActionResult AccountSetting()
        {
            return View();
        }


        // Change Password function
        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            //var userName = HttpContext.Session.GetString("Username");
            var userName = User.Identity?.Name ?? HttpContext.Session.GetString("Username");


            if (string.IsNullOrEmpty(userName))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var userCredential = _registerDbContext.UserCredential.FirstOrDefault(uc => uc.Username == userName);
                if (userCredential == null)
                {
                    ViewBag.PasswordChangeError = "User not found.";
                    return View("AccountSetting");
                }

                if (!VerifyPassword(currentPassword, userCredential.Password))
                {
                    ViewBag.PasswordChangeError = "The current password is incorrect.";
                    return View("AccountSetting");
                }

                if (newPassword != confirmPassword)
                {
                    ViewBag.PasswordChangeError = "New password and confirm password do not match.";
                    return View("AccountSetting");
                }

                userCredential.Password = HashPassword(newPassword);
                _registerDbContext.SaveChanges();

                ViewBag.PasswordChangeSuccess = "Password changed successfully.";
                return View("AccountSetting");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for {Username}", userName);
                ViewBag.PasswordChangeError = "An unexpected error occurred. Please try again.";
                return View("AccountSetting");
            }
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool VerifyPassword(string enteredPassword, string storedPasswordHash)
        {
            return BCrypt.Net.BCrypt.Verify(enteredPassword, storedPasswordHash);
        }

        public IActionResult AccreditedEstab()
        {
            // Fetch data from the database and remove duplicates based on EstablishmentName and Branch
            var qrCodes = _registerDbContext.QrCodes
                                  .GroupBy(q => new { q.EstablishmentName, q.Branch })
                                  .Select(g => g.First()) // Select the first record in each group
                                  .ToList();

            return View(qrCodes);
        }


        public IActionResult QrScan()
        {
            return View();
        }


        [HttpPost]
        public IActionResult ProcessQrCode(string qrCodeValue)
        {
            if (!string.IsNullOrEmpty(qrCodeValue))
            {
                try
                {
                    var qrContent = JsonConvert.DeserializeObject<dynamic>(qrCodeValue);

                    if (qrContent != null)
                    {
                        var commoditiesUrl = qrContent.CommoditiesUrl?.ToString();
                        var medicineUrl = qrContent.MedicineUrl?.ToString();
                        var establishment = qrContent.EstablishmentName?.ToString();
                        var branch = qrContent.Branch?.ToString();

                        if (!string.IsNullOrEmpty(commoditiesUrl) && !string.IsNullOrEmpty(medicineUrl))
                        {
                            // Store the values in TempData for cross-action persistence
                            TempData["CommoditiesUrl"] = commoditiesUrl;
                            TempData["MedicineUrl"] = medicineUrl;
                            TempData["Establishment"] = establishment;
                            TempData["Branch"] = branch;

                            // Based on the QR code content, redirect to the appropriate action
                            if (!string.IsNullOrEmpty(commoditiesUrl))
                            {
                                return RedirectToAction("Commodities", new { establishment = establishment, branch = branch });
                            }
                            else if (!string.IsNullOrEmpty(medicineUrl))
                            {
                                return RedirectToAction("Medicine", new { establishment = establishment, branch = branch });
                            }
                        }
                        else
                        {
                            ViewBag.ScannedResult = "Invalid QR code content.";
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    ViewBag.ScannedResult = qrCodeValue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing QR code: {QrCodeValue}", qrCodeValue);
                    ViewBag.ScannedResult = "An unexpected error occurred while processing the QR code.";
                }
            }
            else
            {
                ViewBag.Error = "QR code scanning failed or no value received.";
            }

            return View("ProcessQrCode");
        }



        public IActionResult Medicine(string establishment, string branch)
        {
            //Retrieve the user's account ID
            var accountId = GetUserAccountId();
            ViewBag.AccountId = accountId;

            // Use TempData or query parameters to pass the data
            ViewBag.Establishment = TempData["Establishment"] ?? establishment;
            ViewBag.Branch = TempData["Branch"] ?? branch;

            return View();
        }


        [Route("User/SubmitTransaction")]
        [HttpPost]
        public async Task<IActionResult> SubmitTransaction([FromBody] List<Medicine.MedicineTransaction> transactions)
        {
            if (transactions == null || !transactions.Any())
            {
                return BadRequest(new { message = "No transactions were provided." });
            }

            try
            {
                foreach (var transaction in transactions)
                {
                    var account = await _registerDbContext.Accounts.FindAsync(transaction.AccountId);
                    if (account == null)
                    {
                        return BadRequest(new { message = "Session Expired, please log-in again. Thank you!" });
                    }

                    // Ensure all price values are non-negative
                    transaction.Price = transaction.Price >= 0 ? transaction.Price : 0;
                    transaction.TotalPrice = transaction.TotalPrice >= 0 ? transaction.TotalPrice : 0;
                    transaction.DiscountedPrice = transaction.DiscountedPrice >= 0 ? transaction.DiscountedPrice : 0;

                    // Get or create ledger
                    var ledger = await _registerDbContext.MedicineTransactionLedgers
                        .FirstOrDefaultAsync(l => l.AccountId == transaction.AccountId);

                    if (ledger == null)
                    {
                        ledger = new MedicineTransactionLedger
                        {
                            LedgerId = Guid.NewGuid(),
                            AccountId = transaction.AccountId,
                        };
                        _registerDbContext.MedicineTransactionLedgers.Add(ledger);
                        await _registerDbContext.SaveChangesAsync();
                    }

                    // Calculate remaining balance
                    var remainingBalance = transaction.PrescribedQuantity - transaction.PurchasedQuantity;
                    if (remainingBalance < 0)
                    {
                        return BadRequest(new { message = "Purchased quantity cannot exceed prescribed quantity." });
                    }

                    // Set transaction status based on remaining balance
                    transaction.TransactionStatus = remainingBalance == 0 ? "Completed" : "Pending";

                    // Find all pending transactions for the same medicine
                    var pendingTransactions = await _registerDbContext.MedicineTransactions
                        .Where(t => t.AccountId == transaction.AccountId 
                            && t.MedicineName == transaction.MedicineName 
                            && t.TransactionStatus == "Pending")
                        .OrderBy(t => t.DatePurchased)
                        .ToListAsync();

                    // Mark all previous pending transactions as completed
                    foreach (var pendingTransaction in pendingTransactions)
                    {
                        pendingTransaction.TransactionStatus = "Completed";
                        pendingTransaction.LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
                    }

                    // Create a new transaction record for history
                    transaction.MedTransactionId = Guid.NewGuid();
                    transaction.RemainingBalance = remainingBalance;
                    transaction.LedgerId = ledger.LedgerId;
                    transaction.DatePurchased = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
                    transaction.EstablishmentName = string.IsNullOrEmpty(transaction.EstablishmentName) ? "Unknown" : transaction.EstablishmentName;
                    transaction.Branch = string.IsNullOrEmpty(transaction.Branch) ? "Unknown" : transaction.Branch;
                    transaction.LastModified = transaction.DatePurchased;

                    // Always add the transaction for history tracking
                    _registerDbContext.MedicineTransactions.Add(transaction);
                }

                await _registerDbContext.SaveChangesAsync();

                return Ok(new
                {
                    message = "Transactions submitted successfully!",
                    processedTransactions = transactions.Select(t => new
                    {
                        t.MedTransactionId,
                        t.MedicineName,
                        t.MedicineBrand,
                        t.PrescribedQuantity,
                        t.PurchasedQuantity,
                        t.RemainingBalance,
                        t.Price,
                        t.TotalPrice,
                        t.DiscountedPrice,
                        t.EstablishmentName,
                        t.Branch,
                        t.TransactionStatus,
                        t.DatePurchased,
                        t.LastModified
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing the transactions.", error = ex.Message });
            }
        }



        [HttpGet]
        [Route("User/GetUnfinishedTransactions")]
        public async Task<IActionResult> GetUnfinishedTransactions(Guid accountId)
        {
            try
            {
                // First get all pending transactions with remaining balance
                var allTransactions = await _registerDbContext.MedicineTransactions
                    .Where(t => t.AccountId == accountId && 
                           t.RemainingBalance > 0 && 
                           t.TransactionStatus != "Completed")  // Changed from "Pending" to != "Completed"
                    .OrderByDescending(t => t.DatePurchased)
                    .ToListAsync();

                // Then group them by medicine name and take the most recent for each
                var latestTransactions = allTransactions
                    .GroupBy(t => t.MedicineName)
                    .Select(g => g.First())
                    .OrderByDescending(t => t.DatePurchased)
                    .ToList();

                if (!latestTransactions.Any())
                    return NotFound(new { message = "No unfinished transactions found." });

                return Ok(latestTransactions.Select(t => new
                {
                    t.MedTransactionId,
                    t.MedicineBrand,
                    t.MedicineName,
                    t.PrescribedQuantity,
                    t.PurchasedQuantity,
                    t.RemainingBalance,
                    t.Price,
                    t.TotalPrice,
                    t.DiscountedPrice,
                    t.DatePurchased,
                    t.EstablishmentName,
                    t.Branch,
                    t.PTRNo,
                    t.AttendingPhysician,
                    t.TransactionStatus
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch transactions.", error = ex.Message });
            }
        }

        [HttpGet]
        [Route("User/GetAllTransactions")]
        public async Task<IActionResult> GetAllTransactions(Guid accountId)
        {
            try
            {
                var transactions = await _registerDbContext.MedicineTransactions
                    .Where(t => t.AccountId == accountId)
                    .OrderByDescending(t => t.DatePurchased)
                    .ToListAsync();

                if (!transactions.Any())
                    return NotFound(new { message = "No transactions found." });

                return Ok(transactions.Select(t => new
                {
                    t.MedTransactionId,
                    t.MedicineBrand,
                    t.MedicineName,
                    t.PrescribedQuantity,
                    t.PurchasedQuantity,
                    t.RemainingBalance,
                    t.Price,
                    t.TotalPrice,
                    t.DiscountedPrice,
                    t.DatePurchased,
                    t.EstablishmentName,
                    t.Branch,
                    t.PTRNo,
                    t.AttendingPhysician,
                    t.TransactionStatus,
                    t.LastModified
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch transactions.", error = ex.Message });
            }
        }



        [HttpGet]
        [Route("User/GetTransactionDetails")]
        public async Task<IActionResult> GetTransactionDetails(Guid transactionId)
        {
            try
            {
                var transaction = await _registerDbContext.MedicineTransactions
                    .Where(t => t.MedTransactionId == transactionId)
                    .FirstOrDefaultAsync();

                if (transaction == null)
                    return NotFound(new { message = "Transaction not found." });

                return Ok(new
                {
                    transaction.PTRNo,
                    transaction.AttendingPhysician,
                    transaction.Signature
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch transaction details.", error = ex.Message });
            }
        }

        [HttpPost]
        [Route("User/UpdateTransactionDetails")]
        public async Task<IActionResult> UpdateTransactionDetails([FromBody] MedicineTransaction updatedTransaction)
        {
            try
            {
                if (updatedTransaction == null)
                    return BadRequest(new { message = "Invalid transaction data." });

                // Fetch the existing transaction
                var transaction = await _registerDbContext.MedicineTransactions
                    .Where(t => t.MedTransactionId == updatedTransaction.MedTransactionId)
                    .FirstOrDefaultAsync();

                if (transaction == null)
                    return NotFound(new { message = "Transaction not found." });

                // Update only the fields that are provided
                if (!string.IsNullOrEmpty(updatedTransaction.PTRNo))
                    transaction.PTRNo = updatedTransaction.PTRNo;

                if (!string.IsNullOrEmpty(updatedTransaction.AttendingPhysician))
                    transaction.AttendingPhysician = updatedTransaction.AttendingPhysician;

                if (!string.IsNullOrEmpty(updatedTransaction.Signature))
                    transaction.Signature = updatedTransaction.Signature;

                // Save the changes
                await _registerDbContext.SaveChangesAsync();

                return Ok(new { message = "Transaction updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update transaction.", error = ex.Message });
            }
        }



        // Get the logged-in user's AccountId from the claims
        private Guid GetUserAccountId()
        {
            var accountIdClaim = User?.FindFirst("AccountId");
            if (accountIdClaim != null && Guid.TryParse(accountIdClaim.Value, out Guid userAccountId))
            {
                return userAccountId;
            }
            return Guid.Empty;
        }

        public IActionResult Commodities(string establishment, string branch)
        {
            // Retrieve the user's account ID
            var accountId = GetUserAccountId();
            ViewBag.AccountId = accountId;

            // Use TempData or query parameters to pass the data
            ViewBag.Establishment = TempData["Establishment"] ?? establishment; // Prefer TempData if available
            ViewBag.Branch = TempData["Branch"] ?? branch;

            return View();
        }


        [HttpGet]
        [Route("User/GetRemainingBalance")]
        public IActionResult GetRemainingBalance(Guid accountId)
        {
            if (accountId == Guid.Empty)
            {
                return Json(new { success = false, message = "Invalid AccountId." });
            }

            // Convert current UTC time to Philippine Standard Time (UTC +8)
            var philippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var philippineDateTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippineTimeZone);

            // Determine the start of the current week (Monday at 00:00:00) in Philippine time
            DateTime startOfWeek = philippineDateTime.Date.AddDays(-(int)philippineDateTime.DayOfWeek + (int)DayOfWeek.Monday);

            // Fetch the most recent commodity transaction for the account
            var lastTransaction = _registerDbContext.CommodityTransactions
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.CreatedDate)
                .FirstOrDefault();

            // Default balance at the start of a new week
            decimal remainingBalance = 2500;

            if (lastTransaction != null)
            {
                // Convert transaction date to Philippine time for accurate comparison
                DateTime transactionDate = TimeZoneInfo.ConvertTimeFromUtc(lastTransaction.CreatedDate, philippineTimeZone);

                // If the last transaction is within the current week, use its remaining balance
                if (transactionDate >= startOfWeek)
                {
                    remainingBalance = lastTransaction.RemainingDiscount;
                }
            }

            return Json(new { success = true, remainingBalance });
        }

        // Helper to get the start of the week (Monday, 00:00:00) in Philippine time
        private DateTime GetStartOfWeek(DateTime date, TimeZoneInfo tz)
        {
            var localDate = TimeZoneInfo.ConvertTimeFromUtc(date, tz);
            int diff = (7 + (localDate.DayOfWeek - DayOfWeek.Monday)) % 7;
            return localDate.Date.AddDays(-diff);
        }

        [Route("User/SubmitCommodities")]
        [HttpPost]
        public IActionResult SubmitCommodities([FromBody] CommodityTransaction transaction)
        {
            if (transaction == null || transaction.Items == null || !transaction.Items.Any())
            {
                return BadRequest("Invalid transaction data.");
            }

            // Retrieve AccountId based on the logged-in user
            transaction.AccountId = GetUserAccountId();

            if (transaction.AccountId == Guid.Empty)
            {
                return BadRequest("Session Timeout. Please log in again.");
            }

            // Validate AccountId
            var account = _registerDbContext.Accounts.FirstOrDefault(a => a.Id == transaction.AccountId);

            // Fetch the most recent commodity transaction for this account, if it exists
            var lastTransaction = _registerDbContext.CommodityTransactions
                .Where(t => t.AccountId == transaction.AccountId)
                .OrderByDescending(t => t.CreatedDate)
                .FirstOrDefault();

            // Convert UTC time to Philippine Standard Time (UTC +8)
            var philippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var nowPhilippine = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippineTimeZone);
            var currentWeekStart = GetStartOfWeek(DateTime.UtcNow, philippineTimeZone);

            // Determine the remaining discount
            if (lastTransaction != null)
            {
                var lastTransactionWeekStart = GetStartOfWeek(lastTransaction.CreatedDate, philippineTimeZone);
                if (currentWeekStart > lastTransactionWeekStart)
                {
                    // New week: Reset the remaining discount to 2500
                    transaction.RemainingDiscount = 2500;
                }
                else
                {
                    // Same week: Use the remaining discount from the last transaction
                    transaction.RemainingDiscount = lastTransaction.RemainingDiscount;
                }
            }
            else
            {
                // No previous transaction: Initialize the remaining discount to 2500
                transaction.RemainingDiscount = 2500;
            }

            // Ensure transaction ID is unique for each new transaction.
            if (transaction.TransactionId == Guid.Empty)
            {
                transaction.TransactionId = Guid.NewGuid();
                transaction.CreatedDate = nowPhilippine;
            }

            // Set modified date to the current Philippine time
            transaction.ModifiedDate = nowPhilippine;

            decimal totalDiscountedAmount = 0;  // To track total discounted price

            foreach (var item in transaction.Items)
            {
                item.AccountId = transaction.AccountId;  // Assign AccountId to each item
                item.TotalPrice = item.Quantity * item.Price;  // Calculate total price
                decimal discountPrice = item.TotalPrice * 0.95m;  // Apply 5% discount

                // Calculate the difference for this item between the total and discounted price
                decimal currentDifference = item.TotalPrice - discountPrice;

                // If remaining discount is 0, apply no discount
                if (transaction.RemainingDiscount <= 0)
                {
                    item.DiscountedPrice = item.TotalPrice; // No discount if limit reached
                }
                else
                {
                    // Adjust the remaining discount based on the current difference
                    if (transaction.RemainingDiscount - currentDifference < 0)
                    {
                        // Only apply discount up to the remaining balance
                        decimal allowedDiscount = transaction.RemainingDiscount;
                        item.DiscountedPrice = item.TotalPrice - allowedDiscount;
                        transaction.RemainingDiscount = 0;
                    }
                    else
                    {
                        transaction.RemainingDiscount -= currentDifference;  // Apply the new deduction
                        transaction.RemainingDiscount = Math.Max(0, transaction.RemainingDiscount);  // Ensure it doesn't go negative
                        item.DiscountedPrice = discountPrice;  // Store the discounted price
                    }
                }

                totalDiscountedAmount += item.DiscountedPrice;  // Add to the total discounted amount

                // Set the created and modified dates for each item
                if (item.CommodityItemId == Guid.Empty)
                {
                    item.CreatedDate = nowPhilippine;
                }
                item.ModifiedDate = nowPhilippine;
            }

            // Save the transaction in the database
            _registerDbContext.CommodityTransactions.Add(transaction);
            _registerDbContext.SaveChanges();

            // Update the remaining discount after the transaction
            var updatedTransaction = _registerDbContext.CommodityTransactions
                .FirstOrDefault(t => t.TransactionId == transaction.TransactionId);

            if (updatedTransaction != null)
            {
                updatedTransaction.RemainingDiscount = transaction.RemainingDiscount;
                _registerDbContext.SaveChanges();
            }

            // Return the updated remaining discount
            return Ok(new
            {
                success = true,
                message = "Transaction completed!",
                remainingDiscount = transaction.RemainingDiscount
            });
        }

        public IActionResult History()
        {
            var userAccountId = GetUserAccountId(); // Get the logged-in user's AccountId  

            if (userAccountId == Guid.Empty)
            {
                return RedirectToAction("Login", "Account");
            }

            // Fetch commodity transactions  
            var commodityTransactions = _registerDbContext.CommodityTransactions
                .Where(t => t.AccountId == userAccountId)
                .Include(t => t.Items)
                .OrderByDescending(t => t.CreatedDate)
                .Select(t => new
                {
                    TransactionId = t.TransactionId.ToString(),
                    CreatedDate = t.CreatedDate.ToString("yyyy-MM-dd"),
                    t.EstablishmentName,
                    Branch = t.BranchName,
                    PurchaseType = "Commodity",
                    Items = t.Items.Select(i => new
                    {
                        Description = i.Description,
                        Brand = "", // Commodities do not have a brand  
                        Quantity = i.Quantity,
                        Price = i.Price,
                        TotalPrice = i.TotalPrice,
                        DiscountedPrice = i.DiscountedPrice,
                        PrescribedQuantity = (int?)null, // Add null for commodities  
                        RemainingBalance = (int?)null, // Add null for commodities  
                        AttendingPhysician = (string)null, // Add null for commodities  
                        PTRNo = (string)null, // Add null for commodities  
                        TransactionStatus = (string)null, // Add null for commodities  
                        LastModified = (string)null // Add null for commodities  
                    }).ToList()
                })
                .ToList();

            // Fetch all medicine transactions for the user, regardless of status  
            var medicineTransactions = _registerDbContext.MedicineTransactions
                .Where(m => m.AccountId == userAccountId)
                .OrderByDescending(m => m.DatePurchased)
                .Select(m => new
                {
                    TransactionId = m.MedTransactionId.ToString(),
                    CreatedDate = m.DatePurchased.ToString("yyyy-MM-dd"),
                    m.EstablishmentName,
                    Branch = m.Branch,
                    PurchaseType = "Medicine",
                    Items = new List<object>
                    {
                       new
                       {
                           Description = m.MedicineName,
                           Brand = m.MedicineBrand,
                           Quantity = m.PurchasedQuantity,
                           m.Price,
                           m.TotalPrice,
                           m.DiscountedPrice,
                           PrescribedQuantity = m.PrescribedQuantity,
                           RemainingBalance = m.RemainingBalance,
                           AttendingPhysician = m.AttendingPhysician,
                           PTRNo = m.PTRNo,
                           TransactionStatus = m.TransactionStatus,
                           LastModified = m.LastModified.ToString("yyyy-MM-dd HH:mm:ss")
                       }
                    }
                })
                .ToList();

            // Combine both transaction types into a single list  
            var allTransactions = commodityTransactions.Cast<object>().Concat(medicineTransactions.Cast<object>())
                .OrderByDescending(t => ((dynamic)t).CreatedDate)
                .ToList();

            ViewBag.Transactions = Newtonsoft.Json.JsonConvert.SerializeObject(allTransactions);

            return View();
        }





        [HttpGet]
        public IActionResult Report()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Report(string problemDescription, string establishment, string branch)
        {
            // Get the logged-in user's AccountId
            var userAccountId = GetUserAccountId();
            if (userAccountId == Guid.Empty)
            {
                return RedirectToAction("Login", "Account");
            }

            // Check if ProblemDescription is empty
            if (string.IsNullOrWhiteSpace(problemDescription))
            {
                // Display a SweetAlert error if ProblemDescription is missing
                TempData["Error"] = "Problem Description is required.";
                return RedirectToAction("Report");
            }


            // Create a new Report object and populate it with form data
            var report = new Report
            {
                ReportId = 0, // Assuming it's auto-incremented
                AccountId = userAccountId,
                ProblemDescription = problemDescription,
                Establishment = establishment,
                Branch = branch,
                CreatedDate = DateTime.UtcNow
            };

            try
            {
                _registerDbContext.Reports.Add(report);
                _registerDbContext.SaveChanges();

                TempData["Success"] = "Report submitted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report to the database.");
                TempData["Error"] = "An unexpected error occurred while submitting the report.";
            }

            return RedirectToAction("Report");
        }








    }
}