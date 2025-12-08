using UiAutomationGRPC.Client.Framework.Locators;

namespace UiAutomationGRPC.Client.Framework.Pages
{
    public abstract class BasePageObject<T> where T : BasePageObject<T>
    {
        protected readonly ILocators Locators;

        protected BasePageObject(ILocators locators)
        {
            Locators = locators;
        }
    }
}
