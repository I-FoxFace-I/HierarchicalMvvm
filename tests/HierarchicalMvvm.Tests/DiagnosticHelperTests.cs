using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using FluentAssertions;
using HierarchicalMvvm.Generator;
using System;
using Microsoft.CodeAnalysis;
namespace HierarchicalMvvm.Tests
{
    public class DiagnosticHelperTests
    {
        [Fact]
        public void InfoDescriptor_ShouldHaveCorrectId()
        {
            // Assert
            DiagnosticHelper.InfoDescriptor.Id.Should().Be("HIER001");
            DiagnosticHelper.InfoDescriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info);
        }
        [Fact]
        public void WarningDescriptor_ShouldHaveCorrectId()
        {
            // Assert
            DiagnosticHelper.WarningDescriptor.Id.Should().Be("HIER002");
            DiagnosticHelper.WarningDescriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        }
        [Fact]
        public void ErrorDescriptor_ShouldHaveCorrectId()
        {
            // Assert
            DiagnosticHelper.ErrorDescriptor.Id.Should().Be("HIER003");
            DiagnosticHelper.ErrorDescriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        }
        [Fact]
        public void Descriptors_ShouldHaveCorrectCategory()
        {
            // Assert
            DiagnosticHelper.InfoDescriptor.Category.Should().Be("HierarchicalMvvmGenerator");
            DiagnosticHelper.WarningDescriptor.Category.Should().Be("HierarchicalMvvmGenerator");
            DiagnosticHelper.ErrorDescriptor.Category.Should().Be("HierarchicalMvvmGenerator");
        }
        [Fact]
        public void Descriptors_ShouldBeEnabled()
        {
            // Assert
            DiagnosticHelper.InfoDescriptor.IsEnabledByDefault.Should().BeTrue();
            DiagnosticHelper.WarningDescriptor.IsEnabledByDefault.Should().BeTrue();
            DiagnosticHelper.ErrorDescriptor.IsEnabledByDefault.Should().BeTrue();
        }
        [Fact]
        public void Descriptors_ShouldHaveCorrectMessageFormat()
        {
            // Assert
            DiagnosticHelper.InfoDescriptor.MessageFormat.ToString().Should().Be("{0}");
            DiagnosticHelper.WarningDescriptor.MessageFormat.ToString().Should().Be("{0}");
            DiagnosticHelper.ErrorDescriptor.MessageFormat.ToString().Should().Be("{0}");
        }
    }
}