using System.Collections.Generic;
using UiAutomation;

namespace UiAutomationGRPC.Library.Selectors
{
    /// <summary>
    /// Model representing a single selector step.
    /// </summary>
    public class SelectorModel
    {
        /// <summary>
        /// Additional search property (e.g. index or name).
        /// </summary>
        public object AdditionalSearchProperty { get; set; }
        
        /// <summary>
        /// List of conditions for this selector.
        /// </summary>
        internal List<Condition> Condition { get; set; }
        
        /// <summary>
        /// The search scope/type (Children or Descendants).
        /// </summary>
        public SearchType? SearchType { get; set; }
    }
}