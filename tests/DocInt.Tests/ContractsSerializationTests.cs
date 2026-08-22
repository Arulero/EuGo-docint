using System.Reflection;
using System.Text.Json;
using DocInt.Api.Contracts;

namespace DocInt.Tests;

public class ContractsSerializationTests
{
    [Fact]
    public void Kind_serializes_lowercase_and_nulls_are_omitted()
    {
        var result = new FileResult("manual.pdf", FileKind.Pdf, "# md", null, null, ["w1"], null);
        var json = JsonSerializer.Serialize(new ExtractResponse([result]), DocIntJson.Options);
        Assert.Contains("\"kind\":\"pdf\"", json);
        Assert.Contains("\"warnings\":[\"w1\"]", json);
        Assert.DoesNotContain("tables", json);
        Assert.DoesNotContain("imageDescription", json);
        Assert.DoesNotContain("error", json);
    }

    [Fact]
    public void Typed_cells_serialize_as_native_json_scalars()
    {
        var table = new TableResult("Sheet1", "| a |",
            [["Part", 40m, 19.99m, true, null]]);
        var result = new FileResult("bom.xlsx", FileKind.Xlsx, "# md", [table], null, [], null);
        var json = JsonSerializer.Serialize(result, DocIntJson.Options);
        Assert.Contains("[\"Part\",40,19.99,true,null]", json);
    }

    [Fact]
    public void Error_result_serializes_code_and_message()
    {
        var result = new FileResult("x.bin", null, null, null, null, [],
            new FileError(ErrorCodes.UnsupportedType, "could not detect a supported file type for 'x.bin'"));
        var json = JsonSerializer.Serialize(result, DocIntJson.Options);
        Assert.Contains("\"code\":\"unsupported_type\"", json);
        Assert.DoesNotContain("\"kind\"", json);
    }

    [Fact]
    public void Kind_names_are_lowercase()
    {
        Assert.Equal("pdf", FileKind.Pdf.Name());
        Assert.Equal("xlsx", FileKind.Xlsx.Name());
        Assert.Equal("image", FileKind.Image.Name());
    }

    /// <summary>
    /// The per-file error vocabulary, pinned whole. Both directions matter: a new code is a v1
    /// contract change a caller has to be told about, and a deleted one is the same in reverse.
    /// </summary>
    /// <remarks>
    /// engine_unconfigured is why this test exists. No configuration can produce it any more —
    /// both Foundry endpoints are required, so a surface the service cannot serve is a boot
    /// failure rather than a per-file error — which leaves the constant with no reference anywhere
    /// in the service. That is exactly the state in which an "unused constant" cleanup deletes it,
    /// silently narrowing the contract. It stays defined because withdrawing a code costs a caller
    /// a change and buys nothing.
    /// </remarks>
    [Fact]
    public void The_per_file_error_vocabulary_is_the_frozen_v1_set()
    {
        var declared = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["corrupt", "empty_file", "engine_error", "engine_unconfigured", "timeout", "too_large",
             "unsupported_type"],
            declared);
    }
}
