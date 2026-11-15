using System.Text;
using System.Text.RegularExpressions;

namespace Asms.UI.Tests;

public sealed class FontAssetTests
{
    [Fact]
    public void Css_font_files_exist_and_have_valid_font_signatures()
    {
        var cssPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Asms.UI.Shared",
            "wwwroot",
            "css",
            "asms.css");
        var css = File.ReadAllText(cssPath);
        var paths = Regex.Matches(
                css,
                """url\(["']?(?<path>[^)"']+)["']?\)""",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => Uri.UnescapeDataString(match.Groups["path"].Value))
            .ToArray();

        Assert.Equal(2, paths.Length);
        foreach (var relativePath in paths)
        {
            var fontPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cssPath)!, relativePath));
            Assert.StartsWith(
                Path.Combine(FindRepositoryRoot(), "src", "Asms.UI.Shared", "wwwroot"),
                fontPath,
                StringComparison.OrdinalIgnoreCase);
            var bytes = File.ReadAllBytes(fontPath);
            Assert.NotEmpty(bytes);
            Assert.True(
                HasFontSignature(bytes),
                $"{Path.GetFileName(fontPath)} has an unknown font signature.");
        }
    }

    [Fact]
    public void Font_licence_files_are_present()
    {
        var fontsPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Asms.UI.Shared",
            "wwwroot",
            "fonts");

        Assert.True(File.Exists(Path.Combine(fontsPath, "OFL-NotoNaskhArabic.txt")));
        Assert.True(File.Exists(Path.Combine(fontsPath, "OFL-NotoSans.txt")));
    }

    [Fact]
    public void Noto_Naskh_Arabic_covers_Pashto_and_Dari_letters()
    {
        var fontPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Asms.UI.Shared",
            "wwwroot",
            "fonts",
            "NotoNaskhArabic[wght].ttf");
        var font = File.ReadAllBytes(fontPath);
        var requiredCharacters = "ټډړږښګڼۍېپچژگکی";

        foreach (var character in requiredCharacters)
        {
            Assert.True(
                ContainsGlyph(font, character),
                $"The bundled font has no glyph for U+{(int)character:X4}.");
        }
    }

    [Fact]
    public void Css_has_no_remote_imports_or_physical_direction_properties()
    {
        var cssPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Asms.UI.Shared",
            "wwwroot",
            "css",
            "asms.css");
        var css = File.ReadAllText(cssPath);
        var physicalProperties = new Regex(
            @"\b(?:margin|padding)-(?:left|right)\b|\btext-align\s*:\s*(?:left|right)\b|\bfloat\s*:\s*(?:left|right)\b|\bborder-(?:left|right)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.DoesNotContain("http", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(physicalProperties, css);
        Assert.Contains("font-weight: 400 700", css, StringComparison.Ordinal);
        Assert.Contains("font-weight: 100 900", css, StringComparison.Ordinal);
    }

    private static bool HasFontSignature(byte[] bytes) =>
        bytes.Length >= 4 &&
        ((bytes[0] == 0x00 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x00) ||
         Encoding.ASCII.GetString(bytes, 0, 4) is "true" or "wOF2");

    private static bool ContainsGlyph(byte[] font, char character)
    {
        var cmapOffset = FindTable(font, "cmap");
        var encodingCount = ReadUInt16(font, cmapOffset + 2);
        var subtableOffsets = new List<int>();

        for (var index = 0; index < encodingCount; index++)
        {
            var recordOffset = cmapOffset + 4 + index * 8;
            var subtableOffset = checked(cmapOffset + (int)ReadUInt32(font, recordOffset + 4));
            subtableOffsets.Add(subtableOffset);
        }

        foreach (var subtableOffset in subtableOffsets)
        {
            var format = ReadUInt16(font, subtableOffset);
            if (format == 12 && Format12HasGlyph(font, subtableOffset, character))
            {
                return true;
            }
        }

        return subtableOffsets.Any(
            offset => ReadUInt16(font, offset) == 4 && Format4HasGlyph(font, offset, character));
    }

    private static bool Format12HasGlyph(byte[] font, int offset, char character)
    {
        var groupCount = ReadUInt32(font, offset + 12);
        for (var index = 0; index < groupCount; index++)
        {
            var groupOffset = checked(offset + 16 + (int)index * 12);
            var startCode = ReadUInt32(font, groupOffset);
            var endCode = ReadUInt32(font, groupOffset + 4);
            var startGlyph = ReadUInt32(font, groupOffset + 8);
            if (character >= startCode && character <= endCode)
            {
                return startGlyph + character - startCode != 0;
            }
        }

        return false;
    }

    private static bool Format4HasGlyph(byte[] font, int offset, char character)
    {
        var segmentCount = ReadUInt16(font, offset + 6) / 2;
        var endCodeOffset = offset + 14;
        var startCodeOffset = endCodeOffset + segmentCount * 2 + 2;
        var deltaOffset = startCodeOffset + segmentCount * 2;
        var rangeOffset = deltaOffset + segmentCount * 2;

        for (var index = 0; index < segmentCount; index++)
        {
            var endCode = ReadUInt16(font, endCodeOffset + index * 2);
            var startCode = ReadUInt16(font, startCodeOffset + index * 2);
            if (character < startCode || character > endCode)
            {
                continue;
            }

            var delta = unchecked((short)ReadUInt16(font, deltaOffset + index * 2));
            var rangeWordOffset = rangeOffset + index * 2;
            var glyphOffset = ReadUInt16(font, rangeWordOffset);
            if (glyphOffset == 0)
            {
                return ((character + delta) & 0xFFFF) != 0;
            }

            var glyphIndexOffset = rangeWordOffset + glyphOffset + (character - startCode) * 2;
            var glyphIndex = ReadUInt16(font, glyphIndexOffset);
            return glyphIndex != 0 && ((glyphIndex + delta) & 0xFFFF) != 0;
        }

        return false;
    }

    private static int FindTable(byte[] font, string tag)
    {
        var tableCount = ReadUInt16(font, 4);
        for (var index = 0; index < tableCount; index++)
        {
            var recordOffset = 12 + index * 16;
            if (Encoding.ASCII.GetString(font, recordOffset, 4) == tag)
            {
                return checked((int)ReadUInt32(font, recordOffset + 8));
            }
        }

        throw new InvalidDataException($"The font has no {tag} table.");
    }

    private static ushort ReadUInt16(byte[] data, int offset)
    {
        EnsureRange(data, offset, 2);
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        EnsureRange(data, offset, 4);
        return ((uint)data[offset] << 24) |
            ((uint)data[offset + 1] << 16) |
            ((uint)data[offset + 2] << 8) |
            data[offset + 3];
    }

    private static void EnsureRange(byte[] data, int offset, int count)
    {
        if (offset < 0 || offset > data.Length - count)
        {
            throw new InvalidDataException("The font table is truncated.");
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Asms.UI.Shared")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
