using System.Collections.Generic;
using System.Windows.Automation;

namespace UiAutomationGRPC.Client
{
    public enum SearchType
    {
        Children, Descendants
    }

    public class Selector : BaseSelector
    {
        private SelectorModel _selector;
        private SearchType _searchType;
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
        /// This method search child with index in parent element
        /// </summary>
        /// <param name="index">Index of child element</param>
        /// <returns>ChildActions</returns>
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
        /// This method search child with name
        /// </summary>
        /// <param name="name">Name of child element</param>
        /// <returns>ChildActions</returns>
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
        /// This method search child with name
        /// </summary>
        /// <param name="name">Name of child element</param>
        /// <returns>ChildActions</returns>
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
        /// This method search Descendants with conditions
        /// </summary>
        /// <param name="propertyConditions">Conditions of Descendants element</param>
        /// <returns>IndexNameBuild</returns>
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
        /// This method search Children with conditions
        /// </summary>
        /// <param name="propertyConditions">Conditions of Children element</param>
        /// <returns></returns>
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

    public class ChildActions : BaseSelector
    {
        private SelectorModel _selector;
        private SearchType _searchType;

        public ChildActions(List<SelectorModel> list, SelectorModel selector, SearchType searchType)
        {
            List = list;
            _selector = selector;
            _searchType = searchType;
        }

        /// <summary>
        /// This method search Descendants with conditions
        /// </summary>
        /// <param name="propertyConditions">Conditions of Descendants element</param>
        /// <returns>IndexNameBuild</returns>
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
        /// This method search Children with conditions
        /// </summary>
        /// <param name="propertyConditions">Conditions of Children element</param>
        /// <returns></returns>
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

    public class MainActions : BaseSelector
    {
        private SelectorModel _selector;
        private SearchType _searchType;
        public MainActions(List<SelectorModel> list, SelectorModel selector, SearchType searchType)
        {
            List = list;
            _selector = selector;
            _searchType = searchType;
        }

        /// <summary>
        /// This method search Descendants with conditions
        /// </summary>
        /// <param name="propertyConditions">Conditions of Descendants element</param>
        /// <returns>IndexNameBuild</returns>
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
        /// This method search Children with conditions
        /// </summary>
        /// <param name="propertyConditions">Conditions of Children element</param>
        /// <returns></returns>
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
        /// This method search child with index in parent element
        /// </summary>
        /// <param name="index">Index of child element</param>
        /// <returns>ChildActions</returns>
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
        /// This method search child with name
        /// </summary>
        /// <param name="name">Name of child element</param>
        /// <returns>ChildActions</returns>
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
