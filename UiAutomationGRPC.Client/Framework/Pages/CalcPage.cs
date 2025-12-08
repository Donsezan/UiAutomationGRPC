using UiAutomationGRPC.Client.Framework.Locators;

namespace UiAutomationGRPC.Client.Framework.Pages
{
    public class CalcPage : BasePageObject<CalcPage>
    {
        public CalcPage(ILocators locators) : base(locators)
        {
            // Optional: Wait for the app to be ready in constructor
            Locators.CalcPage.ResultText.WaitForElementExist();
        }

        public CalcPage ClickTwo()
        {
            Locators.CalcPage.ButtonTwo.Click();
            return this;
        }

        public CalcPage ClickPlus()
        {
            Locators.CalcPage.ButtonPlus.Click();
            return this;
        }

        public CalcPage ClickEqual()
        {
            Locators.CalcPage.ButtonEqual.Click();
            return this;
        }

        public string GetResult()
        {
            return Locators.CalcPage.ResultText.Name();
        }
    }
}
