using System;
using UiAutomation;
using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Locators;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public class CalcNavigationPaget<TPage> : BasePageObject<TPage> where TPage : BasePageObject<TPage>
    {
        private readonly TPage _previousPage;
        private readonly CalcNavigationPagetLocators _locators;
        public CalcNavigationPaget(UiAutomationService.UiAutomationServiceClient client, TPage previousPage) : base(client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _previousPage = previousPage;
            _locators = new CalcNavigationPagetLocators(client);
            // Optional: Wait for the app to be ready in constructor
            _locators.ButtonSettings.WaitForElementExist();
        }

        public CalcSettingsPage<TPage> ClickSettings()
        {
            _locators.ButtonSettings.Click();
            return new CalcSettingsPage<TPage>(_client, _previousPage);
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
