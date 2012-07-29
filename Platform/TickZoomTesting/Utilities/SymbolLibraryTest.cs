using NUnit.Framework;
using TickZoom.Symbols;

namespace TickZoom.Utilities
{
    [TestFixture] 
    public class SymbolLibraryTest
    {
        [Test]
        public void TestGetSource()
        {
            var source = SymbolLibrary.GetSymbolSource("USD/JPY.mbt!market");
            Assert.AreEqual("mbt", source);
            source = SymbolLibrary.GetSymbolSource("USD/JPY.mbt.10minute!market");
            Assert.AreEqual("mbt.10minute", source);
            source = SymbolLibrary.GetSymbolSource("USD/JPY!market");
            Assert.AreEqual("default", source);

            source = SymbolLibrary.GetSymbolSource("USD/JPY.mbt");
            Assert.AreEqual("mbt", source);
            source = SymbolLibrary.GetSymbolSource("USD/JPY.mbt.10minute");
            Assert.AreEqual("mbt.10minute", source);
            source = SymbolLibrary.GetSymbolSource("USD/JPY");
            Assert.AreEqual("default", source);
        }
    }
}