using Newtonsoft.Json;

namespace UiAutomationGRPC.Server.Models
{
    public class AppNode
    {
        public string UniqId { get; set; }
        public string UiAutomationId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ControlType { get; set; }
        public string BoundingRectangle { get; set; }
        public bool IsClickable { get; set; }
        public bool IsVisible { get; set; }

        /// <summary>
        /// Set when this node's children were cut off by the depth or node-count cap.
        /// Serialized only when true so the LLM knows the subtree is incomplete.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool ChildrenTruncated { get; set; }

        public List<AppNode> Children { get; set; } = new List<AppNode>();

        // ShouldSerialize* trims noise fields from the JSON shipped to the model.
        // Empty/blank strings and empty child lists are omitted entirely.
        public bool ShouldSerializeUiAutomationId() => !string.IsNullOrEmpty(UiAutomationId);
        public bool ShouldSerializeName() => !string.IsNullOrEmpty(Name);
        public bool ShouldSerializeDescription() => !string.IsNullOrEmpty(Description);
        public bool ShouldSerializeBoundingRectangle() => !string.IsNullOrEmpty(BoundingRectangle);
        public bool ShouldSerializeChildren() => Children != null && Children.Count > 0;
    }
}
