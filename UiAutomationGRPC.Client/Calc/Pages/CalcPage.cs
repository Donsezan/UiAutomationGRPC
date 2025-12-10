using System;
using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Locators;
using UiAutomationGRPC.Library.Helpers;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public class CalcPage : BasePageObject<CalcPage>
    {    
        private readonly CalcPageLocators _locators;
        public CalcPage(UiAutomationDriver driver) : base(driver)
        {
            // Optional: Wait for the app to be ready in constructor
            _driver = driver;
            _locators = new CalcPageLocators(driver);
            _locators.ResultText.WaitForElementExist();
        }

        public CalcPage ClickTwo()
        {
            _locators.ButtonTwo.Click();
            return this;
        }

        public CalcPage ClickPlus()
        {
            _locators.ButtonPlus.Click();
            return this;
        }

        public CalcPage ClickEqual()
        {
            _locators.ButtonEqual.Click();
            return this;
        }

        public string GetResult()
        {
            return _locators.ResultText.Name();
        }

        public CalcNavigationPaget<CalcPage> ClickNavigationButton()
        {
            _locators.ResultText.Name();
            return new CalcNavigationPaget<CalcPage>(_driver, this);
        }

        public CalcPage ClickResultText()
        {
            _locators.ResultText.Click();
            return this;
        }

        public CalcPage SendKey(string key) {
            KeyboardHelper.SendKey(key);
            return this;
        }

    }

    public class CalcPageLocators
    {
        private readonly UiAutomationDriver _driver;

        public CalcPageLocators(UiAutomationDriver driver) => _driver = driver;

        private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiAutomationAdapter(_driver, selector);

        private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

        private IAutomationElement E(string automationId) =>
            CreateElement(() => Window.Descendants(new PropertyConditions().AutomationIdProperty(automationId)));

        public IAutomationElement ButtonOne => E("num1Button");
        public IAutomationElement ButtonTwo => E("num2Button");
        public IAutomationElement ButtonPlus => E("plusButton");
        public IAutomationElement ButtonEqual => E("equalButton");
        public IAutomationElement ResultText => E("CalculatorResults");
        public IAutomationElement NavigationButton => CreateElement(() => Window.Descendants().ControlType("Button").NameContain("Close Navigation"));


}
}
