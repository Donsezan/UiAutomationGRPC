using System;
using System.Drawing;
using System.Windows.Automation;
using System.Xml.Linq;
using UiAutomation;
using UiAutomationGRPC.Client.Framework;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public class CalcPage : BasePageObject<CalcPage>
    {
        private readonly UiAutomationService.UiAutomationServiceClient _client;
        private readonly CalcPageLocators _locators;
        public CalcPage(UiAutomationService.UiAutomationServiceClient client)
        {
            // Optional: Wait for the app to be ready in constructor
            _client = client;
            _locators = new CalcPageLocators(client);
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
            return new CalcNavigationPaget<CalcPage>(_client, this);
        }
    }

    public class CalcPageLocators
    {
        private readonly UiAutomationService.UiAutomationServiceClient _client;

        public CalcPageLocators(UiAutomationService.UiAutomationServiceClient client) => _client = client;

        private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiAutomationAdapter(_client, selector);

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
