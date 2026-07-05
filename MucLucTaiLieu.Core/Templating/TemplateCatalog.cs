using MucLucTaiLieu.Core.Models;
using MucLucTaiLieu.Core.Seed;

namespace MucLucTaiLieu.Core.Templating;

/// <summary>The 4 built-in templates (mota3 §5); lookup + input-variable helpers for the UI.</summary>
public sealed class TemplateCatalog
{
    private readonly List<TemplateDef> _templates;

    public TemplateCatalog(IEnumerable<TemplateDef> templates) => _templates = templates.ToList();

    /// <summary>Build from the extracted seed (App/Assets/seed/templates.json).</summary>
    public static TemplateCatalog FromSeed(SeedStore seed) => new(seed.LoadTemplates());

    public IReadOnlyList<TemplateDef> All => _templates;

    /// <summary>Get a template by id, falling back to the first one for unknown ids.</summary>
    public TemplateDef Get(string id) =>
        _templates.FirstOrDefault(t => t.Id == id) ?? _templates[0];

    /// <summary>Number of Excel-sourced (non-auto) variables for a template.</summary>
    public int InputVarCount(string id) => Get(id).InputVars.Count();
}
