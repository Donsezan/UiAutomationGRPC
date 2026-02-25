using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Selectors;
using UiAutomationGRPC.Library.Elements;

namespace UiAutomationGRPC.Client.Calc.Pages;

public class CalcSettingsPage<TPage> : BasePageObject<TPage> where TPage : BasePageObject<TPage>
{
    private readonly CalcSettingsPageLocators _locators;

    public CalcSettingsPage(UiAutomationDriver driver, TPage previousPage) : base(driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _locators = new CalcSettingsPageLocators(driver);
    }

    /// <summary>
    /// Waits for the page to be ready. Call after construction.
    /// </summary>
    public Task<CalcSettingsPage<TPage>> WaitForReady() =>
        ResolveAsync(async () => { await _locators.BackButton.WaitForElementExistAsync(); return this; });

    public Task<CalcPage> ClickBack() =>
        ResolveAsync(async () => { await _locators.BackButton.ClickAsync(); return new CalcPage(_driver); });
}

public class CalcSettingsPageLocators
{
    private readonly UiAutomationDriver _driver;

    public CalcSettingsPageLocators(UiAutomationDriver driver) => _driver = driver;

    private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiElement(_driver, selector);

    private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

    public IAutomationElement BackButton => CreateElement(() => Window.Descendants().NameContain("Back"));
}
