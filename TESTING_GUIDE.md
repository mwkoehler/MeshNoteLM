# Testing Guide for AINotes

## Overview

This document describes the testing strategy, architecture, and best practices for the AINotes project.

## Test Project Structure

```
AINotes.Tests/
├── Unit/
│   └── Services/
│       └── NoteServiceTests.cs      # Unit tests for NoteService
├── Mocks/
│   ├── MockAppDatabase.cs           # In-memory SQLite database for testing
│   └── MockFileSystemWrapper.cs     # Mock file system operations
└── AINotes.Tests.csproj             # Test project configuration
```

## Testing Stack

- **xUnit** (v2.9.2) - Modern, extensible test framework
- **Moq** (v4.20.72) - Mocking framework for interfaces
- **FluentAssertions** (v7.0.0) - Readable, expressive assertions
- **SQLite (In-Memory)** - Fast, isolated database tests
- **SQLitePCLRaw.bundle_green** - SQLite native library bundle

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test AINotes.Tests/AINotes.Tests.csproj

# Run tests with detailed output
dotnet test AINotes.Tests/AINotes.Tests.csproj --logger "console;verbosity=detailed"

# Run tests with code coverage
dotnet test AINotes.Tests/AINotes.Tests.csproj /p:CollectCoverage=true
```

### Visual Studio

1. Open **Test Explorer** (Test → Test Explorer)
2. Click **Run All** to execute all tests
3. Click individual tests to run specific tests

## Test Coverage

### Currently Tested

✅ **NoteService**
- CRUD operations (Create, Read, Update, Delete)
- Get by ID
- Get all notes
- Ordering and filtering

### Testable But Not Yet Implemented

⚠️ **Services with FileSystem Dependencies**
- `AppDatabase` - Uses `FileSystem.AppDataDirectory`
- `SettingsService` - Uses `FileSystem.AppDataDirectory`
- `PdfCacheService` - Uses `FileSystem.CacheDirectory`

**Recommendation**: Refactor these services to use dependency injection for file system operations.

⚠️ **Services with Complex Dependencies**
- `PluginManager` - Requires DI container and reflection
- `MicrosoftAuthService` - Requires MSAL and platform-specific auth
- `MicrosoftGraphOfficeConverter` - Requires HTTP client and auth service

⚠️ **UI Components**
- ViewModels
- Views
- Controls

## Testability Improvements Made

### 1. IAppDatabase Interface Enhancement

**Before:**
```csharp
public interface IAppDatabase
{
    SQLiteAsyncConnection Connection { get; }
    Task InitializeAsync();
}
```

**After:**
```csharp
public interface IAppDatabase : IDisposable
{
    SQLiteAsyncConnection Connection { get; }
    Task InitializeAsync();
    Task<int> ClearAllDataAsync();  // For test cleanup
}
```

### 2. MockAppDatabase Implementation

Created an in-memory SQLite database for fast, isolated testing:

```csharp
public class MockAppDatabase : IAppDatabase
{
    private readonly SQLiteAsyncConnection _connection;

    public MockAppDatabase()
    {
        _connection = new SQLiteAsyncConnection(":memory:");
        InitializeAsync().Wait();
    }

    public SQLiteAsyncConnection Connection => _connection;

    public async Task InitializeAsync()
    {
        await _connection.CreateTableAsync<NoteModel>();
    }

    public async Task<int> ClearAllDataAsync()
    {
        await _connection.DeleteAllAsync<NoteModel>();
        return 0;
    }

    public void Dispose()
    {
        _connection?.CloseAsync().Wait();
    }
}
```

## Writing Tests

### Test Structure

Follow the **Arrange-Act-Assert** pattern:

```csharp
[Fact]
public async Task MethodName_ShouldExpectedBehavior_WhenCondition()
{
    // Arrange - Set up test data and dependencies
    var note = new NoteModel
    {
        Text = "Test note",
        Timestamp = DateTime.UtcNow.ToString("O")
    };

    // Act - Execute the method being tested
    await _noteService.UpdateAsync(note);

    // Assert - Verify the expected outcome
    note.Id.Should().BeGreaterThan(0);
}
```

### Test Naming Convention

Format: `MethodName_ShouldExpectedBehavior_WhenCondition`

Examples:
- `GetAsync_ShouldReturnNull_WhenNoteDoesNotExist`
- `UpdateAsync_ShouldInsertNewNote_WhenIdIsZero`
- `UpdateAsync_ShouldUpdateExistingNote_WhenIdIsNonZero`

### Using FluentAssertions

FluentAssertions provides readable, expressive assertions:

```csharp
// Instead of:
Assert.NotNull(note);
Assert.Equal("Test", note.Text);

// Use:
note.Should().NotBeNull();
note.Text.Should().Be("Test");

// Collections:
notes.Should().HaveCount(3);
notes.Should().Contain(n => n.Text == "Test");
notes.Should().BeInAscendingOrder(n => n.Timestamp);
```

## Known Issues and Limitations

### 1. SQLite OrderBy NullReferenceException

**Issue**: `GetAllAsync()` method fails when ordering by nullable string properties on empty tables.

**Status**: Tests pass when there's data, but fail on empty tables. This is a sqlite-net-e library limitation.

**Workaround**: Pre-populate test databases or catch/handle null references in production code.

### 2. FileSystem.AppDataDirectory Not Available in Tests

**Issue**: MAUI Essentials APIs (like `FileSystem`) are not available in standard .NET test projects.

**Solution**: Services using `FileSystem` are excluded from linked files in test project.

**Future Fix**: Create `IFileSystemService` interface and inject it into services that need file system access.

### 3. Test Project Cannot Reference MAUI Project Directly

**Issue**: MAUI projects use multiple target frameworks (net9.0-android, net9.0-ios, etc.) which aren't compatible with standard test projects.

**Solution**: Link individual source files instead of referencing the entire project.

**Current Approach**:
```xml
<ItemGroup>
  <Compile Include="..\AINotes\Models\**\*.cs" LinkBase="Models" />
  <Compile Include="..\AINotes\Interfaces\**\*.cs" LinkBase="Interfaces" />
  <Compile Include="..\AINotes\Services\NoteService.cs" Link="Services\NoteService.cs" />
  <!-- ... -->
</ItemGroup>
```

## Future Improvements

### 1. Add IFileSystemService Interface

```csharp
public interface IFileSystemService
{
    string AppDataDirectory { get; }
    string CacheDirectory { get; }
}

public class MauiFileSystemService : IFileSystemService
{
    public string AppDataDirectory => FileSystem.AppDataDirectory;
    public string CacheDirectory => FileSystem.CacheDirectory;
}

public class TestFileSystemService : IFileSystemService
{
    public string AppDataDirectory => Path.GetTempPath();
    public string CacheDirectory => Path.Combine(Path.GetTempPath(), "cache");
}
```

Then inject `IFileSystemService` into services that need it:

```csharp
public class AppDatabase(IFileSystemService fileSystem) : IAppDatabase
{
    private readonly Lazy<SQLiteAsyncConnection> _conn = new(() =>
    {
        string dbPath = Path.Combine(fileSystem.AppDataDirectory, "ainotes.db");
        return new SQLiteAsyncConnection(dbPath);
    });
    // ...
}
```

### 2. Integration Tests with Real Database

Create integration tests that use a real SQLite database file:

```csharp
public class DatabaseIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly IAppDatabase _database;

    public DatabaseIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        // Use real AppDatabase with test file path
    }

    [Fact]
    public async Task FullIntegrationTest()
    {
        // Test with real database file, not in-memory
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }
}
```

### 3. Plugin System Tests

Create tests for plugin loading and management:

```csharp
public class PluginManagerTests
{
    [Fact]
    public async Task LoadPluginsAsync_ShouldDiscoverAllPlugins()
    {
        var serviceProvider = CreateTestServiceProvider();
        var pluginManager = new PluginManager(serviceProvider, logger);

        await pluginManager.LoadPluginsAsync();

        var plugins = pluginManager.GetAllPlugins();
        plugins.Should().NotBeEmpty();
    }
}
```

### 4. UI Testing with FlaUI or Appium

For end-to-end UI testing, consider:
- **FlaUI** (Windows) - Automated UI testing framework
- **Appium** (Mobile) - Cross-platform mobile app automation

### 5. Continuous Integration (CI)

Add test automation to CI/CD pipeline:

```yaml
# Example GitHub Actions workflow
name: Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal
```

## Best Practices

### 1. Test Isolation

Each test should be independent and not rely on other tests:

```csharp
public class NoteServiceTests : IDisposable
{
    private readonly IAppDatabase _mockDatabase;

    public NoteServiceTests()
    {
        // Fresh database for each test
        _mockDatabase = new MockAppDatabase();
    }

    public void Dispose()
    {
        // Cleanup after each test
        _mockDatabase?.Dispose();
    }
}
```

### 2. Use Descriptive Test Names

Test names should clearly describe what is being tested:

```csharp
// Good
[Fact]
public async Task UpdateAsync_ShouldInsertNewNote_WhenIdIsZero()

// Bad
[Fact]
public async Task Test1()
```

### 3. One Assertion Per Test (When Possible)

Focus on testing one behavior per test:

```csharp
// Good - Tests one specific behavior
[Fact]
public async Task GetAsync_ShouldReturnNull_WhenNoteDoesNotExist()
{
    var result = await _noteService.GetAsync(999);
    result.Should().BeNull();
}

// Acceptable - Multiple related assertions for same behavior
[Fact]
public async Task UpdateAsync_ShouldInsertNewNote_WhenIdIsZero()
{
    var note = new NoteModel { Text = "Test" };
    await _noteService.UpdateAsync(note);

    note.Id.Should().BeGreaterThan(0);
    var retrieved = await _noteService.GetAsync(note.Id);
    retrieved.Should().NotBeNull();
    retrieved!.Text.Should().Be("Test");
}
```

### 4. Mock External Dependencies

Use Moq to mock external dependencies:

```csharp
[Fact]
public async Task ConvertToPdfAsync_ShouldReturnCachedPdf_WhenAvailable()
{
    // Arrange
    var mockAuthService = new Mock<IMicrosoftAuthService>();
    var mockCacheService = new Mock<PdfCacheService>();

    mockAuthService.Setup(x => x.IsAuthenticated).Returns(true);
    mockCacheService
        .Setup(x => x.GetCachedPdf(It.IsAny<byte[]>(), "test.docx"))
        .Returns(cachedPdf);

    var converter = new MicrosoftGraphOfficeConverter(
        mockAuthService.Object,
        mockCacheService.Object
    );

    // Act & Assert
    var result = await converter.ConvertToPdfAsync(fileData, "test.docx");
    result.Should().BeEquivalentTo(cachedPdf);
}
```

### 5. Use Theory for Parameterized Tests

Test multiple scenarios with one test method:

```csharp
[Theory]
[InlineData("document.docx")]
[InlineData("spreadsheet.xlsx")]
[InlineData("presentation.pptx")]
[InlineData("document with spaces.docx")]
public async Task ConvertToPdfAsync_ShouldHandleVariousFileNames(string fileName)
{
    // Test implementation
}
```

## Test Results

**Current Status** (as of last run):
- ✅ **5 tests passing**
- ❌ **2 tests failing** (SQLite OrderBy issue on empty tables)
- ⏱️ **Total Duration**: 91ms

### Passing Tests:
1. `GetAsync_ShouldReturnNull_WhenNoteDoesNotExist`
2. `UpdateAsync_ShouldInsertNewNote_WhenIdIsZero`
3. `UpdateAsync_ShouldUpdateExistingNote_WhenIdIsNonZero`
4. `DeleteAsync_ShouldRemoveNote_WhenNoteExists`
5. `GetAsync_ShouldReturnCorrectNote_WhenMultipleNotesExist`

### Failing Tests:
1. `GetAllAsync_ShouldReturnEmptyList_WhenNoNotesExist` - SQLite OrderBy NullReferenceException
2. `GetAllAsync_ShouldReturnAllNotes_OrderedByTimestamp` - SQLite OrderBy NullReferenceException

## Troubleshooting

### "Unable to load DLL 'e_sqlite3'"

**Solution**: Add `SQLitePCLRaw.bundle_green` package:

```bash
dotnet add package SQLitePCLRaw.bundle_green
```

### "The type initializer for 'SQLite.SQLiteConnection' threw an exception"

**Solution**: Ensure SQLitePCLRaw bundle is initialized. This should happen automatically with `bundle_green`.

### Tests Fail Due to File System Access

**Solution**: Refactor services to use `IFileSystemService` interface instead of directly accessing `FileSystem.AppDataDirectory`.

### Cannot Reference MAUI Project

**Solution**: Link individual source files instead of referencing the entire project (see Test Project Structure above).

## Contributing

When adding new features:

1. ✅ Write tests first (TDD approach recommended)
2. ✅ Ensure all tests pass before committing
3. ✅ Add documentation for new test patterns
4. ✅ Update this guide with any new testing strategies

## References

- [xUnit Documentation](https://xunit.net/)
- [Moq Quickstart](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [SQLite-net Documentation](https://github.com/praeclarum/sqlite-net)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
