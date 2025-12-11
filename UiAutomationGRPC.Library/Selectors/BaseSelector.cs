using System.Collections.Generic;

namespace UiAutomationGRPC.Library.Selectors
{
    /// <summary>
    /// Base class for selectors, holding the list of selector models.
    /// </summary>
    public abstract class BaseSelector
    {
        /// <summary>
        /// List of selector models.
        /// </summary>
        protected List<SelectorModel> List = new List<SelectorModel>();
        
        /// <summary>
        /// Gets the list of selectors.
        /// </summary>
        /// <returns>The list of selector models.</returns>
        public List<SelectorModel> GetSelectors()
        {
            return List;
        }
    }
}