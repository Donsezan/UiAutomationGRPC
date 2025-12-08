using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using UiAutomation;
using UiAutomationGRPC.Client.Framework;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public class CalcNavigationPaget<TPage> where TPage : BasePageObject<TPage>
    {
        private readonly TPage _previousPage;
        private readonly UiAutomationService.UiAutomationServiceClient _client;
        private readonly CalcNavigationPagetLocators _locators;
        public CalcNavigationPaget(UiAutomationService.UiAutomationServiceClient client, TPage previousPage)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _previousPage = previousPage;
            _locators = new CalcNavigationPagetLocators(client);
            // Optional: Wait for the app to be ready in constructor
            _locators.ButtonSettings.WaitForElementExist();
        }

        public CalcNavigationPaget<TPage> ClickSettings()
        {
            _locators.ButtonSettings.Click();
            return this;
        }
        public TPage ClickNavigationButton()
        {
            _locators.ButtonNavigation.Click();
            return _previousPage;
        }

    }
    public class CalcNavigationPagetLocators
    {
        private readonly UiAutomationService.UiAutomationServiceClient _client;

        public CalcNavigationPagetLocators(UiAutomationService.UiAutomationServiceClient client) => _client = client;

        private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiAutomationAdapter(_client, selector);

        private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

        private IAutomationElement Element(string automationId) =>
            CreateElement(() => Window.Descendants(new PropertyConditions().AutomationIdProperty(automationId)));

        public IAutomationElement ButtonSettings => Element("num1Button");
        public IAutomationElement ButtonNavigation => Element("GlobalNavButton");

    }
}
