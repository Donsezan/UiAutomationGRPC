using System.Collections.Generic;
using UiAutomation;

namespace UiAutomationGRPC.Library.Selectors
{
    /// <summary>
    /// Search types for UI elements.
    /// </summary>
    public enum SearchType
    {
        /// <summary>
        /// Search only immediate children.
        /// </summary>
        Children,
        
        /// <summary>
        /// Search all descendants.
        /// </summary>
        Descendants
    }

    /// <summary>
    /// Builder for selector paths.
    /// </summary>
    public class Selector : BaseSelector
    {
        private SelectorModel _selector;
        private SearchType _searchType;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Selector"/> class with initial conditions.
        /// </summary>
        /// <param name="propertyConditions">The initial conditions.</param>
        public Selector(PropertyConditions propertyConditions)
        {
            _searchType = SearchType.Descendants;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = propertyConditions.Condition
            };
            List.Add(_selector);
        }

        /// <summary>
        /// Search child with index in parent element.
        /// </summary>
        /// <param name="index">Index of child element.</param>
        /// <returns>ChildActions for further building.</returns>
        public ChildActions Index(int index)
        {
            var previesSelector = _selector;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = previesSelector.Condition,
                AdditionalSearchProperty = index
            };
            List.Add(_selector);
            return new ChildActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Search child containing name.
        /// </summary>
        /// <param name="name">Name part of child element.</param>
        /// <returns>ChildActions for further building.</returns>
        public ChildActions NameContain(string name)
        {
            var previesSelector = _selector;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = previesSelector.Condition,
                AdditionalSearchProperty = name
            };
            List.Add(_selector);
            return new ChildActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Search child with control type.
        /// </summary>
        /// <param name="type">Control type of child element.</param>
        /// <returns>ChildActions for further building.</returns>
        public ChildActions ControlType(string type)
        {
            var previesSelector = _selector;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = previesSelector.Condition,
                AdditionalSearchProperty = type
            };
            List.Add(_selector);
            return new ChildActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Search Descendants with conditions.
        /// </summary>
        /// <param name="propertyConditions">Conditions of Descendants element.</param>
        /// <returns>MainActions for further building.</returns>
        public MainActions Descendants(PropertyConditions propertyConditions)
        {
            _searchType = SearchType.Descendants;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = propertyConditions.Condition
            };
            List.Add(_selector);
            return new MainActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Search Children with conditions.
        /// </summary>
        /// <param name="propertyConditions">Conditions of Children element.</param>
        /// <returns>MainActions for further building.</returns>
        public MainActions Children(PropertyConditions propertyConditions)
        {
            _searchType = SearchType.Children;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = propertyConditions.Condition
            };
            List.Add(_selector);
            return new MainActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Starts a fluent selector for Descendants.
        /// </summary>
        /// <returns>SelectorFluentContext.</returns>
        public SelectorFluentContext Descendants()
        {
            _searchType = SearchType.Descendants;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = new List<Condition>()
            };
            List.Add(_selector);
            return new SelectorFluentContext(List, _selector);
        }

        /// <summary>
        /// Starts a fluent selector for Children.
        /// </summary>
        /// <returns>SelectorFluentContext.</returns>
        public SelectorFluentContext Children()
        {
            _searchType = SearchType.Children;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = new List<Condition>()
            };
            List.Add(_selector);
            return new SelectorFluentContext(List, _selector);
        }
    }

    /// <summary>
    /// Builder for child actions.
    /// </summary>
    public class ChildActions : BaseSelector
    {
        private SelectorModel _selector;
        private SearchType _searchType;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChildActions"/> class.
        /// </summary>
        public ChildActions(List<SelectorModel> list, SelectorModel selector, SearchType searchType)
        {
            List = list;
            _selector = selector;
            _searchType = searchType;
        }

        /// <summary>
        /// Search Descendants with conditions.
        /// </summary>
        public MainActions Descendants(PropertyConditions propertyConditions)
        {
            _searchType = SearchType.Descendants;
            _selector = new SelectorModel
            {
                SearchType = SearchType.Descendants,
                Condition = propertyConditions.Condition
            };
            List.Add(_selector);
            return new MainActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Search Children with conditions.
        /// </summary>
        public MainActions Children(PropertyConditions propertyConditions)
        {
            _searchType = SearchType.Children;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = propertyConditions.Condition
            };
            List.Add(_selector);
            return new MainActions(List, _selector, _searchType);
        }
    }

    /// <summary>
    /// Builder for main actions.
    /// </summary>
    public class MainActions : BaseSelector
    {
        private SelectorModel _selector;
        private SearchType _searchType;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainActions"/> class.
        /// </summary>
        public MainActions(List<SelectorModel> list, SelectorModel selector, SearchType searchType)
        {
            List = list;
            _selector = selector;
            _searchType = searchType;
        }

        /// <summary>
        /// Search Descendants with conditions.
        /// </summary>
        public MainActions Descendants(PropertyConditions propertyConditions)
        {
            _searchType = SearchType.Descendants;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = propertyConditions.Condition
            };
            List.Add(_selector);
            return new MainActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Search Children with conditions.
        /// </summary>
        public MainActions Children(PropertyConditions propertyConditions)
        {
            _searchType = SearchType.Children;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = propertyConditions.Condition
            };
            List.Add(_selector);
            return new MainActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Search child with index in parent element.
        /// </summary>
        public ChildActions Index(int index)
        {
            var previesSelector = _selector;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = previesSelector.Condition,
                AdditionalSearchProperty = index
            };
            List.Add(_selector);
            return new ChildActions(List, _selector, _searchType);
        }

        /// <summary>
        /// Search child with name.
        /// </summary>
        public ChildActions NameContain(string name)
        {
            var previesSelector = _selector;
            _selector = new SelectorModel
            {
                SearchType = _searchType,
                Condition = previesSelector.Condition,
                AdditionalSearchProperty = name
            };
            List.Add(_selector);
            return new ChildActions(List, _selector, _searchType);
        }
    }

}
