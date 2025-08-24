using DeepSigma.General;
using DeepSigma.General.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace DeepSigma.DataAccess.Utilities
{
    /// <summary>
    /// Manages a collection of keys, potentially for API access or encryption purposes.
    /// </summary>
    public class KeyChain
    {

        private Dictionary<string, KeyChainItem> Keys = [];
        public required string FilePath { get; init; }

        /// <summary>
        /// Initializes a new instance of the KeyChain class.
        /// </summary>
        /// <param name="full_file_path">Required file path</param>
        [SetsRequiredMembers]
        public KeyChain(string full_file_path)
        {
            ValidateExistingFilePath(full_file_path);
            this.FilePath = full_file_path;
            LoadKeysFromFile();
        }

        public bool TryToAddKey(string name, string key)
        {
            if (!Keys.ContainsKey(name))
            {
                Keys[name] = new KeyChainItem(name, key);
                return true;
            }
            return false;
        }

        public KeyChainItem? GetKey(string name)
        {
            if (Keys.TryGetValue(name, out var keyItem))
            {
                return keyItem;
            }
            return null;
        }

        private void LoadKeysFromFile()
        {
            string json_text = File.ReadAllText(this.FilePath);
            Keys = SerializationUtilities.GetDeserializedObject<Dictionary<string, KeyChainItem>>(json_text) ?? [];
        }

        public static void GenerateKeyChainFile(Dictionary<string, KeyChainItem> KeyChain, string full_file_path)
        {
            ValidateNewFilePath(full_file_path);
            string text = SerializationUtilities.GetSerializedString(KeyChain);
            File.WriteAllText(full_file_path, text);
        }

        private static void ValidateExistingFilePath(string full_file_path)
        {
            if (File.Exists(full_file_path) == false)
            {
                throw new ArgumentException($"File does not exists: {full_file_path}");
            }

            if (Path.GetExtension(full_file_path).ToLower() != ".json")
            {
                throw new ArgumentException($"File must be a .json file: {full_file_path}");
            }
        }

        private static void ValidateNewFilePath(string full_file_path)
        {
            if (File.Exists(full_file_path))
            {
                throw new ArgumentException($"File already exists: {full_file_path}");
            }

            if (Path.GetExtension(full_file_path).ToLower() != ".json")
            {
                throw new ArgumentException($"File must be a .json file: {full_file_path}");
            }
        }
    }
}
