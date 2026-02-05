using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Helpers;

namespace UiAutomationGRPC.Client.Calc.Pages;

public abstract class BasePageObject<T> where T : BasePageObject<T>
{
    public UiAutomationDriver _driver;
    protected VirtualKeyboard Keyboard { get; }
    protected VirtualMouse Mouse { get; }
    
    protected BasePageObject(UiAutomationDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        Keyboard = new VirtualKeyboard(driver);
        Mouse = new VirtualMouse(driver);
    }
}
