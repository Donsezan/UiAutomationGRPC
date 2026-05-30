using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Selectors;
using UiAutomationGRPC.Library.Elements;

namespace UiAutomationGRPC.Client.Calc.Pages;

/// <summary>
/// Page Object for Calculator's Settings / About page (reached via
/// <see cref="CalcNavigationPage.OpenSettings"/>). Exposes the theme expander, the build version, and
/// the Back button. All locators are real AutomationIds verified live against Windows Calculator.
/// </summary>
public class CalcSettingsPage : BasePageObject<CalcSettingsPage>
{
    private readonly CalcSettingsLocators _locators;

    public CalcSettingsPage(UiAutomationDriver driver) : base(driver)
    {
        _locators = new CalcSettingsLocators(driver);
    }

    /// <summary>Waits until the Settings page is shown (the Back button is present).</summary>
    public Task<CalcSettingsPage> WaitForReady() =>
        ResolveAsync(async () => { await _locators.BackButton.WaitForElementExistAsync(); return this; });

    /// <summary>Reads the page header — expected to be "Settings".</summary>
    public Task<string> GetTitle() =>
        ResolveAsync(() => _locators.Header.NameAsync());

    /// <summary>Reads the build version shown under About, e.g. "11.2508.4.0".</summary>
    public Task<string> GetBuildVersion() =>
        ResolveAsync(() => _locators.AboutBuildVersion.NameAsync());

    /// <summary>Navigates back to the calculator and returns to <see cref="CalcPage"/>.</summary>
    public Task<CalcPage> ClickBack() =>
        ResolveAsync(async () =>
        {
            await _locators.BackButton.ClickAsync();
            return await new CalcPage(_driver).WaitForReady();
        });
}

public class CalcSettingsLocators
{
    private readonly UiAutomationDriver _driver;

    public CalcSettingsLocators(UiAutomationDriver driver) => _driver = driver;

    private Selector Window => new Selector(new PropertyConditions().NameProperty("Calculator"));

    private IAutomationElement E(string automationId) =>
        new UiElement(_driver, () => Window.Descendants(new PropertyConditions().AutomationIdProperty(automationId)));

    public IAutomationElement Header => E("Header");                       // Name == "Settings" on this page
    public IAutomationElement BackButton => E("BackButton");
    public IAutomationElement AppThemeExpander => E("AppThemeExpander");
    public IAutomationElement AboutBuildVersion => E("AboutBuildVersion");
    public IAutomationElement FeedbackButton => E("FeedbackButton");
}
