using UiAutomation;

namespace UiAutomationGRPC.Library.Selectors;

/// <summary>
/// Model for selector path segments.
/// </summary>
public class SelectorModel
{
    /// <summary>
    /// Index of element in parent (optional search).
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// Additional search property value.
    /// </summary>
    public string? AdditionalSearchProperty { get; set; }

    /// <summary>
    /// Search type (Children or Descendants).
    /// </summary>
    public SearchType? SearchType { get; set; }

    /// <summary>
    /// Conditions to match for this segment.
    /// </summary>
    public List<Condition>? Condition { get; set; }
}