using Allure.Net.Commons.Attributes;
using NUnit.Framework;
using Taf.Desktop.Core.Lint;
using Taf.Desktop.Core.Testing;

namespace Taf.Desktop.Examples.Tests;

/// <summary>Static checks of this assembly: owners, features, window identifiers, quarantines, secrets. No app needed.</summary>
[AllureFeature("Suite hygiene")]
[AllureOwner("qa-platform")]
public class MetadataLintTest : TafTest
{
    [Test]
    [Category(TestCategories.Static)]
    public void TestsAndWindowsHaveTheRequiredMetadata() =>
        MetadataLinter.ForAssembly(typeof(MetadataLintTest).Assembly).AssertClean();
}
