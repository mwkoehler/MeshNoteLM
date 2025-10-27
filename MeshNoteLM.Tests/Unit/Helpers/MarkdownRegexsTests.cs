using MeshNoteLM.Helpers;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Helpers;

public class MarkdownRegexsTests
{
    [Theory]
    [InlineData("1. First item", true)]
    [InlineData("2. Second item", true)]
    [InlineData("10. Tenth item", true)]
    [InlineData("999. Large number item", true)]
    public void NumberedListItemRegex_ShouldMatch_ValidNumberedListItems(string input, bool shouldMatch)
    {
        // Arrange
        var regex = MarkdownRegexes.NumberedListItemRegex();

        // Act
        var match = regex.IsMatch(input);

        // Assert
        match.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("  1. Indented item", true)]
    [InlineData("\t1. Tab indented item", true)]
    [InlineData("    1. Multiple spaces item", true)]
    public void NumberedListItemRegex_ShouldMatch_IndentedNumberedListItems(string input, bool shouldMatch)
    {
        // Arrange
        var regex = MarkdownRegexes.NumberedListItemRegex();

        // Act
        var match = regex.IsMatch(input);

        // Assert
        match.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("1.Item without space", false)]
    [InlineData("1 Item without period", false)]
    [InlineData("Item without number", false)]
    [InlineData("- Bullet point", false)]
    [InlineData("* Asterisk bullet", false)]
    [InlineData("", false)]
    [InlineData("Just text", false)]
    public void NumberedListItemRegex_ShouldNotMatch_InvalidPatterns(string input, bool shouldMatch)
    {
        // Arrange
        var regex = MarkdownRegexes.NumberedListItemRegex();

        // Act
        var match = regex.IsMatch(input);

        // Assert
        match.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("1.  Double space item", true)]
    [InlineData("1.   Triple space item", true)]
    public void NumberedListItemRegex_ShouldMatch_MultipleSpacesAfterPeriod(string input, bool shouldMatch)
    {
        // Arrange
        var regex = MarkdownRegexes.NumberedListItemRegex();

        // Act
        var match = regex.IsMatch(input);

        // Assert
        match.Should().Be(shouldMatch);
    }

    [Fact]
    public void NumberedListItemRegex_ShouldMatch_AtStartOfString()
    {
        // Arrange
        var regex = MarkdownRegexes.NumberedListItemRegex();
        var input = "1. Item at start\nNot a list item";

        // Act
        var match = regex.Match(input);

        // Assert
        match.Success.Should().BeTrue();
        match.Index.Should().Be(0, "regex should match at the start of the string");
    }

    [Fact]
    public void NumberedListItemRegex_ShouldReturnSameInstance()
    {
        // Act
        var regex1 = MarkdownRegexes.NumberedListItemRegex();
        var regex2 = MarkdownRegexes.NumberedListItemRegex();

        // Assert
        regex1.Should().BeSameAs(regex2, "source-generated regex should return the same instance");
    }

    [Theory]
    [InlineData("Some text\n1. List item", false)]
    [InlineData("Text 1. with number", false)]
    public void NumberedListItemRegex_ShouldNotMatch_NumberInMiddleOfText(string input, bool shouldMatch)
    {
        // Arrange
        var regex = MarkdownRegexes.NumberedListItemRegex();

        // Act
        var match = regex.IsMatch(input);

        // Assert
        match.Should().Be(shouldMatch, "regex uses ^ anchor so should only match at start");
    }
}
