# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AINotes is a cross-platform .NET MAUI application targeting .NET 9.0 for Android, iOS, macOS, and Windows. The app provides a note-taking interface with AI assistant integration and supports multiple storage backends through a plugin architecture.

## Build and Development Commands

Build the project:
```bash
dotnet build AINotes.sln
```

Run tests:
```bash
dotnet test AINotes.Tests/AINotes.Tests.csproj
```

For detailed testing information, see [TESTING_GUIDE.md](TESTING_GUIDE.md).

Run for specific platforms:
```bash
# Android
dotnet build AINotes/AINotes.csproj -t:Run -f net9.0-android

# iOS
dotnet build AINotes/AINotes.csproj -t:Run -f net9.0-ios

# macOS
dotnet build AINotes/AINotes.csproj -t:Run -f net9.0-maccatalyst

# Windows
dotnet build AINotes/AINotes.csproj -t:Run -f net9.0-windows10.0.19041.0
```

Clean build artifacts:
```bash
dotnet clean AINotes.sln
```

## Architecture

### Plugin System

The application uses a plugin architecture with two primary plugin types:

1. **ILLMClient Plugins** (AINotes/Interfaces/ILLMClient.cs): AI provider integrations
   - Abstraction for LLM providers (OpenAI, Anthropic Claude, Gemini, Grok, etc.)
   - Supports both synchronous (`SendAsync`) and streaming (`StreamAsync`) chat
   - Provider-agnostic `ChatOptions` with model, temperature, system prompt, and context
   - Example implementations: ClaudePlugin.cs, GeminiPlugin.cs, OpenAIPlugin.cs

2. **IFileSystemPlugin** (AINotes/Interfaces/IFileSystemPlugin.cs): Storage backend integrations
   - Abstraction for file storage (local filesystem, Google Drive, cloud services)
   - Standard file operations: read, write, delete, directory navigation
   - Example implementations: GoogleDrivePlugin.cs, AndroidLocalPlugin.cs, iPhoneLocalPlugin.cs

All plugins inherit from `PluginBase` (AINotes/Plugins/PluginBase.cs) which implements the core `IPlugin` interface with properties: Name, Version, Description, Author, IsEnabled.

### Plugin Loading

Plugins are auto-discovered and loaded at runtime (AINotes/Plugins/PluginManager.cs):
- `PluginManager.LoadPluginsAsync()` scans the assembly for IPlugin implementations
- Called during `App.OnStart()` (AINotes/App.xaml.cs:66)
- Plugins can be enabled/disabled dynamically

**To add a new plugin:**
1. Create a class inheriting from `PluginBase` and implementing either `ILLMClient` or `IFileSystemPlugin`
2. Override abstract properties (Name, Version, Description)
3. Implement `InitializeAsync()` for any setup logic
4. The plugin will be automatically discovered on next app start

### Dependency Injection

Services are registered in `MauiProgram.cs` (AINotes/MauiProgram.cs):
- `PluginManager`: Singleton for plugin lifecycle management
- `IAppDatabase` → `AppDatabase`: SQLite database connection
- `INoteService` → `NoteService`: Note CRUD operations
- `IChatService` → `ChatService`: Chat message operations
- ViewModels and Pages are registered as Transient

Access services from anywhere using `AppServices.Services` (AINotes/Services/AppServices.cs).

### Data Layer

SQLite database (AINotes/Services/AppDatabase.cs):
- Database file: `{FileSystem.AppDataDirectory}/ainotes.db`
- Tables: `NoteModel`, `ChatModel`
- Initialized via `AppInitializer` during app startup
- Uses `sqlite-net-e` package for async operations

Models (AINotes/Models/):
- `NoteModel`: Stores individual notes with Id, Text, Timestamp
- `ChatModel`: Stores chat messages with Sender, Message, NoteId, Timestamp
- `SenderModel`: Represents message sender information

### UI Architecture

MVVM pattern using CommunityToolkit.Mvvm:
- ViewModels (AINotes/ViewModels/): Inherit from `ObservableObject`, use `[ObservableProperty]` attributes
- Views (AINotes/Views/): XAML pages bound to ViewModels via `x:DataType`
- Main layout: Three-pane interface (AINotes/Controls/ThreePaneControl.cs)
  - Pane 1: File tree (SourcesTreeViewModel) displaying filesystem plugins
  - Pane 2: Note list
  - Pane 3: Note editor (NoteEditorPage)

### File Tree System

The file tree (AINotes/ViewModels/SourcesTreeViewModel.cs):
- Queries `PluginManager` for all enabled `IFileSystemPlugin` instances
- Creates `FileSystemSource` wrappers for each plugin
- Uses `TreeBuilder` (AINotes/Helpers/TreeBuilder.cs) to build lazy-loaded tree nodes
- `TreeNodeViewModel` supports async children loading via factory pattern
- Directories expand on-demand to avoid loading entire filesystem upfront

### Key Files

- `MauiProgram.cs`: DI configuration, app builder
- `App.xaml.cs`: App lifecycle, plugin loading, exception handling
- `AppShell.xaml`: Navigation structure
- `Plugins/PluginManager.cs`: Plugin discovery and management
- `Interfaces/ILLMClient.cs`: LLM provider abstraction with full documentation
- `Interfaces/IFileSystemPlugin.cs`: Storage abstraction
- `Services/AppDatabase.cs`: SQLite database initialization
- `Helpers/TreeBuilder.cs`: Recursive tree building for file explorers

### Platform-Specific Code

Platform-specific implementations are in `AINotes/Platforms/` with conditional compilation:
- Android-specific: `#if ANDROID`
- iOS-specific: `#if IOS`
- Windows-specific: `#if WINDOWS`

The codebase targets API levels:
- Android: 35.0+
- iOS/macOS: 15.0+
- Windows: 10.0.17763.0+

## Important Patterns

### Adding an LLM Provider Plugin

1. Create new file in `AINotes/Plugins/` (e.g., `NewLLMPlugin.cs`)
2. Implement `PluginBase` and `ILLMClient`
3. Implement `CreateChatAsync()`, `AskAsync()`, `GetModelsAsync()`
4. Map `ChatOptions` to provider-specific API parameters
5. Handle streaming via `IAsyncEnumerable<LlmStreamChunk>`
6. See `ClaudePlugin.cs` for a complete reference implementation

### Adding a Storage Plugin

1. Create new file in `AINotes/Plugins/` (e.g., `NewStoragePlugin.cs`)
2. Implement `PluginBase` and `IFileSystemPlugin`
3. Implement all file/directory operations
4. Cache file IDs/paths for performance (see GoogleDrivePlugin.cs)
5. The plugin will automatically appear in the Sources tree view

### Exception Handling

The app has comprehensive exception handlers (AINotes/App.xaml.cs and MauiProgram.cs):
- `AppDomain.FirstChanceException`: Logs all exceptions immediately
- `AppDomain.UnhandledException`: Catches unhandled exceptions
- `TaskScheduler.UnobservedTaskException`: Catches async exceptions
- Platform-specific handlers for Android

All exceptions are logged to debug output for troubleshooting.
