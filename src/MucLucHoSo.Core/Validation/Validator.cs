using MucLucHoSo.Core.Grouping;
using MucLucHoSo.Core.Models;
using MucLucHoSo.Core.Reading;
using MucLucHoSo.Core.Templating;

namespace MucLucHoSo.Core.Validation;

/// <summary>Kiểm tra dữ liệu + mapping mà KHÔNG sinh tài liệu (một lượt streaming).</summary>
public static class Validator
{
    public static ValidationResult Run(IRowReader reader, MappingConfig map, RuntimeTemplate tpl)
    {
        var res = new ValidationResult();

        // cột gom nhóm tồn tại?
        if (!reader.Headers.Contains(map.GroupColumn))
            res.Errors.Add($"Không thấy cột gom nhóm '{map.GroupColumn}'.");

        // mọi biến của template đã được bind (cột/hằng/auto)?
        foreach (var v in tpl.RowFields.Concat(tpl.HeaderFields))
            if (map.Find(v) is null)
                res.Errors.Add($"Biến '{v}' chưa được ghép (cột hoặc hằng).");

        // cột được map phải có trong header
        foreach (var b in map.Bindings)
            if (b.Kind == BindingKind.Column && b.Column is not null && !reader.Headers.Contains(b.Column))
                res.Errors.Add($"Biến '{b.Variable}' map tới cột '{b.Column}' không tồn tại.");

        try
        {
            var engine = new GroupEngine(map);
            foreach (var job in engine.Group(reader.ReadRows()))
            {
                res.TotalGroups++;
                res.TotalRows += job.Rows.Count;
                if (job.Rows.Count > res.LargestGroupSize)
                { res.LargestGroupSize = job.Rows.Count; res.LargestGroupKey = job.GroupKey; }
            }
        }
        catch (NonConsecutiveGroupException ex)
        {
            res.Consecutive = false;
            res.Errors.Add(ex.Message);
        }
        return res;
    }
}
