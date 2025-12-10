using System;
using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Helpers;

namespace UiAutomationGRPC.Client.Calc.Pages
{
    public abstract class BasePageObject<T> where T : BasePageObject<T>
    {
        protected readonly CalcPageLocators Locators;
        public UiAutomationDriver _driver;
        protected BasePageObject(UiAutomationDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            KeyboardHelper.Init(_driver);
        }
    }
}
