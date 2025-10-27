# Additional Tests Roadmap

## Overview

This document outlines all the additional tests that can be implemented for the AINotes project, organized by priority and complexity.

---

## 🟢 **Priority 1: High Value, Easy to Implement** (1-2 days)

### 1. LLMChatSession Tests ⭐⭐⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Services/LLMChatSessionTests.cs`

**Why**: Core functionality for chat feature, pure business logic with no external dependencies

**Testable Methods**:
- ✅ `AddUserMessage()` - Adds message and saves
- ✅ `AddAssistantMessage()` - Adds message with provider
- ✅ `AddErrorMessage()` - Adds error message
- ✅ `GetConversationContext()` - Builds context string
- ✅ `GetMessageHistory()` - Filters and formats messages
- ✅ `ClearConversation()` - Clears messages and file
- ✅ `CurrentFilePath` property - Triggers save/load
- ✅ File persistence - Save and load chat markdown

**Test Examples**:
```csharp
[Fact]
public void AddUserMessage_ShouldAddToMessages()
{
    var session = new LLMChatSession();
    session.AddUserMessage("Hello");
    session.Messages.Should().HaveCount(1);
    session.Messages[0].Role.Should().Be("user");
    session.Messages[0].Content.Should().Be("Hello");
}

[Fact]
public void GetConversationContext_ShouldIncludeFileContent()
{
    var session = new LLMChatSession();
    session.CurrentFilePath = "test.md";
    session.CurrentFileContent = "File content here";
    var context = session.GetConversationContext();
    context.Should().Contain("test.md");
    context.Should().Contain("File content here");
}
```

**Estimated**: 10-12 tests, ~2 hours

---

### 2. MarkdownRegexes Tests ⭐⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Helpers/MarkdownRegexesTests.cs`

**Why**: Simple regex validation, no dependencies

**Testable Methods**:
- ✅ `NumberedListItemRegex()` - Matches numbered lists

**Test Examples**:
```csharp
[Theory]
[InlineData("1. Item one", true)]
[InlineData("2. Item two", true)]
[InlineData("  3. Indented item", true)]
[InlineData("Not a list", false)]
[InlineData("1.No space", false)]
public void NumberedListItemRegex_ShouldMatchCorrectly(string input, bool shouldMatch)
{
    var regex = MarkdownRegexes.NumberedListItemRegex();
    regex.IsMatch(input).Should().Be(shouldMatch);
}
```

**Estimated**: 5-7 tests, ~30 minutes

---

### 3. LLMChatMessage Model Tests ⭐⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Models/LLMChatMessageTests.cs`

**Why**: Tests markdown serialization/deserialization

**Testable Methods**:
- ✅ `ToMarkdown()` - Convert message to markdown
- ✅ `FromMarkdown()` - Parse message from markdown
- ✅ `IsError` property - Check if message is error

**Test Examples**:
```csharp
[Fact]
public void ToMarkdown_ShouldFormatUserMessage()
{
    var message = new LLMChatMessage
    {
        Role = "user",
        Content = "Test question",
        Timestamp = new DateTime(2025, 10, 23, 10, 30, 0)
    };

    var markdown = message.ToMarkdown();
    markdown.Should().Contain("## User");
    markdown.Should().Contain("Test question");
    markdown.Should().Contain("2025-10-23");
}

[Fact]
public void FromMarkdown_ShouldParseAssistantMessage()
{
    var markdown = @"## Claude - 2025-10-23 10:30:00
Test response";

    var message = LLMChatMessage.FromMarkdown(markdown);
    message.Should().NotBeNull();
    message.Role.Should().Be("assistant");
    message.LLMProvider.Should().Be("Claude");
    message.Content.Should().Be("Test response");
}
```

**Estimated**: 8-10 tests, ~1 hour

---

### 4. NoteModel & SenderModel Tests ⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Models/ModelTests.cs`

**Why**: Simple POCOs, validation tests

**Testable**:
- ✅ NoteModel properties
- ✅ SenderModel properties
- ✅ Timestamp format validation

**Test Examples**:
```csharp
[Fact]
public void NoteModel_ShouldStoreProperties()
{
    var note = new NoteModel
    {
        Id = 1,
        Text = "Test note",
        Timestamp = DateTime.UtcNow.ToString("O")
    };

    note.Id.Should().Be(1);
    note.Text.Should().Be("Test note");
    note.Timestamp.Should().NotBeNullOrEmpty();
}
```

**Estimated**: 5-6 tests, ~30 minutes

---

## 🟡 **Priority 2: Medium Value, Moderate Complexity** (2-3 days)

### 5. PluginBase Tests ⭐⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Plugins/PluginBaseTests.cs`

**Why**: Base class for all plugins, important for plugin architecture

**Testable**:
- ✅ `HasValidAuthorization()` - Default returns true
- ✅ `TestConnectionAsync()` - Tests authorization
- ✅ `InitializeAsync()` - Base initialization
- ✅ `IsEnabled` property
- ✅ Abstract properties (Name, Version, Description)

**Test Examples**:
```csharp
public class TestPlugin : PluginBase
{
    public override string Name => "TestPlugin";
    public override string Version => "1.0.0";
    public override string Description => "Test plugin";
}

[Fact]
public async Task TestConnectionAsync_ShouldReturnSuccess_WhenAuthorized()
{
    var plugin = new TestPlugin();
    var (success, message) = await plugin.TestConnectionAsync();
    success.Should().BeTrue();
    message.Should().Contain("Valid");
}
```

**Estimated**: 8-10 tests, ~2 hours

---

### 6. AIProviderPluginBase Tests ⭐⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Plugins/AIProviderPluginBaseTests.cs`

**Why**: Base class for all LLM providers (Claude, OpenAI, etc.)

**Requires**: Reading the AIProviderPluginBase.cs to understand its methods

**Testable**:
- ✅ Common LLM provider functionality
- ✅ Message formatting
- ✅ Error handling patterns
- ✅ Streaming support

**Estimated**: 12-15 tests, ~3 hours

---

### 7. LocalFileSystemPluginBase Tests ⭐⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Plugins/LocalFileSystemPluginBaseTests.cs`

**Why**: Base class for local file system plugins

**Testable**:
- ✅ Common file operations
- ✅ Path normalization
- ✅ Directory navigation
- ✅ File filtering

**Estimated**: 10-12 tests, ~2 hours

---

### 8. ServiceHelper Tests ⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Helpers/ServiceHelperTests.cs`

**Why**: Utility class for service operations

**Requires**: Reading ServiceHelper.cs to understand its methods

**Estimated**: 6-8 tests, ~1 hour

---

## 🟠 **Priority 3: Integration Tests** (3-4 days)

### 9. Database Integration Tests ⭐⭐⭐⭐⭐
**Location**: `AINotes.Tests/Integration/DatabaseIntegrationTests.cs`

**Why**: Tests real database operations end-to-end

**Test Scenarios**:
- ✅ Full CRUD workflow with real SQLite file
- ✅ Concurrent operations
- ✅ Large datasets (1000+ notes)
- ✅ Transaction behavior
- ✅ Database recovery from corruption
- ✅ Migration scenarios

**Test Examples**:
```csharp
[Fact]
public async Task EndToEnd_CreateReadUpdateDelete_ShouldWork()
{
    var fileSystem = new TestFileSystemService();
    var database = new AppDatabase(fileSystem);
    await database.InitializeAsync();
    var noteService = new NoteService(database);

    // Create
    var note = new NoteModel { Text = "Test", Timestamp = DateTime.UtcNow.ToString("O") };
    await noteService.UpdateAsync(note);

    // Read
    var retrieved = await noteService.GetAsync(note.Id);
    retrieved.Should().NotBeNull();

    // Update
    retrieved.Text = "Updated";
    await noteService.UpdateAsync(retrieved);

    // Delete
    await noteService.DeleteAsync(retrieved);
    var deleted = await noteService.GetAsync(note.Id);
    deleted.Should().BeNull();
}
```

**Estimated**: 15-20 tests, ~4 hours

---

### 10. LLM Chat Integration Tests ⭐⭐⭐⭐
**Location**: `AINotes.Tests/Integration/LLMChatIntegrationTests.cs`

**Why**: Tests chat session persistence with real file system

**Test Scenarios**:
- ✅ Save and load chat sessions
- ✅ Multiple concurrent sessions
- ✅ Large chat history (100+ messages)
- ✅ File corruption recovery
- ✅ Markdown format validation

**Estimated**: 10-12 tests, ~3 hours

---

### 11. Settings Persistence Integration Tests ⭐⭐⭐
**Location**: `AINotes.Tests/Integration/SettingsPersistenceTests.cs`

**Why**: Tests settings across app restarts

**Test Scenarios**:
- ✅ Settings survive app restart (simulate with multiple instances)
- ✅ Settings file corruption handling
- ✅ Migration from old settings format
- ✅ Concurrent settings updates

**Estimated**: 8-10 tests, ~2 hours

---

## 🔴 **Priority 4: Complex/Time-Consuming Tests** (5-7 days)

### 12. PluginManager Tests ⭐⭐⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Plugins/PluginManagerTests.cs`

**Why**: Critical for plugin architecture

**Challenges**: Requires mocking DI container, reflection, and logger

**Testable**:
- ✅ `LoadPluginsAsync()` - Discovers and loads plugins
- ✅ `GetPlugin()` - Retrieves plugin by name
- ✅ `GetAllPlugins()` - Returns all plugins
- ✅ `EnablePlugin()` / `DisablePlugin()` - Toggle plugins
- ✅ `ReloadPluginAsync()` - Reloads a plugin
- ✅ Authorization checks during loading
- ✅ Plugin initialization failures

**Test Examples**:
```csharp
[Fact]
public async Task LoadPluginsAsync_ShouldDiscoverAllPlugins()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<TestPlugin>();
    var serviceProvider = services.BuildServiceProvider();

    var manager = new PluginManager(serviceProvider, logger);
    await manager.LoadPluginsAsync();

    var plugins = manager.GetAllPlugins();
    plugins.Should().NotBeEmpty();
}
```

**Estimated**: 15-20 tests, ~5 hours

---

### 13. Individual LLM Plugin Tests ⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Plugins/LLMPluginTests/`

**Why**: Tests each LLM provider implementation

**Requires**: Mocking HTTP responses

**Plugins to Test**:
- ClaudePlugin
- OpenAIPlugin
- GeminiPlugin
- GrokPlugin
- MetaPlugin
- MistralPlugin
- PerplexityPlugin

**Test Examples**:
```csharp
[Fact]
public async Task ClaudePlugin_SendAsync_ShouldFormatRequestCorrectly()
{
    var mockSettings = new Mock<ISettingsService>();
    mockSettings.Setup(s => s.ClaudeApiKey).Returns("test-key");

    var plugin = new ClaudePlugin(mockSettings.Object);
    // Test with mocked HTTP client
}
```

**Estimated per plugin**: 8-10 tests, ~2 hours each = **14-16 hours total**

---

### 14. FileSystem Plugin Tests ⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Plugins/FileSystemPluginTests/`

**Why**: Tests file system integrations

**Plugins to Test**:
- GoogleDrivePlugin (requires mocking Google API)
- NotionPlugin (requires mocking Notion API)
- ObsidianPlugin
- RedditPlugin (requires mocking Reddit API)
- ReaderPlugin (requires mocking Reader API)

**Challenges**: External API mocking

**Estimated per plugin**: 10-15 tests, ~3 hours each = **15-20 hours total**

---

### 15. ViewModels Tests ⭐⭐⭐⭐
**Location**: `AINotes.Tests/Unit/ViewModels/`

**Why**: Tests presentation logic

**ViewModels to Test**:
- NoteListViewModel
- NoteViewModel
- SourcesTreeViewModel
- FileTreeViewModel
- TreeNodeViewModel

**Testable**:
- ✅ Property change notifications (INotifyPropertyChanged)
- ✅ Command execution
- ✅ Data loading
- ✅ Filtering and sorting
- ✅ Navigation

**Test Examples**:
```csharp
[Fact]
public async Task NoteViewModel_LoadNote_ShouldSetProperties()
{
    var mockNoteService = new Mock<INoteService>();
    var note = new NoteModel { Id = 1, Text = "Test" };
    mockNoteService.Setup(s => s.GetAsync(1)).ReturnsAsync(note);

    var viewModel = new NoteViewModel(mockNoteService.Object);
    await viewModel.LoadNoteAsync(1);

    viewModel.NoteText.Should().Be("Test");
}
```

**Estimated per ViewModel**: 12-15 tests, ~3 hours each = **15-20 hours total**

---

### 16. Microsoft Graph Integration Tests ⭐⭐⭐
**Location**: `AINotes.Tests/Unit/Services/MicrosoftGraphTests.cs`

**Why**: Tests Office document conversion

**Requires**: Mocking HTTP responses for Microsoft Graph API

**Testable**:
- ✅ MicrosoftAuthService authentication flow (mocked)
- ✅ MicrosoftGraphOfficeConverter conversion (mocked API)
- ✅ Error handling for failed conversions
- ✅ Token refresh logic
- ✅ File upload/download simulation

**Estimated**: 15-20 tests, ~6 hours

---

## 🔵 **Priority 5: UI/E2E Tests** (7-10 days)

### 17. UI Component Tests
**Framework**: Would require FlaUI (Windows) or Appium (Mobile)

**Why**: Tests actual UI interactions

**Not Recommended Yet**: Wait until Phases 1-4 are complete

---

## 📊 **Summary**

| Priority | Category | Tests | Effort | Value | Status |
|----------|----------|-------|--------|-------|--------|
| ✅ Completed | Core Services | 31 | - | ⭐⭐⭐⭐⭐ | **DONE** |
| 🟢 P1 | Business Logic | 35-45 | 1-2 days | ⭐⭐⭐⭐⭐ | **Recommended Next** |
| 🟡 P2 | Plugin Base Classes | 36-45 | 2-3 days | ⭐⭐⭐⭐ | After P1 |
| 🟠 P3 | Integration Tests | 33-42 | 3-4 days | ⭐⭐⭐⭐⭐ | After P2 |
| 🔴 P4 | Complex Tests | 90-120 | 5-7 days | ⭐⭐⭐ | After P3 |
| 🔵 P5 | UI/E2E Tests | 50-100 | 7-10 days | ⭐⭐ | Future |

---

## 🎯 **Recommended Next Steps**

### Immediate (This Week):
1. ✅ **LLMChatSession Tests** (10-12 tests, 2 hours) - HIGH VALUE
2. ✅ **MarkdownRegexes Tests** (5-7 tests, 30 min) - QUICK WIN
3. ✅ **LLMChatMessage Tests** (8-10 tests, 1 hour) - HIGH VALUE
4. ✅ **Model Tests** (5-6 tests, 30 min) - QUICK WIN

**Total**: 28-35 tests, 4-5 hours
**New Total**: 59-66 tests (from 31)

### Next Week:
5. ✅ **PluginBase Tests** (8-10 tests, 2 hours)
6. ✅ **AIProviderPluginBase Tests** (12-15 tests, 3 hours)
7. ✅ **Database Integration Tests** (15-20 tests, 4 hours)

**Total**: 35-45 tests, 9 hours
**New Total**: 94-111 tests

### Following Weeks:
- Priority 3: Integration tests
- Priority 4: Complex plugin and ViewModel tests
- Priority 5: UI tests (if needed)

---

## 💡 **Testing Best Practices to Follow**

1. **Start with High-Value, Low-Complexity** tests (Priority 1)
2. **Maintain 80%+ pass rate** throughout
3. **Keep tests fast** (<100ms average)
4. **Mock external dependencies** (HTTP, file I/O when appropriate)
5. **Use realistic test data** (actual markdown, real file paths)
6. **Test edge cases** (null, empty, very large data)
7. **Document known limitations** (like sqlite-net-e OrderBy issue)

---

## 📈 **Expected Coverage After All Phases**

| Phase | Tests | Coverage | Pass Rate |
|-------|-------|----------|-----------|
| **Current (Phase 2)** | 31 | 45% | 93.5% |
| After P1 | 59-66 | 60% | 95%+ |
| After P2 | 94-111 | 70% | 95%+ |
| After P3 | 127-153 | 80% | 95%+ |
| After P4 | 217-273 | 90% | 95%+ |
| After P5 | 267-373 | 95%+ | 95%+ |

---

**Conclusion**: We have excellent opportunities to expand test coverage! Priority 1 tests (LLMChatSession, MarkdownRegexes, Models) offer the best value/effort ratio and should be implemented next.

