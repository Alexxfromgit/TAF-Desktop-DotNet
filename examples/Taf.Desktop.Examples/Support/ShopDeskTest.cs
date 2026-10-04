using Allure.Net.Commons.Attributes;
using Taf.Desktop.Core.Mocking;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Examples.Backend;

namespace Taf.Desktop.Examples.Support;

/// <summary>Base of the ShopDesk tests: every test starts with the backend in its default state.</summary>
[AllureEpic("ShopDesk")]
public abstract class ShopDeskTest : DesktopTest
{
    protected static MockBackend Backend => ShopBackend.Mock;

    protected override void BeforeAppStart(TestScope scope) => Backend.Reset();
}
