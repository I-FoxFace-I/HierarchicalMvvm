using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using FluentAssertions;
using HierarchicalMvvm.Generator;

namespace HierarchicalMvvm.Tests
{

    public class StringHelpersTests
    {
        [Theory]
        [InlineData("camelCase", true)]
        [InlineData("PascalCase", false)]
        [InlineData("snake_case", false)]
        [InlineData("UPPER_CASE", false)]
        [InlineData("", false)]
        public void IsCamelCase_ShouldWorkCorrectly(string input, bool expected)
        {
            // Act
            var result = StringHelpers.IsCamelCase(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("PascalCase", true)]
        [InlineData("camelCase", false)]
        [InlineData("snake_case", false)]
        [InlineData("UPPER_CASE", false)]
        [InlineData("", false)]
        public void IsTitleCase_ShouldWorkCorrectly(string input, bool expected)
        {
            // Act
            var result = StringHelpers.IsTitleCase(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("snake_case", true)]
        [InlineData("camelCase", false)]
        [InlineData("PascalCase", false)]
        [InlineData("UPPER_CASE", false)]
        [InlineData("", false)]
        public void IsSnakeCase_ShouldWorkCorrectly(string input, bool expected)
        {
            // Act
            var result = StringHelpers.IsSnakeCase(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("PascalCase", "pascalCase")]
        [InlineData("snake_case", "snakeCase")]
        [InlineData("UPPER_CASE", "upperCase")]
        public void ToCamelCase_ShouldWorkCorrectly(string input, string expected)
        {
            // Act
            var result = StringHelpers.ToCamelCase(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("camelCase", "CamelCase")]
        [InlineData("snake_case", "SnakeCase")]
        [InlineData("UPPER_CASE", "UpperCase")]
        public void ToTileCase_ShouldWorkCorrectly(string input, string expected)
        {
            // Act
            var result = StringHelpers.ToTileCase(input);

            // Assert
            result.Should().Be(expected);
        }
    }
} 