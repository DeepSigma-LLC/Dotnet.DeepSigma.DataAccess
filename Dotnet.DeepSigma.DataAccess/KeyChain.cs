using DeepSigma.DataAccess.Models;
using DeepSigma.General.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace DeepSigma.DataAccess
{
    /// <summary>
    /// Manages a collection of keys, potentially for API access or encryption purposes.
    /// </summary>
    public class KeyChain
    {

        private Dictionary<string, KeyChainItem> Keys = [];
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the KeyChain class.
        /// </summary>
        public KeyChain() {   }

        /// <summary>
        /// Initializes a new instance of the KeyChain class.
        /// </summary>
        /// <param name="full_json_file_path">Required file path</param>
        public KeyChain(string full_json_file_path)
        {
            ValidateExistingFilePath(full_json_file_path);
            FilePath = full_json_file_path;
            LoadKeysFromFile();
        }

        /// <summary>
        /// Attempts to add a new key to the key chain.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool TryToAddKey(string name, string key)
        {
            if (!Keys.ContainsKey(name))
            {
                Keys[name] = new KeyChainItem(name, key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Retrieves a key by its name from the key chain.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public KeyChainItem? GetKey(string name)
        {
            if (Keys.TryGetValue(name, out var keyItem))
            {
                return keyItem;
            }
            return null;
        }

        /// <summary>
        /// Generates a new key chain file at the specified path with the provided keys.
        /// </summary>
        /// <param name="KeyChain"></param>
        /// <param name="full_file_path"></param>
        public void ExportToNewKeyChainFile(Dictionary<string, KeyChainItem> KeyChain, string full_file_path)
        {
            ValidateNewFilePath(full_file_path);
            string text = SerializationUtilities.GetSerializedString(KeyChain);
            File.WriteAllText(full_file_path, text);
        }

        /// <summary>
        /// Saves the current key chain to the existing file path, overwriting any existing file.
        /// </summary>
        public void SaveKeyChainByOverwritingExistingFile()
        {
            ValidateNewFilePath(FilePath);
            string text = SerializationUtilities.GetSerializedString(this);
            File.WriteAllText(FilePath, text);
        }

        private void LoadKeysFromFile()
        {
            string json_text = File.ReadAllText(FilePath);
            Keys = SerializationUtilities.GetDeserializedObject<Dictionary<string, KeyChainItem>>(json_text) ?? [];
        }

        private static void ValidateExistingFilePath(string full_file_path)
        {
            if(string.IsNullOrWhiteSpace(full_file_path))
            {
                throw new ArgumentException("File path is null or empty.", nameof(full_file_path));
            }

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
            if (string.IsNullOrWhiteSpace(full_file_path))
            {
                throw new ArgumentException("File path is null or empty.", nameof(full_file_path));
            }

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
