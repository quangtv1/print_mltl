namespace MucLucHoSo.Core.Models;

/// <summary>Cấu hình ghép biến + cột gom nhóm cho một lần chạy.</summary>
public sealed class MappingConfig
{
    public required string GroupColumn { get; init; }
    public required IReadOnlyList<VariableBinding> Bindings { get; init; }
    public bool GroupCaseSensitive { get; init; } = true;

    public VariableBinding? Find(string variable) =>
        Bindings.FirstOrDefault(b => b.Variable == variable);
}
