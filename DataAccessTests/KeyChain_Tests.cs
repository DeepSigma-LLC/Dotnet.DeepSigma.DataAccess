using DeepSigma.DataAccess.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DataAccessTests
{
    public class KeyChain_Tests
    {
        [Fact]
        public void KeyChain_ShouldCreateValidObject()
        {
            KeyChain chain = MyKeyChain.GetKeys();
            Assert.NotNull(chain);
        }

        [Fact]
        public void KeyChain_ShouldHaveExpectedKeys()
        {
            KeyChain chain = MyKeyChain.GetKeys();
            Assert.NotNull(chain);
            KeyChainItem? key = chain.GetKey("AlphaVantageDemo");
            Assert.NotNull(key);
        }
    }
}
