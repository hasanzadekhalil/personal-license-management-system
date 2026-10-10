using FluentAssertions;
using LicenseSystem.ClientSdk.Hardware;
using Xunit;

namespace LicenseSystem.Tests.Acceptance;

public class HardwareFingerprintTests
{
    [Fact]
    public void CompositeHash_ShouldBeDeterministicAndRepeatable()
    {
        // Arrange
        var provider = new MockHardwareFingerprintProvider();

        // Act
        var hash1 = provider.GetCompositeHash();
        var hash2 = provider.GetCompositeHash();

        // Assert
        hash1.Should().NotBeNullOrEmpty();
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void MotherboardReplacement_ShouldAlterCompositeHash_AT06()
    {
        // Arrange
        var provider = new MockHardwareFingerprintProvider();
        var originalHash = provider.GetCompositeHash();

        // Act
        provider.ReplaceMotherboard("NEW-REPLACED-MOTHERBOARD-UUID-999");
        var newHash = provider.GetCompositeHash();

        // Assert (AT-06 condition)
        newHash.Should().NotBe(originalHash);
    }

    [Fact]
    public void CpuReplacement_ShouldAlterCompositeHash_AT07()
    {
        // Arrange
        var provider = new MockHardwareFingerprintProvider();
        var originalHash = provider.GetCompositeHash();

        // Act
        provider.ReplaceCpu("NEW-REPLACED-CPU-I9-14900K");
        var newHash = provider.GetCompositeHash();

        // Assert (AT-07 condition)
        newHash.Should().NotBe(originalHash);
    }

    [Fact]
    public void PrimaryDiskReplacement_ShouldAlterCompositeHash_AT08()
    {
        // Arrange
        var provider = new MockHardwareFingerprintProvider();
        var originalHash = provider.GetCompositeHash();

        // Act
        provider.ReplacePrimaryDisk("NEW-REPLACED-SSD-SAMSUNG-990-PRO");
        var newHash = provider.GetCompositeHash();

        // Assert (AT-08 condition)
        newHash.Should().NotBe(originalHash);
    }

    [Fact]
    public void CleanIdentifier_ShouldFilterGenericVendorStrings()
    {
        // Assert
        HardwareFingerprintHelper.CleanIdentifier("To be filled by O.E.M.", "FALLBACK").Should().Be("FALLBACK");
        HardwareFingerprintHelper.CleanIdentifier("Default string", "FALLBACK").Should().Be("FALLBACK");
        HardwareFingerprintHelper.CleanIdentifier("None", "FALLBACK").Should().Be("FALLBACK");
        HardwareFingerprintHelper.CleanIdentifier("   ", "FALLBACK").Should().Be("FALLBACK");
        HardwareFingerprintHelper.CleanIdentifier("VALID-SERIAL-123", "FALLBACK").Should().Be("VALID-SERIAL-123");
    }
}
