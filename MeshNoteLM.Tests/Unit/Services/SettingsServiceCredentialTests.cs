using MeshNoteLM.Services;
using System;
using System.Collections.Generic;
using Xunit;
using Moq;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Tests.Unit.Services
{
    public class SettingsServiceCredentialTests : IDisposable
    {
        private readonly Mock<IFileSystemService> _mockFileSystem;
        private readonly string _tempDirectory;
        private readonly SettingsService _settingsService;

        public SettingsServiceCredentialTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);

            _mockFileSystem = new Mock<IFileSystemService>();
            _mockFileSystem.Setup(x => x.AppDataDirectory).Returns(_tempDirectory);

            _settingsService = new SettingsService(_mockFileSystem.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
            _mockFileSystem.VerifyAll();
        }

        [Fact]
        public void SetCredential_GenericString_ShouldStoreAndRetrieve()
        {
            // Arrange
            const string key = "test-api-key";
            const string value = "test-secret-value";

            // Act
            _settingsService.SetCredential(key, value);
            var retrieved = _settingsService.GetCredential<string>(key);

            // Assert
            Assert.Equal(value, retrieved);
        }

        [Fact]
        public void SetCredential_GenericString_WithNull_ShouldRemoveCredential()
        {
            // Arrange
            const string key = "test-api-key";
            _settingsService.SetCredential(key, "initial-value");

            // Act
            _settingsService.SetCredential<string>(key, null);
            var retrieved = _settingsService.GetCredential<string>(key);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void GetCredential_NonExistentKey_ShouldReturnDefault()
        {
            // Arrange & Act
            var result = _settingsService.GetCredential<string>("non-existent-key", "default-value");

            // Assert
            Assert.Equal("default-value", result);
        }

        [Fact]
        public void SetCredential_ComplexObject_ShouldStoreAndRetrieve()
        {
            // Arrange
            var testData = new TestCredential
            {
                Username = "testuser",
                Token = "test-token",
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };
            const string key = "test-complex";

            // Act
            _settingsService.SetCredential(key, testData);
            var retrieved = _settingsService.GetCredential<TestCredential>(key);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(testData.Username, retrieved.Username);
            Assert.Equal(testData.Token, retrieved.Token);
            Assert.Equal(testData.ExpiresAt.Date, retrieved.ExpiresAt.Date);
        }

        [Fact]
        public void SetCredential_Boolean_ShouldStoreAndRetrieve()
        {
            // Arrange
            const string key = "feature-enabled";
            const bool value = true;

            // Act
            _settingsService.SetCredential(key, value);
            var retrieved = _settingsService.GetCredential<bool>(key);

            // Assert
            Assert.Equal(value, retrieved);
        }

        [Fact]
        public void SetCredential_Integer_ShouldStoreAndRetrieve()
        {
            // Arrange
            const string key = "max-retry-count";
            const int value = 5;

            // Act
            _settingsService.SetCredential(key, value);
            var retrieved = _settingsService.GetCredential<int>(key);

            // Assert
            Assert.Equal(value, retrieved);
        }

        [Fact]
        public void HasCredential_ExistingKey_ShouldReturnTrue()
        {
            // Arrange
            const string key = "existing-key";
            _settingsService.SetCredential(key, "value");

            // Act
            var result = _settingsService.HasCredential(key);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasCredential_NonExistentKey_ShouldReturnFalse()
        {
            // Act
            var result = _settingsService.HasCredential("non-existent-key");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasCredential_NullValue_ShouldReturnFalse()
        {
            // Arrange
            const string key = "null-key";
            _settingsService.SetCredential(key, "value");
            _settingsService.SetCredential<string>(key, null);

            // Act
            var result = _settingsService.HasCredential(key);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasCredentials_AllKeysExist_ShouldReturnTrue()
        {
            // Arrange
            _settingsService.SetCredential("key1", "value1");
            _settingsService.SetCredential("key2", "value2");
            _settingsService.SetCredential("key3", "value3");

            // Act
            var result = _settingsService.HasCredentials("key1", "key2", "key3");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasCredentials_OneMissingKey_ShouldReturnFalse()
        {
            // Arrange
            _settingsService.SetCredential("key1", "value1");
            _settingsService.SetCredential("key3", "value3");

            // Act
            var result = _settingsService.HasCredentials("key1", "key2", "key3");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasCredentials_EmptyArray_ShouldReturnTrue()
        {
            // Act
            var result = _settingsService.HasCredentials();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void SetCredential_ShouldPersistToFile()
        {
            // Arrange
            const string key = "persistent-key";
            const string value = "persistent-value";
            _settingsService.SetCredential(key, value);

            // Act - Create new instance to test persistence
            var newSettingsService = new SettingsService(_mockFileSystem.Object);
            var retrieved = newSettingsService.GetCredential<string>(key);

            // Assert
            Assert.Equal(value, retrieved);
        }

        [Fact]
        public void SetCredential_GenericOverwrite_ShouldUpdateValue()
        {
            // Arrange
            const string key = "overwrite-key";
            _settingsService.SetCredential(key, "initial-value");

            // Act
            _settingsService.SetCredential(key, "updated-value");
            var retrieved = _settingsService.GetCredential<string>(key);

            // Assert
            Assert.Equal("updated-value", retrieved);
        }

        [Fact]
        public void SetCredential_MultipleDifferentTypes_ShouldCoexist()
        {
            // Arrange
            const string stringKey = "string-credential";
            const string intKey = "int-credential";
            const string boolKey = "bool-credential";
            const string objectKey = "object-credential";

            var testObject = new TestCredential { Username = "test", Token = "token" };

            // Act
            _settingsService.SetCredential(stringKey, "string-value");
            _settingsService.SetCredential(intKey, 42);
            _settingsService.SetCredential(boolKey, true);
            _settingsService.SetCredential(objectKey, testObject);

            // Assert
            Assert.Equal("string-value", _settingsService.GetCredential<string>(stringKey));
            Assert.Equal(42, _settingsService.GetCredential<int>(intKey));
            Assert.True(_settingsService.GetCredential<bool>(boolKey));
            Assert.Equal("test", _settingsService.GetCredential<TestCredential>(objectKey)?.Username);
        }

        [Fact]
        public void GetCredential_TypeMismatch_ShouldReturnDefault()
        {
            // Arrange
            _settingsService.SetCredential("key", "string-value");

            // Act
            var result = _settingsService.GetCredential<int>("key", 99);

            // Assert
            Assert.Equal(99, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("special!@#$%^&*()")]
        [InlineData("key-with-dashes")]
        [InlineData("key_with_underscores")]
        public void SetCredential_SpecialKeyNames_ShouldWork(string key)
        {
            // Arrange
            const string value = "test-value";

            // Act
            _settingsService.SetCredential(key, value);
            var retrieved = _settingsService.GetCredential<string>(key);

            // Assert
            Assert.Equal(value, retrieved);
        }

        [Fact]
        public void GetCredential_LargeObject_ShouldHandleSerialization()
        {
            // Arrange
            var largeObject = new
            {
                Id = Guid.NewGuid(),
                LargeString = new string('x', 10000),
                LargeArray = new int[1000],
                NestedObject = new
                {
                    Property1 = "value1",
                    Property2 = 42,
                    Property3 = DateTime.UtcNow
                }
            };
            const string key = "large-object";

            // Act
            _settingsService.SetCredential(key, largeObject);
            var retrieved = _settingsService.GetCredential<object>(key);

            // Assert
            Assert.NotNull(retrieved);
            // Note: Full object comparison would require more complex serialization handling
        }

        [Fact]
        public void LoadSettings_ExistingFileWithGenericCredentials_ShouldRestore()
        {
            // Arrange
            var settingsData = new
            {
                ObsidianVaultPath = "/test/vault",
                ClaudeApiKey = "claude-key",
                GenericCredentials = new Dictionary<string, object?>
                {
                    ["custom-key"] = "custom-value",
                    ["numeric-key"] = 123,
                    ["bool-key"] = true,
                    ["complex-key"] = new { Service = "test", Key = "secret" }
                }
            };

            var jsonFile = Path.Combine(_tempDirectory, "settings.json");
            File.WriteAllText(jsonFile, System.Text.Json.JsonSerializer.Serialize(settingsData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            // Act - Create new instance that loads the file
            var loadedService = new SettingsService(_mockFileSystem.Object);

            // Assert
            Assert.Equal("custom-value", loadedService.GetCredential<string>("custom-key"));
            Assert.Equal(123, loadedService.GetCredential<int>("numeric-key"));
            Assert.True(loadedService.GetCredential<bool>("bool-key"));
            Assert.NotNull(loadedService.GetCredential<object>("complex-key"));
        }
    }

    // Test class for complex credential objects
    public class TestCredential
    {
        public string? Username { get; set; }
        public string? Token { get; set; }
        public DateTime ExpiresAt { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is TestCredential other)
            {
                return Username == other.Username &&
                       Token == other.Token &&
                       ExpiresAt.Date == other.ExpiresAt.Date;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Username, Token, ExpiresAt.Date);
        }
    }
}