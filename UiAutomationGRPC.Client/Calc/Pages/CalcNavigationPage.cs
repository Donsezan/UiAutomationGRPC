using System;
using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Selectors;
using UiAutomationGRPC.Library.Elements;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public class CalcNavigationPaget<TPage> : BasePageObject<TPage> where TPage : BasePageObject<TPage>
    {
        private readonly TPage _previousPage;
        private readonly CalcNavigationPagetLocators _locators;
        public CalcNavigationPaget(UiAutomationDriver driver, TPage previousPage) : base(driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _previousPage = previousPage;
            _locators = new CalcNavigationPagetLocators(driver);
            // Optional: Wait for the app to be ready in constructor
            _locators.ButtonSettings.WaitForElementExist();
        }

        public CalcSettingsPage<TPage> ClickSettings()
        {
            _locators.ButtonSettings.Click();
            return new CalcSettingsPage<TPage>(_driver, _previousPage);
        }
        public TPage ClickNavigationButton()
        {
            _locators.ButtonNavigation.Click();
            return _previousPage;
        }

    }
    public class CalcNavigationPagetLocators
    {
        private readonly UiAutomationDriver _driver;

        public CalcNavigationPagetLocators(UiAutomationDriver driver) => _driver = driver;

        private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiElement(_driver, selector);

        private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

        private IAutomationElement Element(string automationId) =>
            CreateElement(() => Window.Descendants(new PropertyConditions().AutomationIdProperty(automationId)));

        public IAutomationElement ButtonSettings => Element("num1Button");
        public IAutomationElement ButtonNavigation => Element("GlobalNavButton");

    }
}
