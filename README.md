# Exchange Rate Management System

A refactored .NET 8 exchange rate management system designed to retrieve, cache, and manage currency exchange rates from multiple external providers with support for historical data, cross-currency conversions, and corrected rate handling.

## Project Overview

This system manages exchange rate data from multiple central bank providers (ECB, HMRC, MXCB, etc.) and provides a unified API for retrieving rates between any two currencies on a specified date. 

**Refactoring Goals:**
- ✅ Improve architecture by separating concerns (business logic, data access, external integrations, conversion calculation)
- ✅ Enhance maintainability and testability through clear responsibility boundaries
- ✅ Support missing historical rate auto-fetching from external providers when requested
- ✅ Enable rate correction overwrites without creating duplicate records
- ✅ Preserve all existing unit and integration tests with zero regression

## Features

- **Exchange Rate Retrieval**: Get rates between any two currencies on a specified date
- **Historical Rate Handling**: Automatically fetch missing historical data from external providers when requested
- **Rate Corrections**: Update stored rates when sources publish corrections with duplicate prevention
- **Cross-Currency Conversion**: Calculate indirect rates via triangulation when direct rates unavailable
- **Pegged Currency Support**: Handle currencies pegged to base currencies (e.g., AED pegged to USD)
- **Multiple Frequency Support**: Daily, weekly, monthly, and bi-weekly rate frequencies
- **In-Memory Caching**: SortedDictionary-based caching for O(log n) lookups with efficient fallback
- **Persistent Storage**: Store rates in data store with automatic overwrite on corrections
- **External Provider Integration**: Support for ECB, HMRC, MXCB, and other central bank APIs
- **Fallback to Last Available Rate**: Weekend/holiday handling by falling back to most recent available rate

## Architecture Summary

The refactored system follows a clean, layered architecture with clear separation of concerns:

```
┌────────────────────────────────────────────────────┐
│         HTTP Controller Endpoint                    │
│       GET /api/rates (from, to, date, ...)        │
└────────────────┬─────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────┐
│    ExchangeRateService (Business Logic)             │
│  - Orchestrates GetRate() public API                │
│  - Handles missing rate fetching                    │
│  - Manages rate correction overwrites               │
│  - Coordinates conversion logic                     │
└──────┬──────────────────┬──────────────────┬────────┘
       │                  │                  │
       ▼                  ▼                  ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────────┐
│ Repository   │  │  Fetcher     │  │ ConversionService│
│ (Data Access)│  │(Providers)   │  │ (Conversion Math)│
│              │  │              │  │                  │
│ • Cache mgmt │  │ • Fetch from │  │ • Direct rates   │
│ • Upsert     │  │   external   │  │ • Cross-currency │
│ • Query      │  │   providers  │  │ • Pegged logic   │
│              │  │ • Persist    │  │ • Fallback logic │
└──────────────┘  └──────────────┘  └──────────────────┘
```

### Components

**ExchangeRateService** (`IExchangeRateService`)
- Owns public `GetRate()` API and business rules
- Ensures rates are loaded before conversion attempts
- Fetches missing historical data on demand
- Handles rate corrections with automatic persistence

**ExchangeRateFetcher** (`IExchangeRateFetcher`)
- Pure provider integration layer
- Fetches rates from external central bank APIs
- Supports historical and latest rate fetches
- Calls repository to persist fetched rates
- Handles all rate frequency types (Daily, Weekly, Monthly, BiWeekly)

**ExchangeRateRepository** (`IExchangeRateRepository`)
- Pure data access layer (no business logic, no provider calls)
- In-memory cache with SortedDictionary for efficient lookups
- Upsert logic with duplicate detection
- Cache synchronization on rate corrections
- Pegged currency configuration management

**ExchangeRateConversionService** (`IExchangeRateConversionService`)
- Encapsulates all conversion and calculation logic
- Handles direct and indirect quote types (ECB, MXCB, HMRC styles)
- Implements cross-currency triangulation
- Manages pegged currency conversions
- Provides fallback to last available rate before a requested date

## Refactoring Changes

### Problem: Namespace vs Entity Type Conflict
The codebase had a critical namespace/type ambiguity where `ExchangeRate` referred to both:
- Root namespace: `ExchangeRate`
- Entity type: `ExchangeRate.Core.Entities.ExchangeRate`

This caused **CS0118 compilation errors** in files under `ExchangeRate.Core` namespace.

### Solution: Type Aliases
Applied explicit type aliasing (following existing provider patterns):
```csharp
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;
```

**Files fixed:**
- `IExchangeRateRepository.cs`
- `ExchangeRateRepository.cs`
- `IExchangeRateFetcher.cs`
- `ExchangeRateFetcher.cs`

### Architecture Improvements
1. **Separation of Concerns**: Business logic moved from Repository to Service layer
2. **Provider Integration**: Extracted into dedicated Fetcher service
3. **Conversion Logic**: Isolated in ConversionService for independent testing
4. **Data Access**: Repository focused solely on caching and querying
5. **Clear Dependencies**: Unidirectional dependency flow (Service → Fetcher → Repository)

### Testing & Regression Prevention
- ✅ All 38 existing integration tests pass
- ✅ 1 test skipped (expected behavior)
- ✅ Public API behavior unchanged
- ✅ All conversion rules preserved
- ✅ Cache behavior maintained
- ✅ Fallback logic intact

## How to Run

### Prerequisites
- .NET 8 SDK or later
- Windows, macOS, or Linux

### Build & Test

```bash
# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Run all tests (integration tests included)
dotnet test

# Expected output:
# Passed!  - Failed: 0, Passed: 38, Skipped: 1, Total: 39
```

### Run the API

```bash
# Start the API server (runs on http://localhost:5000)
dotnet run --project src/ExchangeRate.Api

# Example API calls:
# GET /api/rates?from=EUR&to=USD&date=2024-01-15&source=ECB&frequency=Daily
# GET /api/rates?from=USD&to=GBP&date=2024-01-15&source=ECB&frequency=Daily
```

### Query Exchange Rate

```bash
# Simple direct rate
curl "http://localhost:5000/api/rates?from=EUR&to=USD&date=2024-01-15&source=ECB&frequency=Daily"

# Cross-currency conversion
curl "http://localhost:5000/api/rates?from=USD&to=GBP&date=2024-01-15&source=ECB&frequency=Daily"

# Response format:
# {
#   "fromCurrency": "EUR",
#   "toCurrency": "USD",
#   "date": "2024-01-15",
#   "source": "ECB",
#   "frequency": "Daily",
#   "rate": 1.0856
# }
```

## Project Structure

```
exchange-rate-refactoring/
├── src/
│   ├── ExchangeRate.Api/              # ASP.NET Core API endpoint
│   │   ├── Program.cs                 # API configuration & DI setup
│   │   └── appsettings.json           # Configuration
│   │
│   └── ExchangeRate.Core/             # Domain & business logic
│       ├── Entities/                  # Domain models
│       │   ├── ExchangeRate.cs        # Rate entity
│       │   ├── PeggedCurrency.cs      # Pegged currency configuration
│       │   └── Country.cs             # Country reference data
│       │
│       ├── Enums/                     # Type enumerations
│       │   ├── CurrencyTypes.cs       # Currency codes
│       │   ├── ExchangeRateSources.cs # Provider types (ECB, MXCB, etc.)
│       │   ├── ExchangeRateFrequencies.cs # Rate frequencies
│       │   └── QuoteTypes.cs          # Direct/Indirect quotes
│       │
│       ├── Interfaces/                # Service contracts
│       │   ├── IExchangeRateService.cs
│       │   ├── IExchangeRateRepository.cs
│       │   ├── IExchangeRateFetcher.cs
│       │   └── IExchangeRateConversionService.cs
│       │
│       ├── Services/                  # Business logic implementations
│       │   ├── ExchangeRateService.cs
│       │   ├── ExchangeRateFetcher.cs
│       │   └── ExchangeRateConversionService.cs
│       │
│       ├── ExchangeRateRepository.cs  # Data access layer
│       │
│       ├── Helpers/                   # Utility functions
│       │   ├── CurrencyCodeMapper.cs  # String to enum conversion
│       │   └── PeriodHelper.cs        # Date calculations
│       │
│       ├── Providers/                 # External provider implementations
│       │   ├── ExternalApiExchangeRateProvider.cs (base)
│       │   ├── EUECBExchangeRateProvider.cs
│       │   ├── MXCBExchangeRateProvider.cs
│       │   └── [Other providers...]
│       │
│       └── Infrastructure/            # Data store abstraction
│           └── IExchangeRateDataStore.cs
│
└── tests/
    └── ExchangeRate.Tests/            # Integration tests
        └── ExchangeRateIntegrationTests.cs (38 tests)
```

### Key Folders

| Folder | Purpose |
|--------|---------|
| `src/ExchangeRate.Api` | ASP.NET Core API with HTTP endpoint and in-memory data store |
| `src/ExchangeRate.Core` | Domain models, business logic, and external integrations |
| `src/ExchangeRate.Core/Services` | Service layer (newly refactored) |
| `src/ExchangeRate.Core/Providers` | Central bank provider implementations |
| `tests/ExchangeRate.Tests` | Integration tests with WireMock mocking |

## AI Assistance Disclosure

This project was refactored with assistance from:

**GitHub Copilot**
- Analyzed legacy codebase to understand architectural patterns
- Suggested separation of concerns principles
- Provided code completion for service implementations
- Identified and helped fix namespace/type conflicts

**Claude (AI Assistant)**
- Architected the refactored design with clear responsibility boundaries
- Designed the service-oriented architecture
- Implemented all refactored service classes and interfaces
- Diagnosed and resolved CS0118 namespace conflicts
- Wrote comprehensive documentation

**Collaborative Approach:**
1. **Understanding** - Analyzed the 500+ line monolithic repository
2. **Design** - Extracted three complementary services with single responsibilities
3. **Implementation** - Created interfaces and implementations following SOLID principles
4. **Testing** - Verified all existing tests pass with zero regressions
5. **Documentation** - Created architecture diagrams and detailed README

## Contributing

When modifying this system:

1. **Maintain Separation**: Keep business logic in Service, data access in Repository, provider calls in Fetcher
2. **Add Tests**: New features should include integration tests
3. **Run Tests**: Always verify `dotnet test` passes before committing
4. **Document Changes**: Update README if architecture changes

## License

This project is provided as-is for educational and professional use.

## Support

For issues, questions, or suggestions:
- Review the [Architecture Summary](#architecture-summary) for component relationships
- Check test cases in `ExchangeRateIntegrationTests.cs` for usage examples
- Examine service implementations for implementation details

---

**Last Updated:** February 13, 2026  
**Status:** ✅ All Tests Passing (38/38 + 1 skipped)  
**Repository:** https://github.com/A3K3SH/exchange-rate-refactoring
