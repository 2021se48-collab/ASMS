using Asms.Domain.Students;

namespace Asms.Domain.Tests.Students;

public class StudentDuplicateKeyTests
{
    [Fact]
    public void NormalizeName_returns_empty_for_null_empty_or_whitespace()
    {
        Assert.Equal(string.Empty, StudentDuplicateKey.NormalizeName(null));
        Assert.Equal(string.Empty, StudentDuplicateKey.NormalizeName(string.Empty));
        Assert.Equal(string.Empty, StudentDuplicateKey.NormalizeName("   "));
    }

    [Fact]
    public void NormalizeName_trims_and_collapses_spaces()
    {
        Assert.Equal("ahmad khan", StudentDuplicateKey.NormalizeName("  Ahmad   Khan  "));
    }

    [Fact]
    public void NormalizeName_is_case_insensitive_for_latin_names()
    {
        Assert.Equal(
            StudentDuplicateKey.NormalizeName("Ahmad"),
            StudentDuplicateKey.NormalizeName("ahmad"));
    }

    [Fact]
    public void NormalizeName_maps_arabic_yeh_to_persian_yeh()
    {
        Assert.Equal(
            StudentDuplicateKey.NormalizeName("\u064A"),
            StudentDuplicateKey.NormalizeName("\u06CC"));
        Assert.Equal(
            StudentDuplicateKey.NormalizeName("\u0649"),
            StudentDuplicateKey.NormalizeName("\u06CC"));
    }

    [Fact]
    public void NormalizeName_maps_arabic_kaf_to_persian_kaf()
    {
        Assert.Equal(
            StudentDuplicateKey.NormalizeName("\u0643"),
            StudentDuplicateKey.NormalizeName("\u06A9"));
    }

    [Fact]
    public void NormalizeName_removes_tatweel_and_diacritics()
    {
        Assert.Equal("\u0627", StudentDuplicateKey.NormalizeName("\u0627\u0640\u064E"));
    }

    [Fact]
    public void NormalizeName_removes_zero_width_characters()
    {
        Assert.Equal("ab", StudentDuplicateKey.NormalizeName("A\u200B\u200C\u200D\uFEFFB"));
    }

    [Fact]
    public void Create_returns_the_same_key_for_spacing_and_case_differences()
    {
        var dateOfBirth = new DateOnly(2012, 4, 5);

        var firstKey = StudentDuplicateKey.Create(" Ahmad ", "Mohammad Ali", dateOfBirth);
        var secondKey = StudentDuplicateKey.Create("ahmad", "MOHAMMAD   ALI", dateOfBirth);

        Assert.Equal(firstKey, secondKey);
    }

    [Fact]
    public void Create_returns_a_different_key_for_a_different_date_of_birth()
    {
        var firstKey = StudentDuplicateKey.Create("Ahmad", "Ali", new DateOnly(2012, 4, 5));
        var secondKey = StudentDuplicateKey.Create("Ahmad", "Ali", new DateOnly(2012, 4, 6));

        Assert.NotEqual(firstKey, secondKey);
    }

    [Fact]
    public void Create_returns_a_different_key_for_a_different_father_name()
    {
        var dateOfBirth = new DateOnly(2012, 4, 5);

        var firstKey = StudentDuplicateKey.Create("Ahmad", "Ali", dateOfBirth);
        var secondKey = StudentDuplicateKey.Create("Ahmad", "Omar", dateOfBirth);

        Assert.NotEqual(firstKey, secondKey);
    }
}
