using Allure.Net.Commons.Attributes;
using FluentAssertions;
using NUnit.Framework;
using Taf.Desktop.Core.Testing;
using Taf.Desktop.Core.Ui;
using Taf.Desktop.Core.Users;
using Taf.Desktop.Core.Verification;
using Taf.Desktop.Examples.Support;
using Taf.Desktop.Examples.Windows;

namespace Taf.Desktop.Examples.Tests;

[AllureFeature("Menus and dialogs")]
[AllureOwner("desktop-team")]
[TestUser("standard")]
[LoggedIn]
public class MenuAndDialogTests : ShopDeskTest
{
    [Test]
    public void AboutBoxShowsTheVersion()
    {
        var about = On<MainWindow>().OpenAbout();

        Verify.Equal(about.Title, "About ShopDesk", "Title");
        about.Message.Should().StartWith("ShopDesk 1.0");
        about.Ok();
    }

    [Test]
    public void ExportingAnEmptyCartExplainsWhy()
    {
        On<MainWindow>().StartExport();
        var message = On<MessageBoxDialog>();

        Verify.Equal(message.Message, "Your cart is empty", "Message");
        message.Ok();
    }

    [Test]
    [CartContains("Canvas Backpack", 2, Order = 1)]
    public void ExportWritesTheCartAsCsv()
    {
        var file = Path.Combine(Path.GetTempPath(), $"shopdesk-cart-{Guid.NewGuid():N}.csv");
        try
        {
            On<MainWindow>().StartExport();
            On<FileDialog>().Choose(file);

            File.ReadAllLines(file).Should().Equal("Product,Quantity,Amount", "Canvas Backpack,2,59.98");
        }
        finally
        {
            File.Delete(file);
        }
    }
}
