using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Selectors;
using UiAutomationGRPC.Library.Elements;

namespace UiAutomationGRPC.Client.Calc.Pages;

public class CalcNavigationPaget<TPage> : BasePageObject<TPage> where TPage : BasePageObject<TPage>
{
    private readonly TPage _previousPage;
    private readonly CalcNavigationPagetLocators _locators;
    
    public CalcNavigationPaget(UiAutomationDriver driver, TPage previousPage) : base(driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _previousPage = previousPage;
        _locators = new CalcNavigationPagetLocators(driver);
    }

    /// <summary>
    /// Waits for the page to be ready. Call after construction.
    /// </summary>
    public Task<CalcNavigationPaget<TPage>> WaitForReady() =>
        ResolveAsync(async () => { await _locators.ButtonSettings.WaitForElementExistAsync(); return this; });

    public Task<CalcSettingsPage<TPage>> ClickSettings() =>
        ResolveAsync(async () => { await _locators.ButtonSettings.ClickAsync(); return new CalcSettingsPage<TPage>(_driver, _previousPage); });
    
    public Task<TPage> ClickNavigationButton() =>
        ResolveAsync(async () => { await _locators.ButtonNavigation.ClickAsync(); return _previousPage; });
}

public class CalcNavigationPagetLocators
{
    private readonly UiAutomationDriver _driver;

    public CalcNavigationPagetLocators(UiAutomationDriver driver) => _driver = driver;

    private IAutomationElement CreateElement(Func<BaseSelector> selector) => new UiElement(_driver, selector);

    private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

    private IAutomationElement Element(string automationId) =>
        CreateElement(() => Window.Descendants(new PropertyConditions().AutomationIdProperty(automationId)));

    public IAutomationElement ButtonSettings => Element("num1Button");
    public IAutomationElement ButtonNavigation => Element("GlobalNavButton");
}
