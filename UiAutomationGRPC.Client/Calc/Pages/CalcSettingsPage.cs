using System;
using UiAutomation;
using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Locators;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public class CalcSettingsPage<TPage> : BasePageObject<TPage> where TPage : BasePageObject<TPage>
    {
        private readonly TPage _previousPage;
        private readonly CalcSettingsPageLocators _locators;

        public CalcSettingsPage(UiAutomationService.UiAutomationServiceClient client, TPage previousPage)  : base(client)
        {
        
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _previousPage = previousPage;
            _locators = new CalcSettingsPageLocators(client);
            // Optional: Wait for the app to be ready in constructor
            _locators.BackButton.WaitForElementExist();
        }

        public CalcPage ClickBack()
        {
            _locators.BackButton.Click();
            return new CalcPage(_client);
        }
    }
    public class CalcSettingsPageLocators
    {
        private readonly UiAutomationService.UiAutomationServiceClient _client;

        public CalcSettingsPageLocators(UiAutomationService.UiAutomationServiceClient client) => _client = client;

        private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiAutomationAdapter(_client, selector);

        private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

        public IAutomationElement BackButton => CreateElement(() => Window.Descendants().NameContain("Back"));

    }
}
