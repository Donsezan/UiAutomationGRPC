using System.Collections.Generic;

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
        public List<AppNode> Children { get; set; } = new List<AppNode>();
    }
}
