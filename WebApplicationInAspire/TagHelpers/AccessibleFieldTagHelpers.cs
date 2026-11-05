using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebApplicationInAspire.TagHelpers;

/// <summary>Knytter felter til deres feilmelding også når JavaScript er slått av.</summary>
[HtmlTargetElement("input", Attributes = "asp-for")]
[HtmlTargetElement("select", Attributes = "asp-for")]
[HtmlTargetElement("textarea", Attributes = "asp-for")]
public class AccessibleFieldTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")] public ModelExpression For { get; set; } = null!;
    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = null!;
    public override int Order => 1000;
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes["type"]?.Value?.ToString() is "hidden" or "checkbox") return;
        var name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        var id = name.Replace(".", "_");
        var described = output.Attributes["aria-describedby"]?.Value?.ToString() ?? "";
        output.Attributes.SetAttribute("aria-describedby", string.Join(" ", (described + " " + id + "-error").Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct()));
        if (For.Metadata.IsRequired && For.ModelExplorer.ModelType != typeof(bool)) output.Attributes.SetAttribute("aria-required", "true");
        if (ViewContext.ViewData.ModelState.TryGetValue(name, out var state) && state.Errors.Count > 0) output.Attributes.SetAttribute("aria-invalid", "true");
    }
}

[HtmlTargetElement("span", Attributes = "asp-validation-for")]
public class AccessibleValidationTagHelper : TagHelper
{
    [HtmlAttributeName("asp-validation-for")] public ModelExpression For { get; set; } = null!;
    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = null!;
    public override int Order => 1000;
    public override void Process(TagHelperContext context, TagHelperOutput output)
        => output.Attributes.SetAttribute("id", ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name).Replace(".", "_") + "-error");
}
