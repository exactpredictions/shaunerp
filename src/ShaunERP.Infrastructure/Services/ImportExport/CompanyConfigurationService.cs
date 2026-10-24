using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ShaunERP.Infrastructure.Services.ImportExport;

public interface ICompanyConfigurationService
{
    Task<byte[]> DownloadBlankTemplateAsync(string format = "json", CancellationToken ct = default);
    Task<CompanyConfigurationDto> ExportCurrentAsync(CancellationToken ct = default);
    CompanyConfigValidationResult Validate(CompanyConfigurationDto config);
    Task<CompanyConfigPreview> PreviewAsync(CompanyConfigurationDto proposed, CancellationToken ct = default);
    Task<CompanyConfigApplyResult> ApplyAsync(CompanyConfigurationDto proposed, CancellationToken ct = default);
    CompanyConfigurationDto Deserialize(string content, string format);
    string Serialize(CompanyConfigurationDto config, string format);
}

public sealed class CompanyConfigurationService : ICompanyConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly IAccountingService _accounting;
    private readonly IFinanceService _finance;

    public CompanyConfigurationService(
        ShaunErpDbContext db,
        ITenantContext tenant,
        IAuditService audit,
        IAccountingService accounting,
        IFinanceService finance)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _accounting = accounting;
        _finance = finance;
    }

    public Task<byte[]> DownloadBlankTemplateAsync(string format = "json", CancellationToken ct = default)
    {
        EnsureManage();
        var blank = BuildIndiaTemplate();
        var text = Serialize(blank, format);
        return Task.FromResult(Encoding.UTF8.GetBytes(text));
    }

    public async Task<CompanyConfigurationDto> ExportCurrentAsync(CancellationToken ct = default)
    {
        EnsureManage();
        var companyId = RequireCompanyId();
        var company = await _db.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId, ct);
        var profile = await _accounting.GetOrCreateProfileAsync(ct);
        var gst = await _accounting.GetOrCreateGstAsync(ct);
        var settings = await _accounting.GetOrCreateSettingsAsync(ct);
        var statutory = await _accounting.ListStatutoryAsync(ct);
        var currencies = await _accounting.ListCurrenciesAsync(ct);
        var book = await _finance.GetOrCreateBillingBookAsync(ct);
        var fy = await _accounting.GetCurrentFinancialYearAsync(ct);

        return new CompanyConfigurationDto
        {
            Identity = new CompanyIdentityConfig
            {
                LegalName = profile.LegalName,
                TradingName = profile.TradingName ?? company.Name,
                Domain = company.Domain,
                Description = company.Description,
                AddressLine1 = profile.AddressLine1,
                AddressLine2 = profile.AddressLine2,
                City = profile.City,
                District = profile.District,
                State = profile.State,
                Country = profile.Country,
                PostalCode = profile.PostalCode,
                Phone = profile.Phone,
                Mobile = profile.Mobile,
                Email = profile.Email,
                Website = profile.Website,
                PrimaryGstin = profile.PrimaryGstin,
                PrimaryPan = profile.PrimaryPan,
                LogoUrl = profile.LogoUrl
            },
            Financial = new CompanyFinancialConfig
            {
                BaseCurrency = book.Currency,
                FinancialYearLabel = fy?.Name,
                FinancialYearStart = fy?.StartDate,
                FinancialYearEnd = fy?.EndDate,
                BooksName = book.Name,
                BooksLegalName = book.LegalName,
                TaxId = book.TaxId
            },
            Gst = new CompanyGstConfig
            {
                GstRegistered = gst.GstRegistered,
                Gstin = gst.Gstin,
                RegistrationType = gst.RegistrationType.ToString(),
                State = gst.State,
                StateCode = gst.StateCode,
                CompositionScheme = gst.CompositionScheme,
                ReverseChargeApplicable = gst.ReverseChargeApplicable,
                DefaultTaxBehaviour = gst.DefaultTaxBehaviour,
                GstEffectiveDate = gst.GstEffectiveDate
            },
            Banking = new CompanyBankingConfig
            {
                DefaultCashLedgerCode = settings.DefaultCashLedgerCode,
                DefaultBankLedgerCode = settings.DefaultBankLedgerCode
            },
            Invoice = new CompanyInvoiceConfig
            {
                InvoiceNumberPrefix = book.InvoiceNumberPrefix,
                NextInvoiceSequence = book.NextInvoiceSequence,
                OrderNumberPrefix = book.OrderNumberPrefix,
                NextOrderSequence = book.NextOrderSequence,
                DefaultPaymentTerms = book.DefaultPaymentTerms,
                DefaultDueDays = book.DefaultDueDays,
                BillingStreet = book.BillingStreet,
                BillingCity = book.BillingCity,
                BillingState = book.BillingState,
                BillingZip = book.BillingZip,
                BillingCountry = book.BillingCountry,
                BillingEmail = book.Email,
                BillingPhone = book.ContactNumber
            },
            CrmLookups = DefaultCrmLookups(),
            ChartOfAccounts = new CompanyChartOfAccountsRecommendations
            {
                EnsureIndiaSystemCoa = true,
                RecommendedLedgerCodes =
                [
                    SystemLedgerCodes.Cash, SystemLedgerCodes.BankDefault,
                    SystemLedgerCodes.Sales, SystemLedgerCodes.AccountsReceivable,
                    SystemLedgerCodes.AccountsPayable
                ],
                Notes = ["System CoA templates are preserved; import does not delete system ledgers."]
            },
            StatutoryRegistrations = statutory.Select(s => new CompanyStatutoryConfig
            {
                RegistrationType = s.RegistrationType,
                RegistrationNumber = s.RegistrationNumber,
                RegistrationDate = s.RegistrationDate,
                ExpiryDate = s.ExpiryDate,
                IssuingAuthority = s.IssuingAuthority,
                State = s.State,
                Notes = s.Notes,
                IsActive = s.IsActive
            }).ToList(),
            Currencies = currencies.Select(c => new CompanyCurrencyConfig
            {
                CurrencyCode = c.CurrencyCode,
                CurrencyName = c.CurrencyName,
                Symbol = c.Symbol,
                DecimalPlaces = c.DecimalPlaces,
                IsBaseCurrency = c.IsBaseCurrency,
                IsActive = c.IsActive
            }).ToList(),
            AccountingSettings = new CompanyAccountingSettingsConfig
            {
                InventoryEnabled = settings.InventoryEnabled,
                ValuationMethod = settings.ValuationMethod.ToString(),
                NegativeStockAllowed = settings.NegativeStockAllowed,
                StockAccountingEnabled = settings.StockAccountingEnabled,
                AutoCreateCustomerLedger = settings.AutoCreateCustomerLedger,
                AutoCreateSupplierLedger = settings.AutoCreateSupplierLedger,
                AutoPostSalesInvoice = settings.AutoPostSalesInvoice,
                DefaultCashLedgerCode = settings.DefaultCashLedgerCode,
                DefaultBankLedgerCode = settings.DefaultBankLedgerCode,
                DefaultSalesLedgerCode = settings.DefaultSalesLedgerCode,
                DefaultArLedgerCode = settings.DefaultArLedgerCode,
                DefaultApLedgerCode = settings.DefaultApLedgerCode
            }
        };
    }

    public CompanyConfigValidationResult Validate(CompanyConfigurationDto config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(config.Identity.LegalName))
        {
            errors.Add("Identity.LegalName is required.");
        }

        if (!CsvValidator.TryEmail(config.Identity.Email, out var emailError) && emailError is not null)
        {
            errors.Add(emailError);
        }

        if (!CsvValidator.TryGstin(config.Identity.PrimaryGstin, out var gstinError) && gstinError is not null)
        {
            errors.Add(gstinError);
        }

        if (!CsvValidator.TryPan(config.Identity.PrimaryPan, out var panError) && panError is not null)
        {
            errors.Add(panError);
        }

        if (config.Gst.GstRegistered)
        {
            if (string.IsNullOrWhiteSpace(config.Gst.Gstin))
            {
                errors.Add("Gst.Gstin is required when GstRegistered is true.");
            }
            else if (!CsvValidator.TryGstin(config.Gst.Gstin, out var gErr) && gErr is not null)
            {
                errors.Add(gErr);
            }
        }

        if (config.Invoice.NextInvoiceSequence < 1)
        {
            errors.Add("Invoice.NextInvoiceSequence must be >= 1.");
        }

        if (string.IsNullOrWhiteSpace(config.Financial.BaseCurrency))
        {
            warnings.Add("Financial.BaseCurrency is empty; INR will be used.");
        }

        return new CompanyConfigValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    public async Task<CompanyConfigPreview> PreviewAsync(CompanyConfigurationDto proposed, CancellationToken ct = default)
    {
        EnsureManage();
        var validation = Validate(proposed);
        var current = await ExportCurrentAsync(ct);
        var diffs = Diff(current, proposed);
        return new CompanyConfigPreview
        {
            Proposed = proposed,
            Validation = validation,
            Diffs = diffs
        };
    }

    public async Task<CompanyConfigApplyResult> ApplyAsync(CompanyConfigurationDto proposed, CancellationToken ct = default)
    {
        EnsureManage();
        var companyId = RequireCompanyId();
        var validation = Validate(proposed);
        if (!validation.IsValid)
        {
            return new CompanyConfigApplyResult
            {
                Success = false,
                Messages = validation.Errors.ToList()
            };
        }

        var preview = await PreviewAsync(proposed, ct);
        var correlationId = Guid.NewGuid().ToString("N");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var company = await _db.Companies.FirstAsync(c => c.Id == companyId, ct);
            if (!string.IsNullOrWhiteSpace(proposed.Identity.TradingName))
            {
                company.Name = proposed.Identity.TradingName!;
            }
            else if (!string.IsNullOrWhiteSpace(proposed.Identity.LegalName))
            {
                company.Name = proposed.Identity.LegalName;
            }

            company.Domain = proposed.Identity.Domain;
            company.Description = proposed.Identity.Description;

            var profile = await _accounting.GetOrCreateProfileAsync(ct);
            profile.LegalName = proposed.Identity.LegalName;
            profile.TradingName = proposed.Identity.TradingName;
            profile.AddressLine1 = proposed.Identity.AddressLine1;
            profile.AddressLine2 = proposed.Identity.AddressLine2;
            profile.City = proposed.Identity.City;
            profile.District = proposed.Identity.District;
            profile.State = proposed.Identity.State;
            profile.Country = proposed.Identity.Country ?? "India";
            profile.PostalCode = proposed.Identity.PostalCode;
            profile.Phone = proposed.Identity.Phone;
            profile.Mobile = proposed.Identity.Mobile;
            profile.Email = proposed.Identity.Email;
            profile.Website = proposed.Identity.Website;
            profile.PrimaryGstin = proposed.Identity.PrimaryGstin;
            profile.PrimaryPan = proposed.Identity.PrimaryPan;
            profile.LogoUrl = proposed.Identity.LogoUrl;
            await _accounting.SaveProfileAsync(profile, ct);

            var gst = await _accounting.GetOrCreateGstAsync(ct);
            gst.GstRegistered = proposed.Gst.GstRegistered;
            gst.Gstin = proposed.Gst.Gstin;
            gst.RegistrationType = Enum.TryParse<GstRegistrationType>(proposed.Gst.RegistrationType, true, out var regType)
                ? regType
                : GstRegistrationType.Unregistered;
            gst.State = proposed.Gst.State;
            gst.StateCode = proposed.Gst.StateCode;
            gst.CompositionScheme = proposed.Gst.CompositionScheme;
            gst.ReverseChargeApplicable = proposed.Gst.ReverseChargeApplicable;
            gst.DefaultTaxBehaviour = proposed.Gst.DefaultTaxBehaviour;
            gst.GstEffectiveDate = proposed.Gst.GstEffectiveDate;
            await _accounting.SaveGstAsync(gst, ct);

            var settings = await _accounting.GetOrCreateSettingsAsync(ct);
            settings.InventoryEnabled = proposed.AccountingSettings.InventoryEnabled;
            if (Enum.TryParse<InventoryValuationMethod>(proposed.AccountingSettings.ValuationMethod, true, out var valMethod))
            {
                settings.ValuationMethod = valMethod;
            }

            settings.NegativeStockAllowed = proposed.AccountingSettings.NegativeStockAllowed;
            settings.StockAccountingEnabled = proposed.AccountingSettings.StockAccountingEnabled;
            settings.AutoCreateCustomerLedger = proposed.AccountingSettings.AutoCreateCustomerLedger;
            settings.AutoCreateSupplierLedger = proposed.AccountingSettings.AutoCreateSupplierLedger;
            settings.AutoPostSalesInvoice = proposed.AccountingSettings.AutoPostSalesInvoice;
            settings.DefaultCashLedgerCode = proposed.AccountingSettings.DefaultCashLedgerCode ?? proposed.Banking.DefaultCashLedgerCode;
            settings.DefaultBankLedgerCode = proposed.AccountingSettings.DefaultBankLedgerCode ?? proposed.Banking.DefaultBankLedgerCode;
            settings.DefaultSalesLedgerCode = proposed.AccountingSettings.DefaultSalesLedgerCode;
            settings.DefaultArLedgerCode = proposed.AccountingSettings.DefaultArLedgerCode;
            settings.DefaultApLedgerCode = proposed.AccountingSettings.DefaultApLedgerCode;
            await _accounting.SaveSettingsAsync(settings, ct);

            var book = await _finance.GetOrCreateBillingBookAsync(ct);
            book.Name = proposed.Financial.BooksName ?? proposed.Identity.TradingName ?? proposed.Identity.LegalName;
            book.LegalName = proposed.Financial.BooksLegalName ?? proposed.Identity.LegalName;
            book.TaxId = proposed.Financial.TaxId ?? proposed.Identity.PrimaryGstin;
            book.Currency = string.IsNullOrWhiteSpace(proposed.Financial.BaseCurrency) ? "INR" : proposed.Financial.BaseCurrency;
            var currentInvoicePrefix = book.InvoiceNumberPrefix;
            var currentInvoiceSequence = book.NextInvoiceSequence;
            var currentOrderPrefix = book.OrderNumberPrefix;
            var currentOrderSequence = book.NextOrderSequence;
            book.InvoiceNumberPrefix = proposed.Invoice.InvoiceNumberPrefix;
            book.NextInvoiceSequence = NextMonotonicDocumentSequence(
                currentInvoicePrefix, currentInvoiceSequence,
                proposed.Invoice.InvoiceNumberPrefix, proposed.Invoice.NextInvoiceSequence);
            book.OrderNumberPrefix = proposed.Invoice.OrderNumberPrefix;
            book.NextOrderSequence = NextMonotonicDocumentSequence(
                currentOrderPrefix, currentOrderSequence,
                proposed.Invoice.OrderNumberPrefix, proposed.Invoice.NextOrderSequence);
            book.DefaultPaymentTerms = proposed.Invoice.DefaultPaymentTerms;
            book.DefaultDueDays = proposed.Invoice.DefaultDueDays;
            book.BillingStreet = proposed.Invoice.BillingStreet;
            book.BillingCity = proposed.Invoice.BillingCity;
            book.BillingState = proposed.Invoice.BillingState;
            book.BillingZip = proposed.Invoice.BillingZip;
            book.BillingCountry = proposed.Invoice.BillingCountry;
            book.Email = proposed.Invoice.BillingEmail ?? proposed.Identity.Email;
            book.ContactNumber = proposed.Invoice.BillingPhone ?? proposed.Identity.Phone;
            await _finance.UpdateBillingBookAsync(book, ct);

            foreach (var currency in proposed.Currencies)
            {
                await _accounting.UpsertCurrencyAsync(new CurrencyMaster
                {
                    CurrencyCode = currency.CurrencyCode,
                    CurrencyName = currency.CurrencyName,
                    Symbol = currency.Symbol,
                    DecimalPlaces = currency.DecimalPlaces,
                    IsBaseCurrency = currency.IsBaseCurrency,
                    IsActive = currency.IsActive
                }, ct);
            }

            foreach (var statutory in proposed.StatutoryRegistrations)
            {
                await _accounting.UpsertStatutoryAsync(new CompanyStatutoryRegistration
                {
                    RegistrationType = statutory.RegistrationType,
                    RegistrationNumber = statutory.RegistrationNumber,
                    RegistrationDate = statutory.RegistrationDate,
                    ExpiryDate = statutory.ExpiryDate,
                    IssuingAuthority = statutory.IssuingAuthority,
                    State = statutory.State,
                    Notes = statutory.Notes,
                    IsActive = statutory.IsActive
                }, ct);
            }

            if (proposed.ChartOfAccounts.EnsureIndiaSystemCoa)
            {
                await _accounting.EnsureBootstrapAsync(ct);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _audit.WriteAsync(
                "company.config.apply",
                companyId: companyId,
                userId: _tenant.UserId,
                entityType: "CompanyConfiguration",
                entityId: companyId.ToString(),
                afterValues: $"diffs={preview.Diffs.Count}",
                correlationId: correlationId,
                ct: ct);

            return new CompanyConfigApplyResult
            {
                Success = true,
                FieldsUpdated = preview.Diffs.Count,
                Messages = [$"Applied {preview.Diffs.Count} configuration change(s)."],
                CorrelationId = correlationId
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new CompanyConfigApplyResult
            {
                Success = false,
                Messages = [$"Apply failed: {ex.Message}"]
            };
        }
    }

    public CompanyConfigurationDto Deserialize(string content, string format)
    {
        format = (format ?? "json").Trim().ToLowerInvariant();
        if (format is "yaml" or "yml")
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            return deserializer.Deserialize<CompanyConfigurationDto>(content)
                   ?? throw new InvalidOperationException("YAML content deserialized to null.");
        }

        return JsonSerializer.Deserialize<CompanyConfigurationDto>(content, JsonOptions)
               ?? throw new InvalidOperationException("JSON content deserialized to null.");
    }

    public string Serialize(CompanyConfigurationDto config, string format)
    {
        format = (format ?? "json").Trim().ToLowerInvariant();
        if (format is "yaml" or "yml")
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();
            return serializer.Serialize(config);
        }

        return JsonSerializer.Serialize(config, JsonOptions);
    }

    public static CompanyConfigurationDto BuildIndiaTemplate() => new()
    {
        Identity = new CompanyIdentityConfig
        {
            LegalName = "Shaun Infomedia OPC Private Limited",
            TradingName = "Shaun Infomedia",
            Country = "India",
            State = "",
            City = "",
            Email = "",
            Phone = "",
            PrimaryGstin = "",
            PrimaryPan = "",
            Cin = ""
        },
        Financial = new CompanyFinancialConfig
        {
            BaseCurrency = "INR",
            BooksName = "Shaun Infomedia",
            BooksLegalName = "Shaun Infomedia OPC Private Limited"
        },
        Gst = new CompanyGstConfig
        {
            GstRegistered = false,
            RegistrationType = "Unregistered",
            DefaultTaxBehaviour = "Exclusive"
        },
        Banking = new CompanyBankingConfig
        {
            DefaultCashLedgerCode = SystemLedgerCodes.Cash,
            DefaultBankLedgerCode = SystemLedgerCodes.BankDefault
        },
        Invoice = new CompanyInvoiceConfig
        {
            InvoiceNumberPrefix = "INV-",
            NextInvoiceSequence = 1001,
            OrderNumberPrefix = "SO-",
            NextOrderSequence = 1001,
            DefaultPaymentTerms = "Net 30",
            DefaultDueDays = 30,
            BillingCountry = "India"
        },
        CrmLookups = DefaultCrmLookups(),
        ChartOfAccounts = new CompanyChartOfAccountsRecommendations
        {
            EnsureIndiaSystemCoa = true,
            RecommendedLedgerCodes =
            [
                SystemLedgerCodes.Cash, SystemLedgerCodes.BankDefault,
                SystemLedgerCodes.Sales, SystemLedgerCodes.AccountsReceivable,
                SystemLedgerCodes.AccountsPayable
            ],
            Notes =
            [
                "Fill statutory fields before go-live.",
                "Do not store passwords or JWT secrets in this file."
            ]
        },
        Currencies =
        [
            new CompanyCurrencyConfig
            {
                CurrencyCode = "INR",
                CurrencyName = "Indian Rupee",
                Symbol = "₹",
                DecimalPlaces = 2,
                IsBaseCurrency = true,
                IsActive = true
            }
        ],
        AccountingSettings = new CompanyAccountingSettingsConfig
        {
            AutoCreateCustomerLedger = true,
            AutoCreateSupplierLedger = true,
            AutoPostSalesInvoice = true,
            DefaultCashLedgerCode = SystemLedgerCodes.Cash,
            DefaultBankLedgerCode = SystemLedgerCodes.BankDefault,
            DefaultSalesLedgerCode = SystemLedgerCodes.Sales,
            DefaultArLedgerCode = SystemLedgerCodes.AccountsReceivable,
            DefaultApLedgerCode = SystemLedgerCodes.AccountsPayable
        }
    };

    private static CompanyCrmLookupsConfig DefaultCrmLookups() => new()
    {
        LeadSources = ["Website", "Referral", "Cold call", "Partner", "Advertisement"],
        LeadStatuses = ["New", "Contacted", "Qualified", "Lost"],
        Industries = ["Technology", "Manufacturing", "Retail", "Services", "Healthcare"],
        OpportunityStages = ["Qualification", "Needs Analysis", "Proposal", "Negotiation", "Closed Won", "Closed Lost"],
        CustomerTypes = ["Business", "Individual"],
        Territories = ["North", "South", "East", "West", "Central"],
        PaymentTermsOptions = ["Due on receipt", "Net 15", "Net 30", "Net 45", "Net 60"]
    };

    private static IReadOnlyList<CompanyConfigDiffItem> Diff(CompanyConfigurationDto current, CompanyConfigurationDto proposed)
    {
        var items = new List<CompanyConfigDiffItem>();
        void Add(string path, object? before, object? after)
        {
            var b = before?.ToString() ?? string.Empty;
            var a = after?.ToString() ?? string.Empty;
            if (!string.Equals(b, a, StringComparison.Ordinal))
            {
                items.Add(new CompanyConfigDiffItem
                {
                    Path = path,
                    Action = string.IsNullOrEmpty(b) ? "Create" : "Update",
                    Before = b,
                    After = a
                });
            }
        }

        Add("Identity.LegalName", current.Identity.LegalName, proposed.Identity.LegalName);
        Add("Identity.TradingName", current.Identity.TradingName, proposed.Identity.TradingName);
        Add("Identity.Email", current.Identity.Email, proposed.Identity.Email);
        Add("Identity.Phone", current.Identity.Phone, proposed.Identity.Phone);
        Add("Identity.State", current.Identity.State, proposed.Identity.State);
        Add("Identity.City", current.Identity.City, proposed.Identity.City);
        Add("Identity.PrimaryGstin", current.Identity.PrimaryGstin, proposed.Identity.PrimaryGstin);
        Add("Identity.PrimaryPan", current.Identity.PrimaryPan, proposed.Identity.PrimaryPan);
        Add("Financial.BaseCurrency", current.Financial.BaseCurrency, proposed.Financial.BaseCurrency);
        Add("Gst.GstRegistered", current.Gst.GstRegistered, proposed.Gst.GstRegistered);
        Add("Gst.Gstin", current.Gst.Gstin, proposed.Gst.Gstin);
        Add("Gst.State", current.Gst.State, proposed.Gst.State);
        Add("Invoice.InvoiceNumberPrefix", current.Invoice.InvoiceNumberPrefix, proposed.Invoice.InvoiceNumberPrefix);
        Add("Invoice.NextInvoiceSequence", current.Invoice.NextInvoiceSequence, proposed.Invoice.NextInvoiceSequence);
        Add("Invoice.DefaultPaymentTerms", current.Invoice.DefaultPaymentTerms, proposed.Invoice.DefaultPaymentTerms);
        Add("AccountingSettings.AutoPostSalesInvoice", current.AccountingSettings.AutoPostSalesInvoice, proposed.AccountingSettings.AutoPostSalesInvoice);
        Add("Banking.DefaultBankLedgerCode", current.Banking.DefaultBankLedgerCode, proposed.Banking.DefaultBankLedgerCode);
        return items;
    }

    /// <summary>
    /// Keep live numbering when the India template (or an older export) reapplies the
    /// same prefix at a lower sequence. A prefix change is a new series, so the proposed
    /// start is accepted.
    /// </summary>
    public static int NextMonotonicDocumentSequence(
        string? currentPrefix,
        int currentSequence,
        string? proposedPrefix,
        int proposedSequence)
    {
        if (proposedSequence < 1)
            return Math.Max(currentSequence, 1);

        var samePrefix = string.Equals(
            (currentPrefix ?? string.Empty).Trim(),
            (proposedPrefix ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);

        return samePrefix ? Math.Max(currentSequence, proposedSequence) : proposedSequence;
    }

    private void EnsureManage()
    {
        if (_tenant.IsSystemAdmin) return;
        if (!_tenant.Permissions.Contains(Permissions.SettingsCompanyConfigManage))
        {
            throw new UnauthorizedAccessException("Company configuration permission required.");
        }
    }

    private int RequireCompanyId() =>
        _tenant.CompanyId ?? throw new InvalidOperationException("Company context is required.");
}
