using System.Linq;
using Deucarian.Editor;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class ControlCenterRegistrationTests
    {
        private const string PackageId =
            "com.deucarian.authentication";

        [Test]
        public void PackageRegistersStableToolAndCard()
        {
            Assert.That(
                DeucarianToolRegistry.TryGet(
                    DeucarianToolIds.Authentication,
                    out DeucarianToolDescriptor tool),
                Is.True);
            Assert.That(tool.OwningPackage, Is.EqualTo(PackageId));

            DeucarianControlCenterSnapshot snapshot =
                DeucarianControlCenterSnapshotBuilder.Capture(true);
            Assert.That(
                snapshot.Cards.Any(
                    card => card.OwningPackage == PackageId),
                Is.True);
        }
    }
}
