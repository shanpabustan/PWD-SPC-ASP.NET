using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PWD_DSWD_SPC.Data;
using PWD_DSWD_SPC.Models.Registered; // Include your QrCode model
using QRCoder;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.Kernel.Font;



namespace PWD_DSWD_SPC.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly RegisterDbContext _registerDbContext;
        private readonly ILogger<AdminController> _logger;
        private readonly IEmailService _emailService;


        //hosting
        // private readonly string _baseCommoditiesUrl = "https://pwd-spc.org/User/Commodities";
        // private readonly string _baseMedicineUrl = "https://pwd-spc.org/User/Medicine";

        //monsterasp
        private readonly string _baseCommoditiesUrl = "https://pwd-spc.runasp.net/User/Commodities";
        private readonly string _baseMedicineUrl = "https://pwd-spc.runasp.net/User/Medicine";


        //local
        //private readonly string _baseCommoditiesUrl = "https://localhost:7153/User/Commodities";
        //private readonly string _baseMedicineUrl = "https://localhost:7153/User/Medicine";



        // Constructor with dependency injection
        public AdminController(RegisterDbContext registerDbContext, ILogger<AdminController> logger, IEmailService emailService)
        {
            _registerDbContext = registerDbContext ?? throw new ArgumentNullException(nameof(registerDbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emailService = emailService;
        }


        // Admin Dashboard View
        public IActionResult Admin()
        {
            // Count the number of approved applicants
            int totalApprovedApplicants = _registerDbContext.Accounts
                .Where(a => a.Status.Status == "Approved")
                .Count();

            // Count the number of pending applicants
            int totalPendingApplicants = _registerDbContext.Accounts
                .Where(a => a.Status.Status == "Pending") // Adjust if needed
                .Count();

            // Count the number of archived applicants (Disapproved, Deceased, Change of Residency, Expired.)
            int totalArchivedApplicants = _registerDbContext.Accounts
                .Where(a => a.Status.Status == "Disapproved" ||
                             a.Status.Status == "Deceased" ||
                             a.Status.Status == "Expired" ||
                             a.Status.Status == "Change of Residency")
                .Count();

            // Pass the count to the view using ViewBag
            ViewBag.TotalApprovedApplicants = totalApprovedApplicants;
            ViewBag.TotalPendingApplicants = totalPendingApplicants;
            ViewBag.TotalArchivedApplicants = totalArchivedApplicants;



            // Fetch all distinct establishments from the QrCodes table
            var allEstablishments = _registerDbContext.QrCodes
                .GroupBy(q => q.EstablishmentName)
                .Select(g => new
                {
                    EstablishmentName = g.Key,
                    Branches = g.Select(b => new
                    {
                        BranchName = b.Branch,
                    }).Distinct().ToList(),
                    TotalBranches = g.Select(b => b.Branch).Distinct().Count()
                })
                .ToList();

            // Fetch transaction data as before
            var startDate = DateTime.UtcNow.AddDays(-30);
            var endDate = DateTime.UtcNow.Date;

            // Fetch visits for Commodity Transactions
            var totalVisitsQuery = _registerDbContext.CommodityTransactions
                .Where(c => c.CreatedDate.Date >= startDate && c.CreatedDate.Date <= endDate)
                .GroupBy(c => new { c.EstablishmentName, c.BranchName })
                .Select(g => new
                {
                    EstablishmentName = g.Key.EstablishmentName,
                    BranchName = g.Key.BranchName,
                    TotalVisits = g.Count()
                })
                .ToList();

            // Fetch visits for Medicine Transactions
            var totalMedicineVisits = _registerDbContext.MedicineTransactionLedgers
                .Where(ledger => ledger.Transactions
                    .Any(medTransaction => medTransaction.DatePurchased.Date >= startDate && medTransaction.DatePurchased.Date <= endDate))
                .GroupBy(ledger => new
                {
                    EstablishmentName = ledger.Transactions.First().EstablishmentName,
                    BranchName = ledger.Transactions.First().Branch
                })
                .Select(g => new
                {
                    EstablishmentName = g.Key.EstablishmentName,
                    BranchName = g.Key.BranchName,
                    TotalVisits = g.Count()
                })
                .ToList();

            // Combine transaction data
            var combinedVisits = totalVisitsQuery
                .Union(totalMedicineVisits)
                .GroupBy(v => new { v.EstablishmentName, v.BranchName })
                .Select(g => new
                {
                    EstablishmentName = g.Key.EstablishmentName,
                    BranchName = g.Key.BranchName,
                    TotalVisits = g.Sum(v => v.TotalVisits)
                })
                .ToList();

            // Merge all establishments with transaction data
            var establishmentsWithTransactions = allEstablishments
                .Select(establishment => new
                {
                    establishment.EstablishmentName,
                    Branches = establishment.Branches.Select(branch => new
                    {
                        BranchName = branch.BranchName,
                        TotalVisits = combinedVisits
                            .Where(cv => cv.EstablishmentName == establishment.EstablishmentName && cv.BranchName == branch.BranchName)
                            .Sum(cv => cv.TotalVisits) // Sum up visits for each branch
                    }).ToList(),
                    TotalBranches = establishment.TotalBranches
                })
                .ToList();

            // Pass the data to the view
            ViewBag.TotalAccreditedEstablishments = allEstablishments.Count;
            ViewBag.TotalVisits = establishmentsWithTransactions;



            // PWD Per Barangay function - include only approved accounts
            var pwdCountsPerBarangay = _registerDbContext.Accounts
                .Where(a => a.Status.IsApproved) // Only consider approved accounts
                .GroupBy(a => a.Barangay)
                .Select(g => new
                {
                    Barangay = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // List of all barangays
            var allBarangays = new List<string> {
             "I-A (Sambat)", "I-B (City+Riverside)", "I-C (Bagong Bayan)",
             "II-A (Triangulo)", "II-B (Guadalupe)", "II-C (Unson)",
             "II-D (Bulante)", "II-E (San Anton)", "II-F (Villa Rey)",
             "III-A (Hermanos Belen)", "III-B", "III-C (Labak/De Roma)",
             "III-D (Villongco)", "III-E", "III-F (Balagtas)",
             "IV-A", "IV-B", "IV-C",
             "V-A", "V-B", "V-C", "V-D",
             "VI-A (Mavenida)", "VI-B", "VI-C (Bagong Pook)", "VI-D (Lparkers)",
             "VI-E (YMCA)", "VII-A (P.Alcantara)", "VII-B",
             "VII-C", "VII-D", "VII-E",
             "Atisan", "Bautista", "Concepcion (Bunot)", "Del Remedio (Wawa)",
             "Dolores", "San Antonio 1 (Balanga)", "San Antonio 2 (Sapa)",
             "San Bartolome (Matang-ag)", "San Buenaventura (Palakpakin)",
             "San Crispin (Lumbangan)", "San Cristobal", "San Diego (Tiim)",
             "San Francisco (Calihan)", "San Gabriel (Butucan)", "San Gregorio",
             "San Ignacio", "San Isidro (Balagbag)", "San Joaquin", "San Jose (Malamig)",
             "San Juan", "San Lorenzo (Saluyan)", "San Lucas 1 (Malinaw)",
             "San Lucas 2", "San Marcos (Tikew)", "San Mateo", "San Miguel",
             "San Nicolas", "San Pedro", "San Rafael (Magampon)", "San Roque (Buluburan)",
             "San Vicente", "Santa Ana", "Santa Catalina (Sandig)",
             "Santa Cruz (Putol)", "Santa Elena", "Santa Filomena (Banlagin)",
             "Santa Isabel", "Santa Maria", "Santa Maria Magdalena (Boe)",
             "Santa Monica", "Santa Veronica (Bae)", "Santiago I (Bulaho)",
             "Santiago II", "Santisimo Rosario", "Santo Angel (Ilog)",
             "Santo Cristo", "Santo NiÃ±o (Arsum)", "Soledad (Macopa)"
         };

            // Create a dictionary to store counts
            var barangayCounts = allBarangays.ToDictionary(b => b, b => 0);

            // Update counts from the database
            foreach (var item in pwdCountsPerBarangay)
            {
                barangayCounts[item.Barangay] = item.Count;
            }

            // Sort the barangays by count in descending order
            var sortedBarangays = barangayCounts
                .OrderByDescending(b => b.Value)
                .Select(b => new
                {
                    Barangay = b.Key,
                    Count = b.Value
                })
                .ToList();

            ViewBag.PwdCountsPerBarangay = sortedBarangays;


            // Fetch total reports and their details
            var reports = _registerDbContext.Reports
                .Select(r => new
                {
                    FullName = r.Accounts.FirstName + " " + r.Accounts.LastName,
                    r.Accounts.TypeOfDisability,
                    Status = r.Acknowledged ? "Acknowledged" : "Pending"
                })
                .ToList();

            ViewBag.TotalReports = reports.Count;
            ViewBag.Reports = reports;


            // Count users for each disability type, considering only approved applicants
            var disabilityCounts = _registerDbContext.Accounts
                .Where(a => a.Status.IsApproved) // Filter for approved applicants
                .GroupBy(a => a.TypeOfDisability) // Group by Disability Type
                .Select(g => new
                {
                    DisabilityType = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Pass the disability data to the view
            ViewBag.DisabilityCounts = disabilityCounts;


            //QR Code data analytics 
            // Count the total number of generated QR codes
            int totalGeneratedQrCodes = _registerDbContext.QrCodes.Count();
            ViewBag.TotalGeneratedQrCodes = totalGeneratedQrCodes;

            //QR Code data analytics inside the modal
            var generatedQrCodes = _registerDbContext.QrCodes
                .GroupBy(q => new { q.EstablishmentName, q.Branch })
                .Select(g => new
                {
                    EstablishmentName = g.Key.EstablishmentName,
                    Branch = g.Key.Branch,
                    QrCodes = g.Select(q => new
                    {
                        q.QrCodeBase64,
                        q.RegistrationUrl,
                        q.TypeOfQRCode
                    }).ToList()
                }).ToList();

            ViewBag.GeneratedQrCodes = generatedQrCodes;



            return View();
        }

        [HttpGet]
        public IActionResult GetUserVisits(string establishmentName, string branchName, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                // Convert string dates to DateTime if provided
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(startDate?.ToString()))
                {
                    parsedStartDate = DateTime.Parse(startDate.ToString());
                }
                if (!string.IsNullOrEmpty(endDate?.ToString()))
                {
                    parsedEndDate = DateTime.Parse(endDate.ToString()).AddDays(1).AddSeconds(-1); // Include the entire end date
                }

                var commodityUsers = _registerDbContext.CommodityTransactions
                    .Where(c => c.EstablishmentName == establishmentName && c.BranchName == branchName)
                    .Where(c => (!parsedStartDate.HasValue || c.CreatedDate >= parsedStartDate.Value) &&
                                (!parsedEndDate.HasValue || c.CreatedDate <= parsedEndDate.Value))
                    .Join(_registerDbContext.Accounts,
                        transaction => transaction.AccountId,
                        account => account.Id,
                        (transaction, account) => new
                        {
                            User = account.Id,
                            FullName = $"{account.FirstName} {account.LastName}",
                            Date = transaction.CreatedDate,
                            TransactionType = "Commodity"
                        })
                    .ToList();

                var medicineUsers = _registerDbContext.MedicineTransactionLedgers
                    .SelectMany(l => l.Transactions)
                    .Where(t => t.EstablishmentName == establishmentName && t.Branch == branchName)
                    .Where(t => (!parsedStartDate.HasValue || t.DatePurchased >= parsedStartDate.Value) &&
                                (!parsedEndDate.HasValue || t.DatePurchased <= parsedEndDate.Value))
                    .Join(_registerDbContext.Accounts,
                        transaction => transaction.AccountId,
                        account => account.Id,
                        (transaction, account) => new
                        {
                            User = account.Id,
                            FullName = $"{account.FirstName} {account.LastName}",
                            Date = transaction.DatePurchased,
                            TransactionType = "Medicine"
                        })
                    .ToList();

                var allVisits = commodityUsers
                    .Union(medicineUsers)
                    .OrderBy(v => v.Date)
                    .Select(v => new
                    {
                        user = v.User,
                        fullName = v.FullName,
                        date = v.Date,
                        transactionType = v.TransactionType
                    })
                    .ToList();

                return Json(allVisits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user visits data");
                return Json(new { error = "Failed to load visit data: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ExportVisitsToExcel(string establishmentName, string branchName, string startDate, string endDate)
        {
            try
            {
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(startDate))
                {
                    parsedStartDate = DateTime.Parse(startDate);
                }
                if (!string.IsNullOrEmpty(endDate))
                {
                    parsedEndDate = DateTime.Parse(endDate).AddDays(1).AddSeconds(-1);
                }

                var visits = GetVisitsData(establishmentName, branchName, parsedStartDate, parsedEndDate);

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("User Visits");

                    // Add headers
                    worksheet.Cell(1, 1).Value = "Full Name";
                    worksheet.Cell(1, 2).Value = "Visit Date";
                    worksheet.Cell(1, 3).Value = "Transaction Type";

                    // Add data
                    int row = 2;
                    foreach (var visit in visits)
                    {
                        worksheet.Cell(row, 1).Value = visit.FullName;
                        worksheet.Cell(row, 2).Value = visit.Date;
                        worksheet.Cell(row, 3).Value = visit.TransactionType;
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Generate file name
                    var fileName = $"Visits_{establishmentName}_{branchName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                    // Return the Excel file
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        stream.Position = 0;
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting visits to Excel");
                return Json(new { error = "Failed to export data: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ExportVisitsToPDF(string establishmentName, string branchName, string startDate, string endDate)
        {
            try
            {
                DateTime? parsedStartDate = null;
                DateTime? parsedEndDate = null;

                if (!string.IsNullOrEmpty(startDate))
                {
                    parsedStartDate = DateTime.Parse(startDate);
                }
                if (!string.IsNullOrEmpty(endDate))
                {
                    parsedEndDate = DateTime.Parse(endDate).AddDays(1).AddSeconds(-1);
                }

                var visits = GetVisitsData(establishmentName, branchName, parsedStartDate, parsedEndDate);

                using (var stream = new MemoryStream())
                {
                    var writer = new PdfWriter(stream);
                    var pdf = new PdfDocument(writer);
                    var document = new Document(pdf);

                    // Add title
                    document.Add(new Paragraph($"User Visits Report - {establishmentName} ({branchName})")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(16));

                    // Add date range if specified
                    if (parsedStartDate.HasValue || parsedEndDate.HasValue)
                    {
                        var dateRange = $"Date Range: {parsedStartDate?.ToString("MM/dd/yyyy") ?? "Start"} to {parsedEndDate?.ToString("MM/dd/yyyy") ?? "End"}";
                        document.Add(new Paragraph(dateRange).SetTextAlignment(TextAlignment.CENTER));
                    }

                    // Create table
                    var table = new iText.Layout.Element.Table(3).UseAllAvailableWidth();
                    table.AddHeaderCell("Full Name");
                    table.AddHeaderCell("Visit Date");
                    table.AddHeaderCell("Transaction Type");

                    // Add data
                    foreach (var visit in visits)
                    {
                        table.AddCell(visit.FullName);
                        table.AddCell(visit.Date.ToString());
                        table.AddCell(visit.TransactionType);
                    }

                    document.Add(table);
                    document.Close();

                    // Generate file name
                    var fileName = $"Visits_{establishmentName}_{branchName}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

                    return File(stream.ToArray(), "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting visits to PDF");
                return Json(new { error = "Failed to export data: " + ex.Message });
            }
        }

        private List<VisitData> GetVisitsData(string establishmentName, string branchName, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                // Get commodity visits
                var commodityVisits = _registerDbContext.CommodityTransactions
                    .Where(c => c.EstablishmentName == establishmentName && c.BranchName == branchName)
                    .Where(c => (!startDate.HasValue || c.CreatedDate >= startDate.Value) &&
                                (!endDate.HasValue || c.CreatedDate <= endDate.Value))
                    .Join(_registerDbContext.Accounts,
                        transaction => transaction.AccountId,
                        account => account.Id,
                        (transaction, account) => new VisitData
                        {
                            User = account.Id,
                            FullName = $"{account.FirstName} {account.LastName}",
                            Date = transaction.CreatedDate,
                            TransactionType = "Commodity"
                        })
                    .ToList();

                // Get medicine visits
                var medicineVisits = _registerDbContext.MedicineTransactionLedgers
                    .SelectMany(l => l.Transactions)
                    .Where(t => t.EstablishmentName == establishmentName && t.Branch == branchName)
                    .Where(t => (!startDate.HasValue || t.DatePurchased >= startDate.Value) &&
                                (!endDate.HasValue || t.DatePurchased <= endDate.Value))
                    .Join(_registerDbContext.Accounts,
                        transaction => transaction.AccountId,
                        account => account.Id,
                        (transaction, account) => new VisitData
                        {
                            User = account.Id,
                            FullName = $"{account.FirstName} {account.LastName}",
                            Date = transaction.DatePurchased,
                            TransactionType = "Medicine"
                        })
                    .ToList();

                // Combine and sort the results
                var allVisits = commodityVisits
                    .Concat(medicineVisits)
                    .OrderBy(v => v.Date)
                    .ToList();

                return allVisits;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetVisitsData");
                throw;
            }
        }

        private class VisitData
        {
            public Guid User { get; set; }
            public string FullName { get; set; }
            public DateTime Date { get; set; }
            public string TransactionType { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteQrCode(string qrCodeBase64)
        {
            var qrCode = await _registerDbContext.QrCodes.FirstOrDefaultAsync(q => q.QrCodeBase64 == qrCodeBase64);

            if (qrCode != null)
            {
                _registerDbContext.QrCodes.Remove(qrCode);
                await _registerDbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "QR Code deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "QR Code not found.";
            }

            return RedirectToAction("Admin"); // Update to your actual view name
        }

        [HttpGet]
        public IActionResult GetBranches(string establishmentName)
        {
            // Fetch distinct branches for the selected establishment from QrCodes
            var branchData = _registerDbContext.QrCodes
                .Where(q => q.EstablishmentName == establishmentName)
                .GroupBy(q => q.Branch)
                .Select(g => new
                {
                    BranchName = g.Key,
                    Count = g.Count(),
                    TotalVisits = _registerDbContext.CommodityTransactions
                                    .Where(c => c.EstablishmentName == establishmentName && c.BranchName == g.Key)
                                    .Count() +
                                  _registerDbContext.MedicineTransactionLedgers
                                    .Where(ledger => ledger.Transactions
                                        .Any(medTransaction => medTransaction.EstablishmentName == establishmentName && medTransaction.Branch == g.Key))
                                    .SelectMany(ledger => ledger.Transactions)
                                    .Count()
                })
                .ToList();

            // Return only the necessary properties in a simple structure
            return Json(branchData.Select(branch => new { branch.BranchName, branch.Count, branch.TotalVisits }));
        }



        [HttpGet]
        public IActionResult GenerateApprovedApplicantsExcel()
        {
            var approvedApplicants = _registerDbContext.Accounts
                .Where(a => a.Status.IsApproved)
                .Select(a => new
                {
                    a.ApplicantType,
                    a.DisabilityNumber,
                    a.CreatedAt,
                    a.LastName,
                    a.FirstName,
                    a.MiddleName,
                    a.suffix,
                    a.DateOfBirth,
                    a.Gender,
                    a.CivilStatus,
                    a.FatherLastName,
                    a.FatherFirstName,
                    a.FatherMiddleName,
                    a.MotherLastName,
                    a.MotherFirstName,
                    a.MotherMiddleName,
                    a.GuardianLastName,
                    a.GuardianFirstName,
                    a.GuardianMiddleName,
                    a.TypeOfDisability,
                    a.CauseOfDisability,
                    a.HouseNoAndStreet,
                    a.Barangay,
                    a.Municipality,
                    a.Province,
                    a.Region,
                    a.LandlineNo,
                    a.MobileNo,
                    a.EmailAddress,
                    a.EducationalAttainment,
                    a.StatusOfEmployment,
                    a.CategoryOfEmployment,
                    a.TypeOfEmployment,
                    a.Occupation,
                    a.OtherOccupation,
                    a.OrganizationAffiliated,
                    a.ContactPerson,
                    a.OfficeAddress,
                    a.OfficeTelNo,
                    a.SSSNo,
                    a.GSISNo,
                    a.PagIBIGNo,
                    a.PSNNo,
                    a.PhilHealthNo,
                    a.AccomplishByLastName,
                    a.AccomplishByFirstName,
                    a.AccomplishByMiddleName
                })
                .ToList();

            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Approved Applicants");
            var currentRow = 1;

            // Set headers for the Excel file
            worksheet.Cell(currentRow, 1).Value = "Applicant Type";
            worksheet.Cell(currentRow, 2).Value = "Disability Number";
            worksheet.Cell(currentRow, 3).Value = "Created At";
            worksheet.Cell(currentRow, 4).Value = "Last Name";
            worksheet.Cell(currentRow, 5).Value = "First Name";
            worksheet.Cell(currentRow, 6).Value = "Middle Name";
            worksheet.Cell(currentRow, 7).Value = "Suffix";
            worksheet.Cell(currentRow, 8).Value = "Date of Birth";
            worksheet.Cell(currentRow, 9).Value = "Gender";
            worksheet.Cell(currentRow, 10).Value = "Civil Status";
            worksheet.Cell(currentRow, 11).Value = "Father Last Name";
            worksheet.Cell(currentRow, 12).Value = "Father First Name";
            worksheet.Cell(currentRow, 13).Value = "Father Middle Name";
            worksheet.Cell(currentRow, 14).Value = "Mother Last Name";
            worksheet.Cell(currentRow, 15).Value = "Mother First Name";
            worksheet.Cell(currentRow, 16).Value = "Mother Middle Name";
            worksheet.Cell(currentRow, 17).Value = "Guardian Last Name";
            worksheet.Cell(currentRow, 18).Value = "Guardian First Name";
            worksheet.Cell(currentRow, 19).Value = "Guardian Middle Name";
            worksheet.Cell(currentRow, 20).Value = "Type of Disability";
            worksheet.Cell(currentRow, 21).Value = "Cause of Disability";
            worksheet.Cell(currentRow, 22).Value = "House No and Street";
            worksheet.Cell(currentRow, 23).Value = "Barangay";
            worksheet.Cell(currentRow, 24).Value = "Municipality";
            worksheet.Cell(currentRow, 25).Value = "Province";
            worksheet.Cell(currentRow, 26).Value = "Region";
            worksheet.Cell(currentRow, 27).Value = "Landline No";
            worksheet.Cell(currentRow, 28).Value = "Mobile No";
            worksheet.Cell(currentRow, 29).Value = "Email Address";
            worksheet.Cell(currentRow, 30).Value = "Educational Attainment";
            worksheet.Cell(currentRow, 31).Value = "Status of Employment";
            worksheet.Cell(currentRow, 32).Value = "Category of Employment";
            worksheet.Cell(currentRow, 33).Value = "Type of Employment";
            worksheet.Cell(currentRow, 34).Value = "Occupation";
            worksheet.Cell(currentRow, 35).Value = "Other Occupation";
            worksheet.Cell(currentRow, 36).Value = "Organization Affiliated";
            worksheet.Cell(currentRow, 37).Value = "Contact Person";
            worksheet.Cell(currentRow, 38).Value = "Office Address";
            worksheet.Cell(currentRow, 39).Value = "Office Tel No";
            worksheet.Cell(currentRow, 40).Value = "SSS No";
            worksheet.Cell(currentRow, 41).Value = "GSIS No";
            worksheet.Cell(currentRow, 42).Value = "Pag-IBIG No";
            worksheet.Cell(currentRow, 43).Value = "PSN No";
            worksheet.Cell(currentRow, 44).Value = "PhilHealth No";
            worksheet.Cell(currentRow, 45).Value = "Accomplished By Last Name";
            worksheet.Cell(currentRow, 46).Value = "Accomplished By First Name";
            worksheet.Cell(currentRow, 47).Value = "Accomplished By Middle Name";

            // Populate data rows
            foreach (var applicant in approvedApplicants)
            {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = applicant.ApplicantType;
                worksheet.Cell(currentRow, 2).Value = applicant.DisabilityNumber;
                worksheet.Cell(currentRow, 3).Value = applicant.CreatedAt;
                worksheet.Cell(currentRow, 4).Value = applicant.LastName;
                worksheet.Cell(currentRow, 5).Value = applicant.FirstName;
                worksheet.Cell(currentRow, 6).Value = applicant.MiddleName;
                worksheet.Cell(currentRow, 7).Value = applicant.suffix;
                worksheet.Cell(currentRow, 8).Value = applicant.DateOfBirth;
                worksheet.Cell(currentRow, 9).Value = applicant.Gender;
                worksheet.Cell(currentRow, 10).Value = applicant.CivilStatus;
                worksheet.Cell(currentRow, 11).Value = applicant.FatherLastName;
                worksheet.Cell(currentRow, 12).Value = applicant.FatherFirstName;
                worksheet.Cell(currentRow, 13).Value = applicant.FatherMiddleName;
                worksheet.Cell(currentRow, 14).Value = applicant.MotherLastName;
                worksheet.Cell(currentRow, 15).Value = applicant.MotherFirstName;
                worksheet.Cell(currentRow, 16).Value = applicant.MotherMiddleName;
                worksheet.Cell(currentRow, 17).Value = applicant.GuardianLastName;
                worksheet.Cell(currentRow, 18).Value = applicant.GuardianFirstName;
                worksheet.Cell(currentRow, 19).Value = applicant.GuardianMiddleName;
                worksheet.Cell(currentRow, 20).Value = applicant.TypeOfDisability;
                worksheet.Cell(currentRow, 21).Value = applicant.CauseOfDisability;
                worksheet.Cell(currentRow, 22).Value = applicant.HouseNoAndStreet;
                worksheet.Cell(currentRow, 23).Value = applicant.Barangay;
                worksheet.Cell(currentRow, 24).Value = applicant.Municipality;
                worksheet.Cell(currentRow, 25).Value = applicant.Province;
                worksheet.Cell(currentRow, 26).Value = applicant.Region;
                worksheet.Cell(currentRow, 27).Value = applicant.LandlineNo;
                worksheet.Cell(currentRow, 28).Value = applicant.MobileNo;
                worksheet.Cell(currentRow, 29).Value = applicant.EmailAddress;
                worksheet.Cell(currentRow, 30).Value = applicant.EducationalAttainment;
                worksheet.Cell(currentRow, 31).Value = applicant.StatusOfEmployment;
                worksheet.Cell(currentRow, 32).Value = applicant.CategoryOfEmployment;
                worksheet.Cell(currentRow, 33).Value = applicant.TypeOfEmployment;
                worksheet.Cell(currentRow, 34).Value = applicant.Occupation;
                worksheet.Cell(currentRow, 35).Value = applicant.OtherOccupation;
                worksheet.Cell(currentRow, 36).Value = applicant.OrganizationAffiliated;
                worksheet.Cell(currentRow, 37).Value = applicant.ContactPerson;
                worksheet.Cell(currentRow, 38).Value = applicant.OfficeAddress;
                worksheet.Cell(currentRow, 39).Value = applicant.OfficeTelNo;
                worksheet.Cell(currentRow, 40).Value = applicant.SSSNo;
                worksheet.Cell(currentRow, 41).Value = applicant.GSISNo;
                worksheet.Cell(currentRow, 42).Value = applicant.PagIBIGNo;
                worksheet.Cell(currentRow, 43).Value = applicant.PSNNo;
                worksheet.Cell(currentRow, 44).Value = applicant.PhilHealthNo;
                worksheet.Cell(currentRow, 45).Value = applicant.AccomplishByLastName;
                worksheet.Cell(currentRow, 46).Value = applicant.AccomplishByFirstName;
                worksheet.Cell(currentRow, 47).Value = applicant.AccomplishByMiddleName;
            }

            var stream = new MemoryStream();
            workbook.SaveAs(stream); // Save workbook to stream
            stream.Position = 0; // Reset position after saving

            var fileName = $"Approved_Applicants_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            const string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(stream.ToArray(), mimeType, fileName); // Return the file without using 'using' for the stream
        }




        public async Task<IActionResult> ListofAllAccounts()
        {
            var accounts = await _registerDbContext.Accounts
                .Include(a => a.Status)
                .Include(a => a.UserCredential)
                .Where(a => a.Status.IsApproved &&
                            a.Status.Status != "Deceased" &&
                            a.Status.Status != "Change of Residency" &&
                            a.Status.Status != "Disapproved")
                .ToListAsync();

            var expiredAccounts = accounts.Where(a => a.IsExpired).ToList();

            foreach (var expiredAccount in expiredAccounts)
            {
                expiredAccount.Status.Status = "Expired"; // Use "Expired"
                _logger.LogInformation($"Expired account: {expiredAccount.LastName}, {expiredAccount.FirstName} (ID: {expiredAccount.Id})");

                // Send expiration email notification
                try
                {
                    await _emailService.SendExpirationEmailAsync(expiredAccount.EmailAddress);
                    _logger.LogInformation($"Expiration email sent to {expiredAccount.EmailAddress}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send expiration email to {expiredAccount.EmailAddress}.");
                }
            }

            if (expiredAccounts.Any())
            {
                await _registerDbContext.SaveChangesAsync();
            }

            var activeAccounts = accounts.Where(a => !a.IsExpired).ToList();

            _logger.LogInformation($"Fetched {activeAccounts.Count} active approved accounts.");
            return View(activeAccounts);
        }





        [HttpPost]
        public async Task<JsonResult> UpdateDetails(Guid id, [FromBody] Account updatedAccount)

        {

            var existingAccount = await _registerDbContext.Accounts.FindAsync(id);
            if (existingAccount == null)
            {
                return Json(new { success = false, message = "Account not found." });
            }

            // Update properties
            existingAccount.DisabilityNumber = updatedAccount.DisabilityNumber;
            existingAccount.FatherLastName = updatedAccount.FatherLastName;
            existingAccount.FatherFirstName = updatedAccount.FatherFirstName;
            existingAccount.MotherLastName = updatedAccount.MotherLastName;
            existingAccount.MotherFirstName = updatedAccount.MotherFirstName;
            existingAccount.GuardianLastName = updatedAccount.GuardianLastName;
            existingAccount.GuardianFirstName = updatedAccount.GuardianFirstName;
            existingAccount.TypeOfDisability = updatedAccount.TypeOfDisability;
            existingAccount.CauseOfDisability = updatedAccount.CauseOfDisability;
            existingAccount.HouseNoAndStreet = updatedAccount.HouseNoAndStreet;
            existingAccount.Barangay = updatedAccount.Barangay;
            existingAccount.Municipality = updatedAccount.Municipality;
            existingAccount.Region = updatedAccount.Region;
            existingAccount.LandlineNo = updatedAccount.LandlineNo;
            existingAccount.MobileNo = updatedAccount.MobileNo;
            existingAccount.EmailAddress = updatedAccount.EmailAddress;
            existingAccount.EducationalAttainment = updatedAccount.EducationalAttainment;
            existingAccount.StatusOfEmployment = updatedAccount.StatusOfEmployment;
            existingAccount.CategoryOfEmployment = updatedAccount.CategoryOfEmployment;
            existingAccount.TypeOfEmployment = updatedAccount.TypeOfEmployment;
            existingAccount.Occupation = updatedAccount.Occupation;
            existingAccount.OrganizationAffiliated = updatedAccount.OrganizationAffiliated;
            existingAccount.ContactPerson = updatedAccount.ContactPerson;
            existingAccount.OfficeAddress = updatedAccount.OfficeAddress;
            existingAccount.OfficeTelNo = updatedAccount.OfficeTelNo;
            existingAccount.SSSNo = updatedAccount.SSSNo;
            existingAccount.GSISNo = updatedAccount.GSISNo;
            existingAccount.PagIBIGNo = updatedAccount.PagIBIGNo;
            existingAccount.PSNNo = updatedAccount.PSNNo;
            existingAccount.PhilHealthNo = updatedAccount.PhilHealthNo;
            existingAccount.AccomplishByLastName = updatedAccount.AccomplishByLastName;
            existingAccount.AccomplishByFirstName = updatedAccount.AccomplishByFirstName;
            existingAccount.AccomplishByMiddleName = updatedAccount.AccomplishByMiddleName;

            try
            {
                await _registerDbContext.SaveChangesAsync();
                return Json(new { success = true, message = "Account details updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account details.");
                return Json(new { success = false, message = "An error occurred while updating the account." });
            }
        }


        // Archive Button
        [HttpPost]
        public async Task<IActionResult> Archive(Guid id, [FromBody] string reason)
        {
            var account = await _registerDbContext.Accounts
                .Include(a => a.Status)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (account != null && !string.IsNullOrEmpty(reason))
            {
                // Update the Status based on the selected reason
                if (reason == "Deceased" || reason == "Change of Residency")
                {
                    account.Status.Status = reason; // Use existing status property
                }

                await _registerDbContext.SaveChangesAsync();

                // Redirect to the ArchivedAccounts action after successful archive
                return RedirectToAction("ArchivedAccounts", "Admin");
            }

            // Handle failure case, redirect to an error or the same page with a message
            return RedirectToAction("ListofAllAccounts", "Admin", new { error = "Unable to archive the account." });
        }
        //Archive Table
        public async Task<IActionResult> ArchivedAccounts()
        {
            var accounts = await _registerDbContext.Accounts
                .Include(a => a.Status)
                .Include(a => a.UserCredential) // Optional: Include UserCredentials if needed
                .Where(a => a.Status.Status == "Disapproved" ||
                            a.Status.Status == "Deceased" ||
                            a.Status.Status == "Change of Residency" ||
                            a.Status.Status == "Expired") // Use "Expired"
                .ToListAsync();

            return View(accounts);
        }


        [HttpPost]
        public async Task<IActionResult> Restore(Guid id)
        {
            var account = await _registerDbContext.Accounts
                .Include(a => a.Status)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (account != null)
            {
                if (account.Status.Status == "Deceased" || account.Status.Status == "Change of Residency")
                {
                    // Restore the account by setting the Status back to Approved
                    account.Status.Status = "Approved";
                    account.Status.IsApproved = true;

                    await _registerDbContext.SaveChangesAsync();

                    TempData["RestoreSuccess"] = "Account has been successfully restored.";
                    return RedirectToAction("ListofAllAccounts"); // Redirect to the active accounts list
                }

                if (account.Status.Status == "Disapproved")
                {
                    // Update status back to "Pending"
                    account.Status.Status = "Pending";
                    account.Status.IsApproved = false;

                    await _registerDbContext.SaveChangesAsync();

                    // Redirect to Applicants view in ApplicantController
                    TempData["RestoreInfo"] = "Application has been moved back to Applicants view.";
                    return RedirectToAction("Applicants", "Applicant");
                }

                if (account.Status.Status == "Expired")
                {
                    // Restore expired account with a 5-year validity extension
                    DateTime validUntil = DateTime.Now.AddYears(5);
                    account.ValidUntil = validUntil;
                    account.Status.Status = "Approved";
                    account.Status.IsApproved = true;

                    // Send renewal email notification
                    try
                    {
                        await _emailService.SendRenewalEmailAsync(account.EmailAddress);
                        _logger.LogInformation($"Renewal email sent to {account.EmailAddress}.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send renewal email to {account.EmailAddress}.");
                    }

                    await _registerDbContext.SaveChangesAsync();

                    TempData["RestoreSuccess"] = "Expired account has been successfully restored and renewed for 5 more years.";
                    return RedirectToAction("ListofAllAccounts"); // Redirect to the active accounts list
                }
            }

            // Handle failure case with an error message
            TempData["RestoreError"] = "Unable to restore the account. Please try again.";
            return RedirectToAction("ArchivedAccounts");
        }


        public ActionResult QR()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> QR(QrCode model)
        {
            if (model == null || string.IsNullOrEmpty(model.EstablishmentName))
            {
                ModelState.AddModelError("EstablishmentName", "Establishment Name cannot be empty.");
                return View(model);
            }

            // Check for duplicates
            bool qrCodeExists = _registerDbContext.QrCodes.Any(q =>
                q.EstablishmentName == model.EstablishmentName &&
                q.Branch == model.Branch &&
                q.TypeOfQRCode == model.TypeOfQRCode);

            if (qrCodeExists)
            {
                ModelState.AddModelError("", "A QR Code for this establishment, branch, and type already exists.");
                return View(model);
            }


            string qrContent;

            // Determine the QR content based on the type of QR code
            if (model.TypeOfQRCode == "Commodities")
            {
                qrContent = $"{_baseCommoditiesUrl}?establishment={model.EstablishmentName}&branch={model.Branch}";
            }
            else if (model.TypeOfQRCode == "Medicine")
            {
                qrContent = $"{_baseMedicineUrl}?establishment={model.EstablishmentName}&branch={model.Branch}";
            }
            else if (model.TypeOfQRCode == "Both")
            {
                var combinedContent = new
                {
                    CommoditiesUrl = $"{_baseCommoditiesUrl}?establishment={model.EstablishmentName}&branch={model.Branch}",
                    MedicineUrl = $"{_baseMedicineUrl}?establishment={model.EstablishmentName}&branch={model.Branch}",
                    Establishment = model.EstablishmentName,
                    Branch = model.Branch
                };

                qrContent = JsonSerializer.Serialize(combinedContent);
            }
            else
            {
                ModelState.AddModelError("", "Invalid QR Code Type.");
                return View(model);
            }

            // Generate QR Code
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    byte[] qrCodeBytes = qrCode.GetGraphic(7);
                    string qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);
                    string qrCodeUri = $"data:image/png;base64,{qrCodeBase64}";

                    // Save to database
                    var qrCodeEntity = new QrCode
                    {
                        EstablishmentName = model.EstablishmentName,
                        Branch = model.Branch,
                        TypeOfQRCode = model.TypeOfQRCode,
                        QrCodeBase64 = qrCodeBase64,
                        RegistrationUrl = qrContent
                    };

                    await _registerDbContext.QrCodes.AddAsync(qrCodeEntity);
                    await _registerDbContext.SaveChangesAsync();

                    ViewBag.QrCodeUri = qrCodeUri;
                }
            }
            return View(model);
        }

        public ActionResult Commodities()
        {
            // Fetch all users with their AccountId, Name, and PWD No. excluding specific statuses
            var users = _registerDbContext.Accounts
                .Where(u => u.Status.Status != "Pending" && u.Status.Status != "Waiting" && u.Status.Status != "Disapproved") // Exclude "Pending" and "Waiting" statuses
                .Select(u => new
                {
                    AccountId = u.Id, // AccountId as key
                    Name = $"{u.FirstName} {u.LastName}",
                    PwdNo = u.DisabilityNumber
                })
                .ToList();

            return View(users); // Pass user data to the view
        }

        [HttpGet]
        public IActionResult GetTransactionsForUser(Guid userId)
        {
            var transactions = _registerDbContext.CommodityTransactions
                .Where(t => t.AccountId == userId)
                .Select(t => new
                {
                    t.TransactionId,
                    CreatedDate = t.CreatedDate,
                    t.EstablishmentName,
                    t.BranchName,
                    TotalPrice = t.Items.Sum(i => i.TotalPrice),
                    DiscountedPrice = t.Items.Sum(i => i.DiscountedPrice),
                    t.RemainingDiscount
                }).ToList();

            return Json(transactions);
        }

        [HttpGet]
        public IActionResult GetItemsForTransaction(Guid transactionId)
        {
            var items = _registerDbContext.CommodityItems
                .Where(i => i.TransactionId == transactionId)
                .Select(i => new
                {
                    i.Description,
                    i.Quantity,
                    i.Price,
                    i.TotalPrice,
                    i.DiscountedPrice
                }).ToList();

            return Json(items);
        }



        public ActionResult Medicines()
        {
            // Fetch all users with their AccountId, Name, and PWD No. excluding specific statuses
            var users = _registerDbContext.Accounts
                .Where(u => u.Status.Status != "Pending" && u.Status.Status != "Waiting" && u.Status.Status != "Disapproved") // Exclude "Pending" and "Waiting" statuses
                .Select(u => new
                {
                    AccountId = u.Id,
                    Name = $"{u.FirstName} {u.LastName}",
                    PwdNo = u.DisabilityNumber
                })
                .ToList();

            ViewBag.Users = users; // Store users in ViewBag
            return View();
        }

        [HttpGet]
        public JsonResult GetMedicineTransactions(Guid accountId)
        {
            var transactions = _registerDbContext.MedicineTransactions
                .Where(t => t.AccountId == accountId)
                .Select(t => new
                {
                    t.MedTransactionId,
                    t.DatePurchased,
                    EstablishmentName = t.EstablishmentName ?? "N/A", // Default to "N/A"
                    MedicineName = t.MedicineName ?? "N/A", // Default to "N/A"
                    MedicineBrand = t.MedicineBrand ?? "N/A",
                    AttendingPhysician = t.AttendingPhysician ?? "N/A", // Default to "N/A"
                    PTRNo = t.PTRNo ?? "N/A", // Default to "N/A"
                    t.PrescribedQuantity,
                    t.PurchasedQuantity,
                    t.RemainingBalance,
                    TotalPrice = t.TotalPrice != null ? t.TotalPrice : 0, // Default to 0
                    DiscountedPrice = t.DiscountedPrice != null ? t.DiscountedPrice : 0, // Default to 0
                    t.Branch // Make sure the Branch field is included
                })
                .ToList();

            return Json(transactions); // Ensure it's returning an array
        }





        //public IActionResult Report(string searchTerm, string statusFilter)
        //{
        //    var reportsQuery = _registerDbContext.Reports
        //        .Select(r => new
        //        {
        //            r.ReportId,
        //            r.AccountId,
        //            r.ProblemDescription,
        //            r.Establishment,
        //            r.Branch,
        //            r.CreatedDate,
        //            FullName = r.Accounts.FirstName + " " + r.Accounts.LastName,
        //            r.Accounts.TypeOfDisability,
        //            r.Accounts.MobileNo,
        //            r.Accounts.Barangay,
        //            r.Accounts.EmailAddress,
        //            r.Acknowledged
        //        })
        //        .AsQueryable();

        //    if (!string.IsNullOrEmpty(searchTerm))
        //    {
        //        reportsQuery = reportsQuery.Where(r => r.FullName.ToLower().Contains(searchTerm.ToLower()) || r.ReportId.ToString().Contains(searchTerm));
        //    }

        //    if (!string.IsNullOrEmpty(statusFilter))
        //    {
        //        if (statusFilter == "Acknowledged")
        //        {
        //            reportsQuery = reportsQuery.Where(r => r.Acknowledged);
        //        }
        //        else if (statusFilter == "Pending")
        //        {
        //            reportsQuery = reportsQuery.Where(r => !r.Acknowledged);
        //        }
        //    }

        //    var reports = reportsQuery.ToList();

        //    return View(reports);
        //}


        public IActionResult Report(string searchTerm, string typeOfDisability, string status, string establishment, string branch, string address)
        {
            var reportsQuery = _registerDbContext.Reports
                .Select(r => new
                {
                    r.ReportId,
                    r.AccountId,
                    r.ProblemDescription,
                    r.Establishment,
                    r.Branch,
                    r.CreatedDate,
                    FullName = r.Accounts.FirstName + " " + r.Accounts.LastName,
                    r.Accounts.TypeOfDisability,
                    r.Accounts.MobileNo,
                    r.Accounts.Barangay,
                    r.Accounts.EmailAddress,
                    r.Acknowledged
                })
                .AsQueryable();

            // Apply search term
            if (!string.IsNullOrEmpty(searchTerm))
            {
                reportsQuery = reportsQuery.Where(r => r.FullName.ToLower().Contains(searchTerm.ToLower()) || r.ReportId.ToString().Contains(searchTerm));
            }

            // Apply filters based on the modal selection
            if (!string.IsNullOrEmpty(typeOfDisability))
            {
                reportsQuery = reportsQuery.Where(r => r.TypeOfDisability.ToLower().Contains(typeOfDisability.ToLower()));
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Acknowledged")
                {
                    reportsQuery = reportsQuery.Where(r => r.Acknowledged);
                }
                else if (status == "Pending")
                {
                    reportsQuery = reportsQuery.Where(r => !r.Acknowledged);
                }
            }

            if (!string.IsNullOrEmpty(establishment))
            {
                reportsQuery = reportsQuery.Where(r => r.Establishment.ToLower().Contains(establishment));
            }

            if (!string.IsNullOrEmpty(branch))
            {
                reportsQuery = reportsQuery.Where(r => r.Branch.ToLower().Contains(branch));
            }

            if (!string.IsNullOrEmpty(address))
            {
                reportsQuery = reportsQuery.Where(r => r.Barangay.ToLower().Contains(address));
            }

            var reports = reportsQuery.ToList();

            return View(reports);
        }


        //EXPORT FILTERED COMPLAINTS OR REPORTS 
        public IActionResult ExportReport(string searchTerm, string typeOfDisability, string status, string establishment, string branch, string address)
        {
            var reportsQuery = _registerDbContext.Reports
                .Select(r => new
                {
                    r.ReportId,
                    FullName = r.Accounts.FirstName + " " + r.Accounts.LastName,
                    r.ProblemDescription,
                    r.Establishment,
                    r.Branch,
                    r.CreatedDate,
                    r.Accounts.TypeOfDisability,
                    r.Accounts.MobileNo,
                    r.Accounts.Barangay,
                    r.Accounts.EmailAddress,
                    Status = r.Acknowledged ? "Acknowledged" : "Pending"
                })
                .AsQueryable();

            // Apply filters (same as in the Report action)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                reportsQuery = reportsQuery.Where(r => r.FullName.ToLower().Contains(searchTerm.ToLower()) || r.ReportId.ToString().Contains(searchTerm));
            }
            if (!string.IsNullOrEmpty(typeOfDisability))
            {
                reportsQuery = reportsQuery.Where(r => r.TypeOfDisability.ToLower().Contains(typeOfDisability.ToLower()));
            }
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Acknowledged")
                {
                    reportsQuery = reportsQuery.Where(r => r.Status == "Acknowledged");
                }
                else if (status == "Pending")
                {
                    reportsQuery = reportsQuery.Where(r => r.Status == "Pending");
                }
            }
            if (!string.IsNullOrEmpty(establishment))
            {
                reportsQuery = reportsQuery.Where(r => r.Establishment.ToLower().Contains(establishment));
            }
            if (!string.IsNullOrEmpty(branch))
            {
                reportsQuery = reportsQuery.Where(r => r.Branch.ToLower().Contains(branch));
            }
            if (!string.IsNullOrEmpty(address))
            {
                reportsQuery = reportsQuery.Where(r => r.Barangay.ToLower().Contains(address));
            }

            var reports = reportsQuery.ToList();

            // Generate PDF
            using (var stream = new MemoryStream())
            {
                // Create PDF writer and document
                PdfWriter writer = new PdfWriter(stream);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf);

                // Create and set a bold font for the title
                PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                // Add the title to the document
                document.Add(new Paragraph("Complaints")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(18)
                    .SetFont(boldFont));

                // Add Table (compress the columns and adjust width)
                iText.Layout.Element.Table table = new iText.Layout.Element.Table(UnitValue.CreatePercentArray(new float[] { 2, 2, 3, 2, 2, 2, 2 })).SetWidth(UnitValue.CreatePercentValue(100));  // Adjust column widths
                table.AddHeaderCell("Full Name");
                table.AddHeaderCell("Type of Disability");
                table.AddHeaderCell("Problem Description");
                table.AddHeaderCell("Barangay");
                table.AddHeaderCell("Establishment");
                table.AddHeaderCell("Branch");
                table.AddHeaderCell("Status");

                // Add data rows
                foreach (var report in reports)
                {
                    table.AddCell(report.FullName);
                    table.AddCell(report.TypeOfDisability);
                    table.AddCell(report.ProblemDescription);
                    table.AddCell(report.Barangay);
                    table.AddCell(report.Establishment);
                    table.AddCell(report.Branch);
                    table.AddCell(report.Status);
                }

                // Convert to Philippine TimeZone (UTC+8)
                TimeZoneInfo phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");  // "Singapore Standard Time" corresponds to PH time
                DateTime phTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

                // Generate file name with format DD-MM-YYYY-SS_Complaints.pdf
                string fileName = phTime.ToString("dd-MM-yyyy-ss") + "_Complaints.pdf";

                // Debugging: Check if the document was created successfully
                Console.WriteLine("PDF Document Created: " + fileName);

                return File(stream.ToArray(), "application/pdf", fileName);
            }



        }









        [HttpPost]
        public async Task<IActionResult> AcknowledgeReport(int id)
        {
            // Find the report by ID
            var report = _registerDbContext.Reports
                .Include(r => r.Accounts)
                .FirstOrDefault(r => r.ReportId == id);

            if (report == null)
            {
                return Json(new { success = false, message = "Report not found." });
            }

            // Update the acknowledgment status
            report.Acknowledged = true;

            // Save changes to the database
            _registerDbContext.SaveChanges();

            // Send acknowledgment email
            string subject = "Your Report Has Been Acknowledged";
            string message = $@"
            Hello {report.Accounts.FirstName},

            Your report regarding the establishment '{report.Establishment}' and branch '{report.Branch}' has been acknowledged. We have reviewed the details and will proceed with the necessary actions.

            Regarding the issue with the establishment, we have noted the concern and will address it as soon as possible. 

            Please submit any additional requirements between 8:00 AM and 5:00 PM at the DSWD office. If you have any questions or need further assistance, feel free to reach out.

            Thank you for your cooperation!

            Best regards,
            DSWD San Pablo City";

            await _emailService.SendEmailAsync(report.Accounts.EmailAddress, subject, message);

            return Json(new { success = true, message = "Report acknowledged and email sent." });
        }


        public IActionResult Logout()
        {
            HttpContext.SignOutAsync("CookieAuth");
            return RedirectToAction("Login", "Landing");
        }
    }
}
