using System;
using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Locators;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public class CalcSettingsPage<TPage> : BasePageObject<TPage> where TPage : BasePageObject<TPage>
    {
        private readonly CalcSettingsPageLocators _locators;

        public CalcSettingsPage(UiAutomationDriver driver, TPage previousPage)  : base(driver)
        {
        
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _locators = new CalcSettingsPageLocators(driver);
            // Optional: Wait for the app to be ready in constructor
            _locators.BackButton.WaitForElementExist();
        }

        public CalcPage ClickBack()
        {
            _locators.BackButton.Click();
            return new CalcPage(_driver);
        }
    }
    public class CalcSettingsPageLocators
    {
        private readonly UiAutomationDriver _driver;

        public CalcSettingsPageLocators(UiAutomationDriver driver) => _driver = driver;

        private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiAutomationAdapter(_driver, selector);

        private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

        public IAutomationElement BackButton => CreateElement(() => Window.Descendants().NameContain("Back"));

    }
}
