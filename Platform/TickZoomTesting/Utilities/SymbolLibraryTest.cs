using NUnit.Framework;
using TickZoom.Api;
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

        [Test]
        public void TestCustomProperty()
        {
            var symbol = Factory.Symbol.LookupSymbol("SPYTest");
            Assert.AreEqual(symbol["CustomProperty"], "TestValue", "custom property");
        }

        [Test]
        public void TestInheritedCustomProperty()
        {
            var symbol = Factory.Symbol.LookupSymbol("CSCO");
            Assert.AreEqual(symbol["InheritedCustomProperty"], "Testing1", "custom property");
        }

        [Test]
        public void TestInheritedUserCustomProperty()
        {
            var symbol = Factory.Symbol.LookupSymbol("ESU2");
            Assert.AreEqual(symbol["InheritedUserCustomProperty"], "Testing2", "custom property");
        }

    }
}