using LvlUp.Services;
using NUnit.Framework;

namespace LvlUp.Tests
{
    public class PurchasePlacementContextTests
    {
        private double _now;
        private PurchasePlacementContext _context;

        [SetUp]
        public void SetUp()
        {
            _now = 0;
            _context = new PurchasePlacementContext(() => _now);
        }

        [Test]
        public void ProductPlacementIsConsumedByTheNextPurchaseOfThatProduct()
        {
            _context.SetPlacement("out_of_moves", "moves_5");

            Assert.IsNull(_context.Resolve("coins_100", null));
            Assert.AreEqual("out_of_moves", _context.Resolve("moves_5", null));
            Assert.IsNull(_context.Resolve("moves_5", null));
        }

        [Test]
        public void ProductPlacementWinsOverAnyProductAndBothAreConsumed()
        {
            _context.SetPlacement("shop");
            _context.SetPlacement("offer_popup", "starter_pack");

            Assert.AreEqual("offer_popup", _context.Resolve("starter_pack", null));
            Assert.IsNull(_context.Resolve("coins_100", null));
        }

        [Test]
        public void AnyProductPlacementAppliesToWhicheverProductIsBoughtNext()
        {
            _context.SetPlacement("shop");

            Assert.AreEqual("shop", _context.Resolve("coins_100", null));
            Assert.IsNull(_context.Resolve("coins_100", null));
        }

        [Test]
        public void ExplicitPlacementWinsAndStillConsumesPendingPlacement()
        {
            _context.SetPlacement("shop", "coins_100");

            Assert.AreEqual("level_end", _context.Resolve("coins_100", "  level_end "));
            Assert.IsNull(_context.Resolve("coins_100", null));
        }

        [Test]
        public void PlacementExpiresAfterTenMinutes()
        {
            _context.SetPlacement("shop", "coins_100");
            _now = 600;

            Assert.IsNull(_context.Resolve("coins_100", null));
        }

        [Test]
        public void PlacementIsTrimmedAndCappedAndBlankClears()
        {
            _context.SetPlacement("  " + new string('x', 80) + " ", "coins_100");
            Assert.AreEqual(new string('x', 64), _context.Resolve("coins_100", null));

            _context.SetPlacement("shop", "coins_100");
            _context.SetPlacement("   ", "coins_100");
            Assert.IsNull(_context.Resolve("coins_100", null));
        }

        [Test]
        public void ClearRemovesOneProductOrEverything()
        {
            _context.SetPlacement("shop", "coins_100");
            _context.SetPlacement("offer_popup", "starter_pack");
            _context.ClearPlacement("coins_100");
            Assert.IsNull(_context.Resolve("coins_100", null));

            _context.SetPlacement("shop");
            _context.ClearPlacement();
            Assert.IsNull(_context.Resolve("starter_pack", null));
        }
    }
}
