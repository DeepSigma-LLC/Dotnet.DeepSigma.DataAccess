using DeepSigma.General;

namespace DataAccessTests
{
    internal static class MyKeyChain
    {
        internal static KeyChain GetKeys()
        {
            string path = Path.Combine(Environment.CurrentDirectory, "TestData", "KeyChain-0d155f10-2518-4ee0-a0d7-41ff76cd7ee0.json");
            KeyChain keyChain = new(path);
            return keyChain;
        }
    }
}
